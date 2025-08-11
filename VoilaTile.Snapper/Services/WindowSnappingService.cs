namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Provides functionality to get and reposition windows using native APIs.
    /// </summary>
    public class WindowSnappingService
    {
        /// <summary>
        /// Retrieves the currently focused window if it is eligible for snapping.
        /// </summary>
        public SnappableWindowInfo? GetFocusedWindow()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))
            {
                return null;
            }

            // Skip known shell / system windows
            const int MaxClassNameLength = 256;
            var classNameBuilder = new StringBuilder(MaxClassNameLength);
            GetClassName(hwnd, classNameBuilder, MaxClassNameLength);
            string className = classNameBuilder.ToString();

            if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "ApplicationManager_DesktopShellWindow")
            {
                return null;
            }

            if (!GetWindowRect(hwnd, out RECT rect))
            {
                return null;
            }

            return new SnappableWindowInfo(
                hwnd,
                rect.Left,
                rect.Top,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top);
        }

        /// <summary>
        /// Snaps the given window to the specified tile, adjusting for window styles.
        /// </summary>
        public void SnapWindow(SnappableWindowInfo window, ResolvedTileModel tile)
        {
            if (!GetWindowRect(window.Hwnd, out RECT windowRect))
            {
                return;
            }

            // Try to get the extended frame bounds via DWM
            int leftMargin = 0;
            int topMargin = 0;
            int rightMargin = 0;
            int bottomMargin = 0;

            if (DwmGetWindowAttribute(window.Hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT frameRect, Marshal.SizeOf(typeof(RECT))) == 0)
            {
                leftMargin = frameRect.Left - windowRect.Left;
                topMargin = frameRect.Top - windowRect.Top;
                rightMargin = windowRect.Right - frameRect.Right;
                bottomMargin = windowRect.Bottom - frameRect.Bottom;

                Debug.WriteLine($"Margins: Left={leftMargin}, Right={rightMargin}, Top={topMargin}, Bottom={bottomMargin}");
            }

            SetWindowPos(
                window.Hwnd,
                IntPtr.Zero,
                (int)tile.ScreenX - leftMargin,
                (int)tile.ScreenY - topMargin,
                (int)tile.Width + leftMargin + rightMargin,
                (int)tile.Height + topMargin + bottomMargin,
                SetWindowPosFlags.ShowWindow);
        }

        #region Win32

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [Flags]
        private enum SetWindowPosFlags : uint
        {
            NoZOrder = 0x0004,
            NoActivate = 0x0010,
            ShowWindow = 0x0040
        }

        #endregion
    }
}

