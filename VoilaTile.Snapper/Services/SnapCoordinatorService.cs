namespace VoilaTile.Snapper.Services
{
    using System.Collections.Generic;
    using VoilaTile.Snapper.Input;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// Coordinates snapping based on input from overlays and manages global state transitions.
    /// </summary>
    public class SnapCoordinatorService
    {
        private readonly OverlayDisplayService overlayDisplayService;
        private readonly WindowSnappingService windowSnappingService;
        private readonly InputStateManager inputStateManager;

        // Current input matches per monitor (DeviceID → tile)
        private ResolvedTileModel? selectedTile;

        // Tracks last foreground window to snap
        private SnappableWindowInfo? targetWindow;

        // Active overlay view models
        private readonly List<OverlayViewModel> activeOverlays = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapCoordinatorService"/> class.
        /// </summary>
        public SnapCoordinatorService(
            OverlayDisplayService overlayDisplayService,
            WindowSnappingService windowSnappingService,
            InputStateManager inputStateManager)
        {
            this.overlayDisplayService = overlayDisplayService;
            this.windowSnappingService = windowSnappingService;
            this.inputStateManager = inputStateManager;
        }

        /// <summary>
        /// Launches overlays and starts the snapping process.
        /// </summary>
        /// <param name="layouts">Resolved layouts from LayoutResolver.</param>
        public void BeginSnapping(List<ZoneLayoutModel> layouts)
        {
            this.targetWindow = windowSnappingService.GetFocusedWindow();

            if (this.targetWindow == null)
            {
                return;
            }

            this.selectedTile = null;
            this.activeOverlays.Clear();

            foreach (var layout in layouts) 
            {
                var viewModel = new OverlayViewModel(layout, (match) =>
                {
                    this.selectedTile = match;
                });

                this.activeOverlays.Add(viewModel);
            }

            this.overlayDisplayService.ShowOverlays(this.activeOverlays);

            this.inputStateManager.EnterInputMode();
        }

        /// <summary>
        /// Cancels the current snap session (e.g. on Esc).
        /// </summary>
        public void Cancel()
        {
            this.overlayDisplayService.HideOverlays();
            this.activeOverlays.Clear();
            this.selectedTile = null;
            this.inputStateManager.ReturnToHotKeyMode();
        }

        /// <summary>
        /// Finalizes snapping (e.g. on Space key press).
        /// </summary>
        public void CommitSnap()
        {
            if (this.targetWindow == null)
            {
                Cancel();
                return;
            }

            if (this.selectedTile != null)
            {
                this.windowSnappingService.SnapWindow(this.targetWindow, this.selectedTile);
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
    }
}

