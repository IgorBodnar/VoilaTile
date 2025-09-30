namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Resolves window icons using WM_GETICON / class icon / shell icon with a small LRU cache.
    /// </summary>
    internal sealed class WindowIconService : IWindowIconService
    {
        #region Fields

        private const int MaxCache = 64;

        // Cache keyed by process name or class as fallback. In practice, exe path would be ideal, but we only stored process name in cold path.
        private readonly Dictionary<string, IconSource> cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> lru = new();

        #endregion

        #region Methods

        /// <inheritdoc/>
        public IconSource? GetIcon(WindowEntry entry)
        {
            if (!Win32.IsWindow(entry.Id.Hwnd))
            {
                return null;
            }

            // 1) Try window-provided icons.
            var hIcon = TryGetHwndIcon(entry.Id.Hwnd);
            if (hIcon != IntPtr.Zero)
            {
                var src = FromHicon(hIcon, destroy: true);
                return src is null ? null : new IconSource(src);
            }

            // 2) Fallback to shell icon by process name (approximation).
            // If you later add ExecutablePath to WindowEntry, switch to that as the cache key.
            string key = string.IsNullOrWhiteSpace(entry.ProcessName) ? entry.ClassName : entry.ProcessName;

            if (this.cache.TryGetValue(key, out var cached))
            {
                TouchLru(key);
                return cached;
            }

            var shellIcon = ShellIconForProcessName(key);
            if (shellIcon is not null)
            {
                CachePut(key, shellIcon);
                return shellIcon;
            }

            return null;
        }

        /// <inheritdoc/>
        public void ClearCache()
        {
            this.cache.Clear();
            this.lru.Clear();
        }

        #endregion

        #region Helpers - Window/Icon retrieval

        private static IntPtr TryGetHwndIcon(IntPtr hwnd)
        {
            // WM_GETICON order: small2, small, big.
            IntPtr h = Win32.SendMessage(hwnd, Win32.WM_GETICON, (IntPtr)Win32.ICON_SMALL2, IntPtr.Zero);
            if (h == IntPtr.Zero)
            {
                h = Win32.SendMessage(hwnd, Win32.WM_GETICON, (IntPtr)Win32.ICON_SMALL, IntPtr.Zero);
            }

            if (h == IntPtr.Zero)
            {
                h = Win32.SendMessage(hwnd, Win32.WM_GETICON, (IntPtr)Win32.ICON_BIG, IntPtr.Zero);
            }

            if (h == IntPtr.Zero)
            {
                // Class icons as last resort.
                h = Win32.GetClassLongPtr(hwnd, Win32.GCL_HICONSM);
                if (h == IntPtr.Zero)
                {
                    h = Win32.GetClassLongPtr(hwnd, Win32.GCL_HICON);
                }
            }

            return h;
        }

        private static ImageSource? FromHicon(IntPtr hIcon, bool destroy)
        {
            try
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (destroy && hIcon != IntPtr.Zero)
                {
                    _ = DestroyIcon(hIcon);
                }
            }
        }

        // Shell icon fallback — conservative, uses SHGetFileInfo on the exe name (not path).
        private static IconSource? ShellIconForProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return null;
            }

            // Create a dummy ".exe" filename to get the generic exe icon if shell can't resolve a real path.
            string fakePath = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName
                : processName + ".exe";

            var shfi = new SHFILEINFO();
            IntPtr hImg = SHGetFileInfo(
                fakePath,
                0,
                out shfi,
                (uint)Marshal.SizeOf<SHFILEINFO>(),
                SHGFI.SHGFI_ICON | SHGFI.SHGFI_LARGEICON | SHGFI.SHGFI_USEFILEATTRIBUTES);

            if (hImg == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var src = FromHicon(shfi.hIcon, destroy: true);
                return src is null ? null : new IconSource(src);
            }
            finally
            {
                // hImg is a handle to the image list, not needed to destroy here.
            }
        }

        private void CachePut(string key, IconSource icon)
        {
            if (this.cache.ContainsKey(key))
            {
                this.cache[key] = icon;
                TouchLru(key);
                return;
            }

            this.cache[key] = icon;
            this.lru.AddFirst(key);

            if (this.cache.Count > MaxCache)
            {
                string last = this.lru.Last!.Value;
                this.lru.RemoveLast();
                _ = this.cache.Remove(last);
            }
        }

        private void TouchLru(string key)
        {
            var node = this.lru.Find(key);
            if (node is not null)
            {
                this.lru.Remove(node);
                this.lru.AddFirst(node);
            }
        }

        #endregion

        #region Shell interop

        [Flags]
        private enum SHGFI : uint
        {
            SHGFI_ICON = 0x000000100,
            SHGFI_LARGEICON = 0x000000000,
            SHGFI_SMALLICON = 0x000000001,
            SHGFI_USEFILEATTRIBUTES = 0x000000010,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            SHGFI uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion
    }
}
