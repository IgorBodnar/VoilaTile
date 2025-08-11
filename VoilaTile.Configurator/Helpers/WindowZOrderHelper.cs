namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    public static class WindowZOrderHelper
    {
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public static void PlaceWindowBelow(IntPtr hwndToPlace, IntPtr hwndTarget)
        {
            SetWindowPos(hwndToPlace, hwndTarget, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public static void PlaceWindowBelow(Window overlay, Window target)
        {
            var hwndOverlay = new System.Windows.Interop.WindowInteropHelper(overlay).Handle;
            var hwndTarget = new System.Windows.Interop.WindowInteropHelper(target).Handle;
            PlaceWindowBelow(hwndOverlay, hwndTarget);
        }
    }
}
