namespace VoilaTile.Settings.Views
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Shell;

    /// <summary>
    /// Main settings shell window with custom chrome and corrected maximize bounds.
    /// </summary>
    public partial class ShellView : Window
    {
        #region Fields

        /// <summary>
        /// Windows message for retrieving/overriding min/max tracking size and position.
        /// </summary>
        private const int WM_GETMINMAXINFO = 0x0024;

        /// <summary>
        /// Flag for <see cref="MonitorFromWindow(IntPtr, int)"/> to get the nearest monitor.
        /// </summary>
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ShellView"/> class.
        /// </summary>
        public ShellView()
        {
            this.InitializeComponent();

            // Hook native messages (once we have an HWND) and keep chrome visuals consistent.
            this.SourceInitialized += this.OnSourceInitialized;
            this.StateChanged += this.OnStateChangedAdjustChromeForMaximize;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Installs a window procedure hook to correct maximize bounds for borderless windows.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(this.WndProc);
            }

            // Ensure initial chrome visuals are correct for the starting state.
            this.ApplyChromeForState(this.WindowState);
        }

        /// <summary>
        /// Updates chrome padding and corner radius when the window state changes.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Event args.</param>
        private void OnStateChangedAdjustChromeForMaximize(object? sender, EventArgs e)
        {
            this.ApplyChromeForState(this.WindowState);
        }

        /// <summary>
        /// Handles left-button drag and double-click maximize/restore on the custom title bar.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Mouse button event args.</param>
        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // Double-click: toggle maximize/restore
                    if (this.WindowState == WindowState.Maximized)
                    {
                        SystemCommands.RestoreWindow(this);
                    }
                    else
                    {
                        SystemCommands.MaximizeWindow(this);
                    }
                }
                else
                {
                    // Drag
                    this.DragMove();
                }
            }
        }

        /// <summary>
        /// Shows the system menu on right-click within the title bar area.
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Mouse button event args.</param>
        private void OnTitleBarRightClick(object sender, MouseButtonEventArgs e)
        {
            var mousePos = this.PointToScreen(e.GetPosition(this));
            SystemCommands.ShowSystemMenu(this, mousePos);
        }

        /// <summary>
        /// Minimizes the window (command binding).
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Executed routed event args.</param>
        private void OnMinimize(object sender, ExecutedRoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        /// <summary>
        /// Toggles maximize/restore (command binding).
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Executed routed event args.</param>
        private void OnMaximizeRestore(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
            }
            else
            {
                SystemCommands.MaximizeWindow(this);
            }
        }

        /// <summary>
        /// Closes the window (command binding).
        /// </summary>
        /// <param name="sender">Event source.</param>
        /// <param name="e">Executed routed event args.</param>
        private void OnClose(object sender, ExecutedRoutedEventArgs e) => SystemCommands.CloseWindow(this);

        /// <summary>
        /// Window procedure hook to adjust maximized size and position for borderless windows.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="msg">Message id.</param>
        /// <param name="wParam">wParam.</param>
        /// <param name="lParam">lParam.</param>
        /// <param name="handled">Set to true if handled.</param>
        /// <returns>Result pointer.</returns>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                this.AdjustMaximizedSizeAndPosition(hwnd, lParam);
                // do not mark as handled to allow default processing
            }
            return IntPtr.Zero;
        }


        /// <summary>
        /// Applies chrome visuals (padding, corner radius, resize border) for the given window state.
        /// Removes interior padding and rounding while maximized to avoid visual spill.
        /// </summary>
        /// <param name="state">The current window state.</param>
        private void ApplyChromeForState(WindowState state)
        {
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome is null)
            {
                return;
            }

            if (state == WindowState.Maximized)
            {
                // Flatten visuals while maximized.
                chrome.CornerRadius = new CornerRadius(0);
                chrome.ResizeBorderThickness = new Thickness(0);
                this.Padding = new Thickness(0);
            }
            else
            {
                // Restore visuals for normal state.
                chrome.CornerRadius = new CornerRadius(6);
                chrome.ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness;
                this.Padding = SystemParameters.WindowResizeBorderThickness;
            }
        }

        /// <summary>
        /// Forces the maximized bounds to the current monitor's work area,
        /// preventing the 1–8 DIP spill that occurs with borderless windows across DPI scales.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="lParam">Pointer to <see cref="MINMAXINFO"/>.</param>
        private void AdjustMaximizedSizeAndPosition(IntPtr hwnd, IntPtr lParam)
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero) return;

            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref mi)) return;

            // Max bounds = work area, fixes spill when maximized.
            var rcWork = mi.rcWork;
            var rcMonitor = mi.rcMonitor;

            mmi.ptMaxPosition.X = Math.Abs(rcWork.Left - rcMonitor.Left);
            mmi.ptMaxPosition.Y = Math.Abs(rcWork.Top - rcMonitor.Top);
            mmi.ptMaxSize.X = Math.Abs(rcWork.Right - rcWork.Left);
            mmi.ptMaxSize.Y = Math.Abs(rcWork.Bottom - rcWork.Top);

            double scaleX = 1.0, scaleY = 1.0;
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                scaleX = dpi.DpiScaleX;
                scaleY = dpi.DpiScaleY;
            }
            catch { /* fall back to 1.0 */ }

            int minWpx = (int)Math.Ceiling(this.MinWidth * scaleX);
            int minHpx = (int)Math.Ceiling(this.MinHeight * scaleY);

            if (minWpx > 0)
                mmi.ptMinTrackSize.X = Math.Max(mmi.ptMinTrackSize.X, minWpx);
            if (minHpx > 0)
                mmi.ptMinTrackSize.Y = Math.Max(mmi.ptMinTrackSize.Y, minHpx);

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        #endregion Methods

        #region Win32

        /// <summary>
        /// Native point structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Native min/max info structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        /// <summary>
        /// Native rectangle (device pixels).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Monitor info containing full and work areas.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        /// <summary>
        /// Gets the monitor handle nearest to the specified window.
        /// </summary>
        /// <param name="hwnd">Window handle.</param>
        /// <param name="dwFlags">Selection flags.</param>
        /// <returns>Monitor handle.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        /// <summary>
        /// Retrieves information about a display monitor.
        /// </summary>
        /// <param name="hMonitor">Monitor handle.</param>
        /// <param name="lpmi">Monitor info structure to fill.</param>
        /// <returns>True on success; otherwise false.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        #endregion Win32
    }
}


