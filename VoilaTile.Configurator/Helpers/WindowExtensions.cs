namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    public static class WindowExtensions
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        /// <summary>
        /// Atomically sets the position and size of a WPF window using a System.Drawing.Rectangle.
        /// </summary>
        /// <param name="window">The target Window.</param>
        /// <param name="bounds">The desired window bounds in screen coordinates.</param>
        public static void SetWindowBounds(this Window window, Rectangle bounds)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                throw new InvalidOperationException("Window must be shown before setting bounds.");
            }

            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }
    }

}
