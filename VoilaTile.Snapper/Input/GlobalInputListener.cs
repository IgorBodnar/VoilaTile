namespace VoilaTile.Snapper.Input
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Windows.Input;
    using System.Windows.Media.Effects;
    using VoilaTile.Snapper.Services;

    /// <summary>
    /// Listens for low-level keyboard input and routes characters during input mode.
    /// </summary>
    public class GlobalInputListener : IDisposable
    {
        private readonly InputStateManager stateManager;
        private readonly SettingsMonitoringService settings;
        private IntPtr hookId = IntPtr.Zero;
        private readonly LowLevelKeyboardProc hookCallback;

        private bool isWinDown = false;

        private bool isShiftDown = false;

        /// <summary>
        /// Occurs when a character key is typed.
        /// </summary>
        public event Action<char>? OnCharacterTyped;

        /// <summary>
        /// Occurs when the Enter key is pressed.
        /// </summary>
        public event Action? OnEnterPressed;

        /// <summary>
        /// Occurs when the Backspace key is pressed.
        /// </summary>
        public event Action? OnBackspacePressed;

        /// <summary>
        /// Occurs when the Escape key is pressed.
        /// </summary>
        public event Action? OnEscapePressed;

        /// <summary>
        /// Occurs when the Space key is pressed.
        /// </summary>
        public event Action? OnSpacePressed;

        /// <summary>
        /// Occurs when the custom hotkey (Win + Space) is pressed.
        /// </summary>
        public event Action? OnManualHotKeyPressed;

        /// <summary>
        /// Initializes and installs the global input listener.
        /// </summary>
        public GlobalInputListener(InputStateManager stateManager, SettingsMonitoringService settings)
        {
            this.settings = settings;
            this.stateManager = stateManager;
            this.hookCallback = HookProc;
            this.hookId = SetHook(this.hookCallback);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                GetModuleHandle(curModule!.ModuleName), 0);
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0)
                return CallNextHookEx(hookId, nCode, wParam, lParam);

            int vkCode = Marshal.ReadInt32(lParam);
            Key key = KeyInterop.KeyFromVirtualKey(vkCode);

            bool shouldSuppress = false;

            if (wParam == (IntPtr)WM_KEYDOWN)
            {
                if (key == Key.LWin || key == Key.RWin)
                {
                    isWinDown = true;
                }
                else if (key == Key.LeftShift || key == Key.RightShift)
                {
                    isShiftDown = true;
                }
                else if (key == this.settings.ShortcutKey && isWinDown && isShiftDown && stateManager.CurrentMode == InputMode.HotKey)
                {
                    OnManualHotKeyPressed?.Invoke();
                    shouldSuppress = true;
                }
                else if (stateManager.CurrentMode == InputMode.Input)
                {
                    switch (key)
                    {
                        case Key.Escape:
                            OnEscapePressed?.Invoke();
                            shouldSuppress = true;
                            break;
                        case Key.Enter:
                            OnEnterPressed?.Invoke();
                            shouldSuppress = true;
                            break;
                        case Key.Space:
                            OnSpacePressed?.Invoke();
                            shouldSuppress = true;
                            break;
                        case Key.Back:
                            OnBackspacePressed?.Invoke();
                            shouldSuppress = true;
                            break;
                        default:
                            char ch = ToChar(vkCode);
                            if (!char.IsControl(ch))
                            {
                                OnCharacterTyped?.Invoke(ch);
                                shouldSuppress = true;
                            }
                            break;
                    }
                }
            }
            else if (wParam == (IntPtr)WM_KEYUP)
            {
                if (key == Key.LWin || key == Key.RWin)
                {
                    isWinDown = false;
                }
                else if (key == Key.LeftShift || key == Key.RightShift)
                {
                    isShiftDown = false;
                }
            }

            return shouldSuppress ? (IntPtr)1 : CallNextHookEx(hookId, nCode, wParam, lParam);
        }


        private static char ToChar(int vkCode)
        {
            byte[] keyboardState = new byte[256];
            GetKeyboardState(keyboardState);

            uint scanCode = MapVirtualKey((uint)vkCode, 0);
            var sb = new System.Text.StringBuilder(2);
            int result = ToUnicode((uint)vkCode, scanCode, keyboardState, sb, sb.Capacity, 0);

            return result > 0 ? sb[0] : '\0';
        }

        #region Win32

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
            IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern int GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
            System.Text.StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion
    }
}

