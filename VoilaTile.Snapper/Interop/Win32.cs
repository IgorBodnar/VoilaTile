// File: Interop/Win32.cs
// -------------------------------------------------------------------------------------
// <copyright file="Win32.cs" company="VoilaTile">
// Copyright © VoilaTile.
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.Interop
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// User32 and related interop needed for window enumeration and metadata.
    /// </summary>
    internal static class Win32
    {
        #region Delegates

        internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region Constants

        internal const int GWL_EXSTYLE = -20;
        internal const int GW_OWNER = 4;
        internal const int GA_ROOT = 2;

        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_APPWINDOW = 0x00040000;

        internal const int WM_GETICON = 0x007F;
        internal const int ICON_SMALL = 0;
        internal const int ICON_BIG = 1;
        internal const int ICON_SMALL2 = 2;

        internal const int GCL_HICON = -14;
        internal const int GCL_HICONSM = -34;

        internal const int SW_RESTORE = 9;

        internal const int GW_HWNDFIRST = 0;
        internal const int GW_HWNDNEXT  = 2;

        #endregion

        #region User32 P/Invokes

        [DllImport("user32.dll")]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hWnd, int gaFlags);

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll",
            SetLastError = true,
            ExactSpelling = true,
            EntryPoint = "GetWindowInfo")]
        private static extern bool GetWindowInfoNative(IntPtr hwnd, ref WINDOWINFO pwi);



        #endregion

        #region DWM P/Invokes

        internal const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        internal const int DWMWA_CLOAKED = 14;

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            out RECT pvAttribute,
            int cbAttribute);

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            out uint pvAttribute,
            int cbAttribute);

        #endregion

        #region Kernel32 / Psapi

        [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(int access, bool inherit, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr handle);

        internal const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        internal const int PROCESS_VM_READ = 0x0010;

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public bool IsEmpty => this.right <= this.left || this.bottom <= this.top;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWINFO
        {
            public uint cbSize;
            public RECT rcWindow;
            public RECT rcClient;
            public uint dwStyle;
            public uint dwExStyle;
            public uint dwWindowStatus;
            public uint cxWindowBorders;
            public uint cyWindowBorders;
            public ushort atomWindowType;
            public ushort wCreatorVersion;
        }

        #endregion

        #region Helpers

        internal static string GetWindowTextSafe(IntPtr hWnd)
        {
            int len = GetWindowTextLength(hWnd);
            if (len <= 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(len + 1);
            _ = GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        internal static bool GetWindowInfo(IntPtr hwnd, out WINDOWINFO info)
        {
            info = new WINDOWINFO
            {
                cbSize = (uint)Marshal.SizeOf<WINDOWINFO>()
            };

            return GetWindowInfoNative(hwnd, ref info);
        }


        internal static string GetClassNameSafe(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            _ = GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        internal static bool TryGetExtendedFrameBounds(IntPtr hWnd, out RECT rect)
        {
            int hr = DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());
            return hr >= 0 && !rect.IsEmpty;
        }

        internal static bool IsCloaked(IntPtr hWnd)
        {
            int hr = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out uint cloaked, sizeof(uint));
            return hr >= 0 && cloaked != 0;
        }

        internal static string? TryGetProcessPath(uint pid)
        {
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                // MainModule can fail for protected/system processes — fall back below.
                return proc.MainModule?.FileName;
            }
            catch
            {
                // ignore and try psapi
            }

            IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, false, pid);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var sb = new StringBuilder(1024);
                uint len = GetModuleFileNameEx(handle, IntPtr.Zero, sb, sb.Capacity);
                return len > 0 ? sb.ToString() : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                _ = CloseHandle(handle);
            }
        }

        #endregion
    }
}
