namespace VoilaTile.Configurator.Helpers
{
    using System.Windows;
    using VoilaTile.Configurator.ViewModels;
    using VoilaTile.Configurator.Views;

    /// <summary>
    /// A helper class responsible for managing the monitor overlays.
    /// </summary>
    public static class OverlayManager
    {
        private static MonitorOverlayWindow? currentOverlay;
        private static string? currentMonitorName;

        public static void ShowOverlay(MonitorViewModel monitor)
        {
            if (currentOverlay is not null && currentMonitorName == monitor.DeviceName)
            {
                currentOverlay.DataContext = monitor.SelectedTemplate;
                return;
            }

            HideOverlay();

            var owner = Application.Current.MainWindow;

            var overlay = new MonitorOverlayWindow
            {
                DataContext = monitor.SelectedTemplate,
                Left = monitor.MonitorInfo.WorkX,
                Top = monitor.MonitorInfo.WorkY,
                Width = monitor.MonitorInfo.WorkWidth,
                Height = monitor.MonitorInfo.WorkHeight,
                ShowActivated = false,
                Topmost = false,
            };

            currentOverlay = overlay;
            currentMonitorName = monitor.DeviceName;
            overlay.Show();

            bool isSameMonitor = MonitorUtils.IsSameMonitor(owner, monitor.MonitorInfo);

            if (isSameMonitor)
            {
                // On same monitor: focus main window after showing overlay
                owner.Activate(); // Brings it above the overlay
            }
            else
            {
                // On different monitors: reliably use Win32 z-order
                WindowZOrderHelper.PlaceWindowBelow(overlay, owner);
            }
        }

        public static void HideOverlay()
        {
            if (currentOverlay != null)
            {
                currentOverlay.Close();
                currentOverlay = null;
            }
        }
    }

}
