namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using VoilaTile.Common.Helpers;
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

                window.SourceInitialized += (_, __) =>
                {
                    // Use WPF's current scale (what WPF is actually using) for the inverse layout.
                    ApplyInverseDpiLayout(window);

                    // On DPI change, let WPF update first, then fix layout and bounds.
                    HookDpiChanged(window, () =>
                    {
                        window.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ApplyInverseDpiLayout(window);
                            ApplyBoundsPx(window, pxX, pxY, pxW, pxH);
                        }), DispatcherPriority.Send);
                    });

                    // Enforce exact pixel bounds once the HWND exists.
                    ApplyBoundsPx(window, pxX, pxY, pxW, pxH);
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
        /// Apply inverse DPI layout so pixel-based values render at correct size
        /// using the DPI WPF is actually using for this window.
        /// </summary>
        private void ApplyInverseDpiLayout(Window window)
        {
            var src = (HwndSource?)PresentationSource.FromVisual(window);
            double scale = 1.0;

            if (src?.CompositionTarget is HwndTarget target)
            {
                // WPF's authoritative per-window scale
                scale = target.TransformToDevice.M11;
            }
            else
            {
                // Fallback if Source not ready yet
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int dpi = GetDpiForWindow(hwnd);
                    if (dpi > 0) scale = dpi / Defaults.StandardDpi;
                }
            }

            if (scale <= 0) scale = 1.0;
            double inv = 1.0 / scale;

            if (window.Content is FrameworkElement root)
            {
                var st = root.LayoutTransform as ScaleTransform;
                if (st is null)
                {
                    root.LayoutTransform = new ScaleTransform(inv, inv);
                }
                else
                {
                    st.ScaleX = inv;
                    st.ScaleY = inv;
                }
            }
        }

        /// <summary>
        /// Hooks up WM_DPICHANGED while setting it as handled to remove UI jitter.
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

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);
    }
}

