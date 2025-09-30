namespace VoilaTile.Snapper.Interop
{
    using System;
    using System.Windows;
    using System.Windows.Interop;

    /// <summary>
    /// Extensions for WPF interop helpers.
    /// </summary>
    internal static class WpfExtensions
    {
        /// <summary>
        /// Gets the HWND for this <see cref="Window"/>, or <see cref="IntPtr.Zero"/> if not available.
        /// </summary>
        /// <param name="window">The WPF window.</param>
        /// <returns>The native handle or <see cref="IntPtr.Zero"/>.</returns>
        internal static IntPtr GetHandleOrZero(this Window window)
        {
            if (window is null)
            {
                return IntPtr.Zero;
            }

            try
            {
                var helper = new WindowInteropHelper(window);
                return helper.Handle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }
}

