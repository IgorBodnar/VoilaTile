// File: Services/QuickGrabCoordinatorService.cs
// -------------------------------------------------------------------------------------
// Coordinates Quick Grab (focus-only, hint-driven) overlays and input.
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.Helpers;
    using VoilaTile.Snapper.Input;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services.Interfaces;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// Coordinates Quick Grab overlays and manages lifecycle + input.
    /// </summary>
    public sealed class QuickGrabCoordinatorService : IKeyboardControllable
    {
        private readonly InputStateManager inputStateManager;
        private readonly OverlayDisplayService overlayDisplayService;
        private readonly IWindowEnumerator windowEnumerator;
        private readonly IHintPlacementService hintPlacementService;
        private readonly IHintService hintService;
        private readonly IWindowFocusService focusService;

        private readonly List<QuickGrabOverlayViewModel> activeOverlays = new();

        private QuickHintViewModel? selectedHint;

        public QuickGrabCoordinatorService(
            OverlayDisplayService overlayDisplayService,
            IWindowEnumerator windowEnumerator,
            IHintPlacementService hintPlacementService,
            IHintService hintService,
            InputStateManager inputStateManager,
            IWindowFocusService focusService)
        {
            this.overlayDisplayService = overlayDisplayService ?? throw new ArgumentNullException(nameof(overlayDisplayService));
            this.windowEnumerator = windowEnumerator ?? throw new ArgumentNullException(nameof(windowEnumerator));
            this.hintPlacementService = hintPlacementService ?? throw new ArgumentNullException(nameof(hintPlacementService));
            this.hintService = hintService ?? throw new ArgumentNullException(nameof(hintService));
            this.inputStateManager = inputStateManager ?? throw new ArgumentNullException(nameof(inputStateManager));
            this.focusService = focusService ?? throw new ArgumentNullException(nameof(focusService));
        }

        /// <inheritdoc/>
        public IDisposable Attach(GlobalInputListener listener)
        {
            var cd = new CompositeDisposable();

            void OnChar(char c) => this.ForwardCharacter(c);
            listener.OnCharacterTyped += OnChar;
            cd.Add(new AnonymousDisposable(() => listener.OnCharacterTyped -= OnChar));

            void OnBackspace() => this.Backspace();
            listener.OnBackspacePressed += OnBackspace;
            cd.Add(new AnonymousDisposable(() => listener.OnBackspacePressed -= OnBackspace));

            void OnEscape() => this.Cancel();
            listener.OnEscapePressed += OnEscape;
            cd.Add(new AnonymousDisposable(() => listener.OnEscapePressed -= OnEscape));

            void OnEnter() => this.Confirm();
            listener.OnEnterPressed += OnEnter;
            cd.Add(new AnonymousDisposable(() => listener.OnEnterPressed -= OnEnter));

            void OnSpace() => this.Confirm();
            listener.OnSpacePressed += OnSpace;
            cd.Add(new AnonymousDisposable(() => listener.OnSpacePressed -= OnSpace));

            return cd;
        }

        /// <summary>
        /// Starts Quick Grab: shows overlays, snapshots desktop, places & assigns hints.
        /// </summary>
        /// <summary>
        /// Starts Quick Grab: snapshots desktop, places & assigns hints, then shows overlays.
        /// </summary>
        public void Begin()
        {
            // 1) Capture monitors once
            var monitors = MonitorManager.GetMonitors();

            // 2) Reset local state (no overlays yet)
            this.activeOverlays.Clear();
            this.selectedHint = null;

            this.inputStateManager.EnterInputMode();

            // 3) Enumerate + place + assign on a worker thread
            Task.Run(() =>
            {
                var options = new WindowQueryOptions
                {
                    AltTabOnly = false,
                    IncludeMinimized = false,
                    IncludeToolWindows = false,
                    ExcludeCloaked = true,
                    MinWidth = 32,
                    MinHeight = 24,
                };

                var windows = this.windowEnumerator.Snapshot(options);

                // Placements (px + chosen monitor + Z)
                var placements = this.hintPlacementService.ComputePlacements(windows, monitors);

                // Assign hints deterministically
                var placedWithHints = AttachHints(placements, this.hintService);

                // Group by monitor & convert px -> DIP for overlay canvases
                var batches = QuickGrabBatchBuilder.BuildBatches(placedWithHints, monitors);

                // 4) Build VMs, attach badges, the show overlays (UI thread)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    this.activeOverlays.Clear();

                    foreach (var mon in monitors)
                    {
                        var vm = new QuickGrabOverlayViewModel(mon, match => this.selectedHint = match);

                        var batch = batches.FirstOrDefault(b => b.MonitorDeviceID == mon.DeviceID);
                        vm.SetBadges(batch?.Badges ?? Array.Empty<QuickGrabBadge>());

                        this.activeOverlays.Add(vm);
                    }

                    // Show overlays only after contents are ready
                    this.overlayDisplayService.ShowOverlays(this.activeOverlays);
                });
            });
        }


        /// <summary>
        /// Cancels the current Quick Grab session (e.g., Esc).
        /// </summary>
        public void Cancel()
        {
            this.overlayDisplayService.HideOverlays();
            this.activeOverlays.Clear();
            this.inputStateManager.ReturnToHotKeyMode();
        }

        /// <summary>
        /// Confirms the selection (e.g., Space/Enter). If there is a unique match across overlays,
        /// attempts to focus that window (when a focus service is provided).
        /// </summary>
        public void Confirm()
        {
            if (this.selectedHint is not null)
            {
                this.focusService.TryFocus(this.selectedHint.Id);
            }

            Cancel();
        }

        /// <summary>
        /// Forwards a character key to all overlays.
        /// </summary>
        public void ForwardCharacter(char c)
        {
            foreach (var vm in this.activeOverlays)
            {
                vm.AppendCharacter(c);
            }
        }

        /// <summary>
        /// Forwards backspace key to all overlays.
        /// </summary>
        public void Backspace()
        {
            foreach (var vm in this.activeOverlays)
            {
                vm.Backspace();
            }
        }

        private static IReadOnlyList<PlacedHintWithText> AttachHints(
            IReadOnlyList<HintPlacement> placements,
            IHintService hintService)
        {
            var ordered = placements
                .OrderBy(p => p.Z)
                .ThenBy(p => p.MonitorDeviceID, StringComparer.Ordinal)
                .ThenBy(p => p.Xpx)
                .ThenBy(p => p.Ypx)
                .ToList();

            var hints = hintService.AssignHints(ordered.Count);

            var result = new List<PlacedHintWithText>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                result.Add(new PlacedHintWithText(
                    p.Id,
                    p.MonitorDeviceID,
                    p.Xpx,
                    p.Ypx,
                    p.Z,
                    hints[i]));
            }

            return result;
        }
    }
}

