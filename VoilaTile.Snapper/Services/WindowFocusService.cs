namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Restores and focuses a target HWND using an attach-thread-input dance with fallbacks.
    /// </summary>
    internal sealed class WindowFocusService : IWindowFocusService
    {
        public bool TryFocus(WindowId id)
        {
            var hwnd = id.Hwnd;
            if (!Win32.IsWindow(hwnd))
            {
                return false;
            }

            // Restore if minimized
            if (Win32.IsIconic(hwnd))
            {
                _ = ShowWindow(hwnd, Win32.SW_RESTORE);
            }

            // Bring to foreground using ATI (AttachThreadInput) with the current foreground thread
            var fg = Win32.GetForegroundWindow();
             uint thisThread = GetCurrentThreadId();
            uint fgThread = Win32.GetWindowThreadProcessId(fg, out _);

            bool attached = false;
            try
            {
                attached = AttachThreadInput(thisThread, fgThread, true);

                // Give it a nudge to the top and focus
                _ = SetForegroundWindow(hwnd);
                _ = BringWindowToTop(hwnd);
                _ = SetFocus(hwnd);
            }
            finally
            {
                if (attached)
                {
                    _ = AttachThreadInput(thisThread, fgThread, false);
                }
            }

            // If it still isn't foreground, try AllowSetForegroundWindow and a slight delay
            if (Win32.GetForegroundWindow() != hwnd)
            {
                _ = AllowSetForegroundWindow(ASFW_ANY);
                _ = SetForegroundWindow(hwnd);

                // A tiny wait sometimes helps with weird shells
                Thread.Sleep(10);
            }

            // Final fallback: flash to request attention (do not block)
            bool focused = Win32.GetForegroundWindow() == hwnd;
            if (!focused)
            {
                var fi = new FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = hwnd,
                    dwFlags = FLASHW_TRAY,
                    uCount = 1,
                    dwTimeout = 0,
                };
                _ = FlashWindowEx(ref fi);
            }

            return focused;
        }

        #region user32 extras

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        private const int ASFW_ANY = -1;

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_TRAY = 0x00000002;

        #endregion
    }
}
