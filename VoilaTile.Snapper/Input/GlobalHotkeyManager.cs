namespace VoilaTile.Snapper.Input
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows.Interop;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// Manages the registration and detection of a global system-wide hotkey.
    /// </summary>
    public class GlobalHotkeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        private readonly WindowInteropHelper interopHelper;
        private readonly HwndSource hwndSource;
        private readonly int hotkeyId = 9000; // Arbitrary ID

        /// <summary>
        /// Occurs when the registered hotkey is pressed.
        /// </summary>
        public event Action? HotKeyPressed;

        /// <summary>
        /// Initializes and registers the global hotkey.
        /// </summary>
        /// <param name="window">Any window (hidden or shown) for WndProc binding.</param>
        /// <param name="modifiers">The modifier keys (Alt, Ctrl, etc.).</param>
        /// <param name="key">The key to bind to.</param>
        public GlobalHotkeyManager(Window window, ModifierKeys modifiers, Key key)
        {
            interopHelper = new WindowInteropHelper(window);
            hwndSource = HwndSource.FromHwnd(interopHelper.Handle)!;
            hwndSource.AddHook(WndProc);

            int virtualKey = KeyInterop.VirtualKeyFromKey(key);
            if (!RegisterHotKey(interopHelper.Handle, hotkeyId, (uint)modifiers, (uint)virtualKey))
            {
                throw new InvalidOperationException("Failed to register global hotkey.");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            UnregisterHotKey(interopHelper.Handle, hotkeyId);
            hwndSource.RemoveHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == hotkeyId)
            {
                HotKeyPressed?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    /// <summary>
    /// Modifier keys used with RegisterHotKey.
    /// </summary>
    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0x0000,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008
    }
}

