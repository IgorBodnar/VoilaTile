namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.ViewModels;
    using VoilaTile.Snapper.Views;

    /// <summary>
    /// Manages creation and visibility of all monitor overlay windows.
    /// Positions/sizes overlays in physical pixels to be DPI-proof.
    /// </summary>
    public sealed class OverlayDisplayService
    {
        private readonly Dictionary<string, OverlayWindow> overlays = new();

        /// <summary>
        /// Shows overlays for the specified monitor layouts.
        /// </summary>
        /// <param name="overlayViewModels">The resolved overlays per monitor.</param>
        public void ShowOverlays(List<OverlayViewModel> overlayViewModels)
        {
            foreach (var vm in overlayViewModels)
            {
                // Target rectangle in physical pixels (virtual-screen coordinates).
                int pxX = (int)vm.Layout.Monitor.WorkX;
                int pxY = (int)vm.Layout.Monitor.WorkY;
                int pxW = (int)vm.Layout.Monitor.WorkWidth;
                int pxH = (int)vm.Layout.Monitor.WorkHeight;

                var window = new OverlayWindow(vm)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Width = 1,
                    Height = 1,
                    Left = 0,
                    Top = 0,
                    ShowActivated = false,
                    Topmost = true,
                    ResizeMode = ResizeMode.NoResize,
                };

                // After HWND exists, enforce pixel-perfect bounds and lock across DPI changes.
                window.SourceInitialized += (_, __) =>
                {
                    this.HookDpiChanged(window, () => this.ApplyBoundsPx(window, pxX, pxY, pxW, pxH));
                    this.ApplyBoundsPx(window, pxX, pxY, pxW, pxH);
                };

                window.Show();

                this.overlays[vm.Layout.Monitor.DeviceID] = window;
            }
        }

        /// <summary>
        /// Hides and clears all overlay windows.
        /// </summary>
        public void HideOverlays()
        {
            foreach (var window in this.overlays.Values)
            {
                try
                {
                    window.Hide();
                    window.Close();
                }
                catch
                {
                }
            }

            this.overlays.Clear();
        }

        /// <summary>
        /// Applies a window rectangle in physical pixels via Win32.
        /// </summary>
        private void ApplyBoundsPx(Window window, int x, int y, int width, int height)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;

            SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Hooks WM_DPICHANGED to ignore Windows suggested rect and re-apply our pixel bounds.
        /// </summary>
        private void HookDpiChanged(Window window, Action onDpiChanged)
        {
            var src = (HwndSource?)PresentationSource.FromVisual(window);
            if (src == null)
            {
                return;
            }

            src.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                const int WM_DPICHANGED = 0x02E0;
                if (msg == WM_DPICHANGED)
                {
                    onDpiChanged();
                    handled = true;
                }

                return IntPtr.Zero;
            });
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);
    }
}


