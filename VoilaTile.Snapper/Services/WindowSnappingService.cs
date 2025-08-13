namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Configuration;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Provides functionality to get and reposition windows using native APIs.
    /// </summary>
    public class WindowSnappingService
    {
        /// <summary>
        /// The hash set of excluded window classes for snapping.
        /// </summary>
        private static readonly HashSet<string> ExcludedWindowClasses = new(StringComparer.Ordinal)
        {
            "Progman",
            "WorkerW",
            "Shell_TrayWnd",
            "ApplicationManager_DesktopShellWindow",
        };

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

            if (ExcludedWindowClasses.Contains(className))
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
                return;

            int targetX = (int)Math.Round(tile.ScreenX);
            int targetY = (int)Math.Round(tile.ScreenY);
            int targetW = Math.Max(1, (int)Math.Round(tile.Width));
            int targetH = Math.Max(1, (int)Math.Round(tile.Height));

            // Move to destination monitor first (no size), so DWM margins we read next are for the destination DPI.
            const SetWindowPosFlags MoveOnlyFlags =
                SetWindowPosFlags.NoSize | SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate;
            SetWindowPos(window.Hwnd, IntPtr.Zero, targetX, targetY, 0, 0, MoveOnlyFlags);

            // Refresh rects after the move
            GetWindowRect(window.Hwnd, out windowRect);

            // Read DWM extended-frame margins on the destination monitor.
            int leftMargin = 0, topMargin = 0, rightMargin = 0, bottomMargin = 0;
            if (DwmGetWindowAttribute(window.Hwnd, DWMWA_EXTENDED_FRAME_BOUNDS,
                                      out RECT frameRect, Marshal.SizeOf(typeof(RECT))) == 0)
            {
                leftMargin = frameRect.Left - windowRect.Left;
                topMargin = frameRect.Top - windowRect.Top;
                rightMargin = windowRect.Right - frameRect.Right;
                bottomMargin = windowRect.Bottom - frameRect.Bottom;
            }

            // Compute outer size for the desired client rect.
            int outerW = targetW + leftMargin + rightMargin;
            int outerH = targetH + topMargin + bottomMargin;

            // If the target app is Unaware/SystemAware on a >StandardDpi monitor,
            //     shrink (or grow) the outer size by StandardDpi/dpi so the visual area matches the tile.
            var ctx = GetWindowDpiAwarenessContext(window.Hwnd);
            var awareness = GetAwarenessFromDpiAwarenessContext(ctx);

            if (awareness == DPI_AWARENESS.DPI_AWARENESS_UNAWARE ||
                awareness == DPI_AWARENESS.DPI_AWARENESS_SYSTEM_AWARE)
            {
                uint destDpi = tile.DpiX != 0 ? tile.DpiX : GetDpiForWindow(window.Hwnd);
                if (destDpi == 0) destDpi = Defaults.StandardDpi;

                double invScale = (double)Defaults.StandardDpi / destDpi;
                outerW = Math.Max(1, (int)Math.Round(outerW * invScale));
                outerH = Math.Max(1, (int)Math.Round(outerH * invScale));
            }

            // Final size set in pixels.
            const SetWindowPosFlags SizeOnlyFlags =
                SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate | SetWindowPosFlags.ShowWindow;
            SetWindowPos(window.Hwnd, IntPtr.Zero,
                         targetX - leftMargin,
                         targetY - topMargin,
                         outerW, outerH, SizeOnlyFlags);

            // Debug information.
            Debug.WriteLine($"[Snap] awareness={awareness}, destDpi={GetDpiForWindow(window.Hwnd)}, " +
                            $"margins L{leftMargin} T{topMargin} R{rightMargin} B{bottomMargin}, " +
                            $"outerW={outerW}, outerH={outerH}");
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern DPI_AWARENESS GetAwarenessFromDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private enum DPI_AWARENESS
        {
            DPI_AWARENESS_INVALID = -1,
            DPI_AWARENESS_UNAWARE = 0,
            DPI_AWARENESS_SYSTEM_AWARE = 1,
            DPI_AWARENESS_PER_MONITOR_AWARE = 2,
        }

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
            NoSize = 0x0001,
            NoMove = 0x0002,
            NoZOrder = 0x0004,
            NoActivate = 0x0010,
            ShowWindow = 0x0040,
        }

        #endregion
    }
}

