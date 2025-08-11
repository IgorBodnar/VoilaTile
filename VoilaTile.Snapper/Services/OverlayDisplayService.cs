namespace VoilaTile.Snapper.Services
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.ViewModels;
    using VoilaTile.Snapper.Views;

    /// <summary>
    /// Manages creation and visibility of all monitor overlay windows.
    /// </summary>
    public class OverlayDisplayService
    {
        private readonly Dictionary<string, OverlayWindow> overlays = new();

        /// <summary>
        /// Shows overlays for the specified monitor layouts.
        /// </summary>
        /// <param name="layouts">The resolved layouts per monitor.</param>
        public void ShowOverlays(List<OverlayViewModel> overlays)
        {
            foreach (var overlay in overlays)
            {

                var window = new OverlayWindow(overlay);

                // Position to monitor bounds
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = overlay.Layout.Monitor.WorkX;
                window.Top = overlay.Layout.Monitor.WorkY;
                window.Width = overlay.Layout.Monitor.WorkWidth;
                window.Height = overlay.Layout.Monitor.WorkHeight;

                this.overlays[overlay.Layout.Monitor.DeviceID] = window;

                window.Show();

            }
        }

        /// <summary>
        /// Hides and clears all overlay windows.
        /// </summary>
        public void HideOverlays()
        {
            foreach (var window in overlays.Values)
            {
                window.Hide();
                window.Close();
            }

            overlays.Clear();
        }
    }
}

