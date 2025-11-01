namespace VoilaTile.Settings.Services
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using VoilaTile.Common.Models;
    using VoilaTile.Settings.Models;
    using VoilaTile.Settings.ViewModels;
    using VoilaTile.Settings.Views;

    /// <summary>
    /// The manager responsible for opening and handling the layout editor window.
    /// </summary>
    public static class LayoutEditorManager
    {
        /// <summary>
        /// Opens the layout editor window for the specified zone template on the given monitor.
        /// </summary>
        /// <param name="template">The zone template.</param>
        /// <param name="monitorInfo">The monitor info.</param>
        /// <param name="onCloseCallback">The callback to be performed on closing of the editor.</param>
        public static void OpenEditor(
            ZoneTemplate template,
            MonitorInfo monitorInfo,
            Action<LayoutEditorViewModel> onCloseCallback)
        {
            var vm = new LayoutEditorViewModel(template);

            // Target rectangle in physical pixes: monitor work area
            int pxX = monitorInfo.WorkX;
            int pxY = monitorInfo.WorkY;
            int pxW = monitorInfo.WorkWidth;
            int pxH = monitorInfo.WorkHeight;

            var editor = new LayoutEditorWindow(vm)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Width = 1,
                Height = 1,
                Left = 0,
                Top = 0,
                ShowActivated = true,
                Topmost = false,
            };

            var mainWindow = Application.Current.MainWindow;
            mainWindow.Hide();

            // After HWND exists, enforce pixel-perfect bounds and lock them across DPI changes.
            editor.SourceInitialized += (_, __) =>
            {
                HookDpiChanged(editor, () => ApplyBoundsPx(editor, pxX, pxY, pxW, pxH));
                ApplyBoundsPx(editor, pxX, pxY, pxW, pxH);
            };

            editor.Closed += (_, __) =>
            {
                mainWindow.Show();
                onCloseCallback(vm);
            };

            editor.Show();
            editor.Initialize();
        }

        /// <summary>
        /// Applies a window rectangle in physical pixels via Win32.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <param name="x">New x position.</param>
        /// <param name="y">New y position.</param>
        /// <param name="width">New width.</param>
        /// <param name="height">New height.</param>
        /// <remarks>
        /// Preferred over WPF sizing methods to avoid DPI scaling issues and flicker due to order of operations.
        /// </remarks>
        private static void ApplyBoundsPx(Window window, int x, int y, int width, int height)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
            SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Hooks into the window's message loop to listen for DPI change messages.
        /// </summary>
        /// <param name="window">The window.</param>
        /// <param name="onDpiChanged">The callback to be performed on dpi changes.</param>
        private static void HookDpiChanged(Window window, Action onDpiChanged)
        {
            var src = (HwndSource?)PresentationSource.FromVisual(window);
            if (src == null) return;

            src.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                const int WM_DPICHANGED = 0x02E0;
                if (msg == WM_DPICHANGED)
                {
                    // Ignore Windows suggested rect; re-apply our exact pixel bounds.
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

