namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Manages multiple DWM thumbnails that are all rendered into a single destination HWND.
    /// Responsible for registration, updates, and cleanup of thumbnail handles.
    /// </summary>
    internal sealed class ThumbnailSurface : IThumbnailSurface, IDisposable
    {
        #region Fields

        /// <summary>
        /// Destination window handle that hosts all registered DWM thumbnails.
        /// </summary>
        private readonly IntPtr destHwnd;

        /// <summary>
        /// Map from source HWND to the corresponding registered thumbnail handle.
        /// </summary>
        private readonly Dictionary<IntPtr, DwmInterop.HTHUMBNAIL> map = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThumbnailSurface"/> class.
        /// </summary>
        /// <param name="destinationHwnd">The destination HWND where thumbnails will be drawn.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="destinationHwnd"/> is <see cref="IntPtr.Zero"/>.</exception>
        public ThumbnailSurface(IntPtr destinationHwnd)
        {
            if (destinationHwnd == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid HWND", nameof(destinationHwnd));
            }

            this.destHwnd = destinationHwnd;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Ensures a thumbnail for <paramref name="sourceId"/> is registered against this surface's destination HWND.
        /// This method is idempotent and will early-return if already registered.
        /// </summary>
        /// <param name="sourceId">The source window identifier to register.</param>
        public void EnsureRegistered(WindowId sourceId)
        {
            if (sourceId.Hwnd == IntPtr.Zero) return;
            if (sourceId.Hwnd == this.destHwnd) return; // never register self
            if (this.map.ContainsKey(sourceId.Hwnd)) return;

            // Optional: skip non-top-level or invisible sources (defensive)
            if (!Win32.IsWindow(sourceId.Hwnd) || !Win32.IsWindowVisible(sourceId.Hwnd))
            {
                return;
            }

            // External static call: qualify with type name.
            int hr = DwmInterop.DwmRegisterThumbnail(this.destHwnd, sourceId.Hwnd, out var handle);

            // E_INVALIDARG – common for forbidden sources; ignore quietly.
            if (hr == unchecked((int)0x80070057))
            {
                return;
            }

            DwmInterop.CheckHr(hr);
            this.map[sourceId.Hwnd] = handle;
        }

        /// <summary>
        /// Attempts to query the source client size for a registered thumbnail.
        /// </summary>
        /// <param name="sourceId">The source window identifier.</param>
        /// <param name="size">Receives the source client size (in pixels) on success.</param>
        /// <returns>
        /// <see langword="true"/> if the size was obtained; <see langword="false"/> if the window is
        /// not registered or DWM did not return a valid size.
        /// </returns>
        public bool TryGetSourceSize(WindowId sourceId, out SizePx size)
        {
            size = default;

            if (!this.map.TryGetValue(sourceId.Hwnd, out var thumb))
            {
                return false;
            }

            var hr = DwmInterop.DwmQueryThumbnailSourceSize(thumb, out var s);
            if (hr < 0 || s.cx <= 0 || s.cy <= 0)
            {
                return false;
            }

            size = new SizePx(s.cx, s.cy);
            return true;
        }

        /// <summary>
        /// Updates the destination rectangle and opacity for the specified source thumbnail.
        /// </summary>
        /// <param name="sourceId">The source window identifier.</param>
        /// <param name="destinationPx">Destination rectangle in surface pixel coordinates.</param>
        /// <param name="opacity">Per-thumbnail opacity (0–255). Defaults to 255 (opaque).</param>
        public void UpdateRect(WindowId sourceId, RectPx destinationPx, byte opacity = 255)
        {
            if (!this.map.TryGetValue(sourceId.Hwnd, out var thumb)) return;

            var props = new DwmInterop.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DwmInterop.DwmTnpFlags.RectDestination |
                          DwmInterop.DwmTnpFlags.Visible |
                          DwmInterop.DwmTnpFlags.Opacity |
                          DwmInterop.DwmTnpFlags.SourceClientAreaOnly,
                rcDestination = new DwmInterop.RECT
                {
                    left = destinationPx.X,
                    top = destinationPx.Y,
                    right = destinationPx.X + destinationPx.W,
                    bottom = destinationPx.Y + destinationPx.H,
                },
                opacity = opacity,
                fVisible = true,
                fSourceClientAreaOnly = true,
            };

            DwmInterop.CheckHr(DwmInterop.DwmUpdateThumbnailProperties(thumb, ref props));
        }

        /// <summary>
        /// Unregisters (removes) the thumbnail for the specified source, if present.
        /// </summary>
        /// <param name="sourceId">The source window identifier.</param>
        public void Remove(WindowId sourceId)
        {
            if (this.map.Remove(sourceId.Hwnd, out var thumb))
            {
                _ = DwmInterop.DwmUnregisterThumbnail(thumb);
            }
        }

        /// <summary>
        /// Releases all registered thumbnails and clears the internal map.
        /// </summary>
        public void Dispose()
        {
            foreach (var t in this.map.Values)
            {
                _ = DwmInterop.DwmUnregisterThumbnail(t);
            }

            this.map.Clear();
        }

        #endregion
    }

    /// <summary>
    /// Factory for creating <see cref="IThumbnailSurface"/> instances backed by DWM thumbnails.
    /// </summary>
    internal sealed class DwmThumbnailSurfaceFactory : IDwmThumbnailSurfaceFactory
    {
        #region Methods

        /// <summary>
        /// Creates a new <see cref="IThumbnailSurface"/> bound to the specified destination HWND.
        /// </summary>
        /// <param name="destinationHwnd">The destination window handle.</param>
        /// <returns>A new thumbnail surface instance.</returns>
        public IThumbnailSurface Create(IntPtr destinationHwnd) => new ThumbnailSurface(destinationHwnd);

        #endregion
    }
}

