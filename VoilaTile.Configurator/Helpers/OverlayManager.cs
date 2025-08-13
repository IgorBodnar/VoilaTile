namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using VoilaTile.Configurator.ViewModels;
    using VoilaTile.Configurator.Views;

    public static class OverlayManager
    {
        private static MonitorOverlayWindow? currentOverlay;
        private static string? currentMonitorName;

        public static void ShowOverlay(MonitorViewModel monitor)
        {
            // Target rectangle in physical pixels from MonitorInfo (work area)
            int pxX = monitor.MonitorInfo.WorkX;
            int pxY = monitor.MonitorInfo.WorkY;
            int pxW = monitor.MonitorInfo.WorkWidth;
            int pxH = monitor.MonitorInfo.WorkHeight;

            if (currentOverlay is not null && currentMonitorName == monitor.DeviceName)
            {
                currentOverlay.DataContext = monitor.SelectedTemplate;
                ApplyBoundsPx(currentOverlay, pxX, pxY, pxW, pxH);
                return;
            }

            HideOverlay();

            var owner = Application.Current.MainWindow;

            var overlay = new MonitorOverlayWindow
            {
                DataContext = monitor.SelectedTemplate,
                ShowActivated = false,
                Topmost = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = 1,
                Height = 1,
                Left = 0,
                Top = 0,
                ResizeMode = ResizeMode.NoResize,
            };

            currentOverlay = overlay;
            currentMonitorName = monitor.DeviceName;

            overlay.SourceInitialized += (_, __) =>
            {
                HookDpiChanged(overlay, () =>
                {
                    ApplyBoundsPx(overlay, pxX, pxY, pxW, pxH);
                });

                ApplyBoundsPx(overlay, pxX, pxY, pxW, pxH);
            };

            overlay.Show();

            bool isSameMonitor = MonitorUtils.IsSameMonitor(owner, monitor.MonitorInfo);
            if (isSameMonitor)
                owner.Activate();
            else
                WindowZOrderHelper.PlaceWindowBelow(overlay, owner);
        }

        public static void HideOverlay()
        {
            if (currentOverlay != null)
            {
                currentOverlay.Close();
                currentOverlay = null;
                currentMonitorName = null;
            }
        }

        private static void ApplyBoundsPx(Window window, int x, int y, int width, int height)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;

            // Set the window rect in physical pixels.
            SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        private static void HookDpiChanged(Window window, Action onDpiChanged)
        {
            var src = (HwndSource?)PresentationSource.FromVisual(window);
            if (src == null) return;

            src.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                const int WM_DPICHANGED = 0x02E0;
                if (msg == WM_DPICHANGED)
                {
                    // Ignore suggested rect and enforce our own size.
                    onDpiChanged();
                    handled = true;
                }
                return IntPtr.Zero;
            });
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);
    }
}

