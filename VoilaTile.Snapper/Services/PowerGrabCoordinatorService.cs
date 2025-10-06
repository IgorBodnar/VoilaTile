namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Media;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Snapper.Input;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.ViewModels;
    using VoilaTile.Snapper.Views;

    /// <summary>
    /// Coordinates the Power Grab (keyboard-first window switcher) flow:
    /// enumerates candidate windows, builds the view model and overlay,
    /// routes input to the view model, and finalizes focus operations.
    /// </summary>
    internal sealed class PowerGrabCoordinatorService
    {
        #region Fields

        /// <summary>
        /// Manages global input state and mode switching.
        /// </summary>
        private readonly InputStateManager inputState;

        /// <summary>
        /// Service used to enumerate windows.
        /// </summary>
        private readonly IWindowEnumerator enumerator;

        /// <summary>
        /// Service that resolves application icons for windows.
        /// </summary>
        private readonly IWindowIconService icons;

        /// <summary>
        /// Service that provides keyboard hint strings.
        /// </summary>
        private readonly IHintService hints;

        /// <summary>
        /// Service that moves focus to a target window.
        /// </summary>
        private readonly IWindowFocusService focus;

        /// <summary>
        /// Factory for creating DWM thumbnail surfaces tied to a host HWND.
        /// </summary>
        private readonly IDwmThumbnailSurfaceFactory surfaceFactory;

        /// <summary>
        /// Function that returns the host HWND for DWM thumbnail registration.
        /// </summary>
        private readonly Func<IntPtr> hostHwndProvider;

        /// <summary>
        /// The overlay view displayed during Power Grab.
        /// </summary>
        private PowerGrabOverlayView? view;

        /// <summary>
        /// The overlay view model backing the UI.
        /// </summary>
        private PowerGrabOverlayViewModel? vm;

        /// <summary>
        /// Indicates whether the Power Grab experience is currently active.
        /// </summary>
        private bool isActive;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerGrabCoordinatorService"/> class.
        /// </summary>
        /// <param name="inputState">Global input state manager.</param>
        /// <param name="enumerator">Window enumeration service.</param>
        /// <param name="icons">Window icon provider.</param>
        /// <param name="hints">Keyboard hint provider.</param>
        /// <param name="focus">Window focus service.</param>
        /// <param name="surfaceFactory">DWM thumbnail surface factory.</param>
        /// <param name="hostHwndProvider">Provider for the host HWND used by DWM thumbnails.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is <c>null</c>.</exception>
        public PowerGrabCoordinatorService(
            InputStateManager inputState,
            IWindowEnumerator enumerator,
            IWindowIconService icons,
            IHintService hints,
            IWindowFocusService focus,
            IDwmThumbnailSurfaceFactory surfaceFactory,
            Func<IntPtr> hostHwndProvider)
        {
            this.inputState = inputState ?? throw new ArgumentNullException(nameof(inputState));
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.icons = icons ?? throw new ArgumentNullException(nameof(icons));
            this.hints = hints ?? throw new ArgumentNullException(nameof(hints));
            this.focus = focus ?? throw new ArgumentNullException(nameof(focus));
            this.surfaceFactory = surfaceFactory ?? throw new ArgumentNullException(nameof(surfaceFactory));
            this.hostHwndProvider = hostHwndProvider ?? throw new ArgumentNullException(nameof(hostHwndProvider));
        }

        #endregion

        #region Events
        // No custom events declared.
        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether the Power Grab overlay is currently active.
        /// </summary>
        public bool IsActive => this.isActive;

        #endregion

        #region Methods

        /// <summary>
        /// Starts the Power Grab overlay: enumerates windows, builds the view model and view,
        /// and switches the input mode to Power Grab.
        /// </summary>
        public void Begin()
        {
            if (this.isActive) return;

            this.isActive = true;
            this.inputState.EnterInputMode();
            this.inputState.SwitchToPowerGrabFeature();

            // Enumerate with default options.
            var options = new WindowQueryOptions
            {
                AltTabOnly = false,
                IncludeToolWindows = false,
                IncludeMinimized = true,
                ExcludeCloaked = true,
                CurrentDesktopOnly = true,
                MinWidth = 10,
                MinHeight = 10,
            };

            var all = this.enumerator.Snapshot(options);

            // Require DWM thumbnail.
            using var prober = new DwmThumbnailProber(this.hostHwndProvider, TimeSpan.FromSeconds(5));
            var candidates = all.Where(e => prober.CanRegister(e.Id)).ToList();

            // Exclude this process.
            var selfExcluding = candidates.Where(e => e.ProcessName != Process.GetCurrentProcess().ProcessName);

            // Icons.
            var withIcons = selfExcluding.Select(e => e with { AppIcon = this.icons.GetIcon(e) }).ToList();

            // VM + view.
            this.vm = new PowerGrabOverlayViewModel(this.hints, withIcons);

            this.view = new PowerGrabOverlayView(this.surfaceFactory)
            {
                DataContext = this.vm,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false,
                Topmost = true,
            };

            this.view.Closed += (_, __) =>
            {
                this.view = null;
                this.vm = null;
            };

            this.view.Show();
        }

        /// <summary>
        /// Cancels and disposes the Power Grab overlay, restoring the prior input mode.
        /// </summary>
        public void Cancel()
        {
            if (!this.isActive) return;

            try { this.view?.Close(); } catch { }

            this.view = null;
            this.vm = null;

            this.inputState.ReturnToHotKeyMode();
            this.isActive = false;
        }

        /// <summary>
        /// Forwards a typed character to the overlay for filtering / hint matching.
        /// </summary>
        /// <param name="c">The character typed.</param>
        public void ForwardCharacter(char c)
        {
            if (!this.isActive || this.vm is null) return;
            this.vm.TypeChar(char.ToUpperInvariant(c));
        }

        /// <summary>
        /// Handles backspace input for the overlay's filter text.
        /// </summary>
        public void Backspace()
        {
            if (!this.isActive || this.vm is null) return;
            this.vm.Backspace();
        }

        /// <summary>
        /// Accepts the current selection (if any), attempts to focus the target window, and closes the overlay.
        /// </summary>
        public void AcceptSelection()
        {
            if (!this.isActive || this.vm is null) return;

            var selected = this.vm.CommitSelection();
            if (selected is not null)
            {
                var ok = this.focus.TryFocus(selected.Id);
                Debug.WriteLine($"[PowerMode] Focus {(ok ? "OK" : "FAILED")} → {selected.ProcessName}  '{selected.Title}'");
            }

            this.Cancel();
        }

        /// <summary>
        /// Shows the large preview of the currently highlighted item.
        /// </summary>
        public void ShowPreview()
        {
            if (!this.isActive || this.vm is null) return;

            this.vm.ShowPreview();
        }

        /// <summary>
        /// Hides the large preview if it is currently shown.
        /// </summary>
        public void HidePreview()
        {
            if (!this.isActive || this.vm is null) return;

            this.vm.HidePreview();
        }

        #endregion
    }
}

