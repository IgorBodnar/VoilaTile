namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
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

        /// <summary>
        /// Maximum number of entries to keep in the in-memory icon cache.
        /// </summary>
        private const int MaxCache = 64;

        /// <summary>
        /// Cache of icons keyed by process name (or class name fallback). Case-insensitive.
        /// </summary>
        private readonly Dictionary<string, IconSource> cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// LRU list tracking cache access order (most-recent at the front).
        /// </summary>
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
            IntPtr hIcon = TryGetHwndIcon(entry.Id.Hwnd);
            if (hIcon != IntPtr.Zero)
            {
                ImageSource? src = FromHicon(hIcon, destroy: true);
                return src is null ? null : new IconSource(src);
            }

            // 2) Fallback to shell icon by process name (approximation).
            // If you later add ExecutablePath to WindowEntry, switch to that as the cache key.
            string key = string.IsNullOrWhiteSpace(entry.ProcessName) ? entry.ClassName : entry.ProcessName;

            if (this.cache.TryGetValue(key, out IconSource? cached))
            {
                this.TouchLru(key);
                return cached;
            }

            IconSource? shellIcon = ShellIconForProcessName(key);
            if (shellIcon is not null)
            {
                this.CachePut(key, shellIcon);
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

        /// <summary>
        /// Attempts to obtain an HICON for a given HWND using WM_GETICON and class icon fallbacks.
        /// </summary>
        /// <param name="hwnd">Target window handle.</param>
        /// <returns>An icon handle if available; otherwise, <see cref="IntPtr.Zero"/>.</returns>
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

        /// <summary>
        /// Creates a frozen <see cref="ImageSource"/> from an HICON and optionally destroys the icon handle.
        /// </summary>
        /// <param name="hIcon">The icon handle.</param>
        /// <param name="destroy">Whether to destroy the icon handle after use.</param>
        /// <returns>The created image source, or <see langword="null"/> on failure.</returns>
        private static ImageSource? FromHicon(IntPtr hIcon, bool destroy)
        {
            try
            {
                ImageSource bs = Imaging.CreateBitmapSourceFromHIcon(
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

        /// <summary>
        /// Gets a shell-derived icon for a process name by querying the shell for a generic executable icon.
        /// </summary>
        /// <param name="processName">Process name (without path).</param>
        /// <returns>An <see cref="IconSource"/> if resolved; otherwise, <see langword="null"/>.</returns>
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

            SHFILEINFO shfi;
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
                ImageSource? src = FromHicon(shfi.hIcon, destroy: true);
                return src is null ? null : new IconSource(src);
            }
            finally
            {
                // hImg is a handle to the image list (no need to destroy here).
            }
        }

        /// <summary>
        /// Inserts or updates a cache entry and updates the LRU ordering (evicting if necessary).
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="icon">Icon to cache.</param>
        private void CachePut(string key, IconSource icon)
        {
            if (this.cache.ContainsKey(key))
            {
                this.cache[key] = icon;
                this.TouchLru(key);
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

        /// <summary>
        /// Marks a cache key as most-recently-used in the LRU list.
        /// </summary>
        /// <param name="key">Cache key to touch.</param>
        private void TouchLru(string key)
        {
            LinkedListNode<string>? node = this.lru.Find(key);
            if (node is not null)
            {
                this.lru.Remove(node);
                this.lru.AddFirst(node);
            }
        }

        #endregion

        #region Interop

        /// <summary>
        /// Flags for <see cref="SHGetFileInfo(string, uint, out SHFILEINFO, uint, SHGFI)"/>.
        /// </summary>
        [Flags]
        private enum SHGFI : uint
        {
            /// <summary>
            /// Retrieve the handle to the icon that represents the file.
            /// </summary>
            SHGFI_ICON = 0x000000100,

            /// <summary>
            /// Retrieve the large icon.
            /// </summary>
            SHGFI_LARGEICON = 0x000000000,

            /// <summary>
            /// Retrieve the small icon.
            /// </summary>
            SHGFI_SMALLICON = 0x000000001,

            /// <summary>
            /// Indicate that the file attributes are specified in <c>dwFileAttributes</c>.
            /// </summary>
            SHGFI_USEFILEATTRIBUTES = 0x000000010,
        }

        /// <summary>
        /// Receives file information from <see cref="SHGetFileInfo(string, uint, out SHFILEINFO, uint, SHGFI)"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            /// <summary>
            /// Handle to the icon that represents the file.
            /// </summary>
            public IntPtr hIcon;

            /// <summary>
            /// Index of the icon image within the system image list.
            /// </summary>
            public int iIcon;

            /// <summary>
            /// File attributes.
            /// </summary>
            public uint dwAttributes;

            /// <summary>
            /// Display name string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            /// <summary>
            /// Type name string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        /// <summary>
        /// Retrieves information about an object in the file system (including its icon).
        /// </summary>
        /// <param name="pszPath">Path to the file.</param>
        /// <param name="dwFileAttributes">File attributes if <see cref="SHGFI.SHGFI_USEFILEATTRIBUTES"/> is set.</param>
        /// <param name="psfi">Receives the file info.</param>
        /// <param name="cbFileInfo">Size of the <see cref="SHFILEINFO"/> structure.</param>
        /// <param name="uFlags">Flags specifying the information to retrieve.</param>
        /// <returns>Handle to the system image list; <see cref="IntPtr.Zero"/> on failure.</returns>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            out SHFILEINFO psfi,
            uint cbFileInfo,
            SHGFI uFlags);

        /// <summary>
        /// Destroys an icon and frees any associated memory.
        /// </summary>
        /// <param name="hIcon">Handle to the icon to be destroyed.</param>
        /// <returns><see langword="true"/> on success; otherwise, <see langword="false"/>.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion
    }
}

