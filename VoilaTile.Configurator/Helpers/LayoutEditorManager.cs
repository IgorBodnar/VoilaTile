namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using VoilaTile.Common.Models;
    using VoilaTile.Configurator.Models;
    using VoilaTile.Configurator.ViewModels;
    using VoilaTile.Configurator.Views;

    public static class LayoutEditorManager
    {
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
        }

        private static void ApplyBoundsPx(Window window, int x, int y, int width, int height)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;
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

