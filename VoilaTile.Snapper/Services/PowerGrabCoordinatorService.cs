namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Media;
    using VoilaTile.Snapper.Input;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.ViewModels;
    using VoilaTile.Snapper.Views;

    internal sealed class PowerGrabCoordinatorService
    {
        private readonly InputStateManager inputState;
        private readonly IWindowEnumerator enumerator;
        private readonly IWindowIconService icons;
        private readonly IHintService hints;
        private readonly IWindowFocusService focus;
        private readonly IDwmThumbnailSurfaceFactory surfaceFactory;
        private readonly Func<IntPtr> hostHwndProvider;

        private PowerGrabOverlayView? view;
        private PowerGrabOverlayViewModel? vm;
        private bool isActive;

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

        public bool IsActive => this.isActive;

        public void Begin()
        {
            if (this.isActive) return;

            this.isActive = true;
            //this.inputState.EnterInputMode();
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

        public void Cancel()
        {
            if (!this.isActive) return;

            try { this.view?.CloseSafely(); } catch { }

            this.view = null;
            this.vm = null;

            this.inputState.ReturnToHotKeyMode();
            this.isActive = false;
        }

        public void ForwardCharacter(char c)
        {
            if (!this.isActive || this.vm is null) return;
            this.vm.TypeChar(char.ToUpperInvariant(c));
        }

        public void Backspace()
        {
            if (!this.isActive || this.vm is null) return;
            this.vm.Backspace();
        }

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

        public void ShowPreview()
        {
            if (!this.isActive || this.vm is null) return;

            this.vm.ShowPreview();
        }

        public void HidePreview()
        {
            if (!this.isActive || this.vm is null) return;

            this.vm.HidePreview();
        }
    }
}
