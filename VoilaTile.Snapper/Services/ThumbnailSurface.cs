namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;
    using static VoilaTile.Snapper.Interop.DwmInterop;

    /// <summary>Manages multiple DWM thumbnails bound to one destination HWND.</summary>
    internal sealed class ThumbnailSurface : IThumbnailSurface, IDisposable
    {
        private readonly IntPtr destHwnd;
        private readonly Dictionary<IntPtr, HTHUMBNAIL> map = new();

        public ThumbnailSurface(IntPtr destinationHwnd)
        {
            if (destinationHwnd == IntPtr.Zero)
                throw new ArgumentException("Invalid HWND", nameof(destinationHwnd));

            this.destHwnd = destinationHwnd;
        }

        public void EnsureRegistered(WindowId sourceId)
        {
            if (sourceId.Hwnd == IntPtr.Zero) return;
            if (sourceId.Hwnd == this.destHwnd) return; // never register self
            if (map.ContainsKey(sourceId.Hwnd)) return;

            // Optional: skip non-top-level or invisible sources (defensive)
            if (!Win32.IsWindow(sourceId.Hwnd) || !Win32.IsWindowVisible(sourceId.Hwnd))
                return;

            int hr = DwmRegisterThumbnail(this.destHwnd, sourceId.Hwnd, out var handle);
            if (hr == unchecked((int)0x80070057)) // E_INVALIDARG – common for forbidden sources
                return;

            DwmInterop.CheckHr(hr);
            map[sourceId.Hwnd] = handle;
        }

        /// <summary>
        /// Attempts to query the source (client) size of a registered thumbnail.
        /// Returns false if the window is not registered or DWM can't provide a size.
        /// </summary>
        public bool TryGetSourceSize(WindowId sourceId, out SizePx size)
        {
            size = default;

            if (!map.TryGetValue(sourceId.Hwnd, out var thumb))
                return false;

            var hr = DwmQueryThumbnailSourceSize(thumb, out var s);
            if (hr < 0 || s.cx <= 0 || s.cy <= 0)
                return false;

            size = new SizePx(s.cx, s.cy);
            return true;
        }

        public void UpdateRect(WindowId sourceId, RectPx destinationPx, byte opacity = 255)
        {
            if (!map.TryGetValue(sourceId.Hwnd, out var thumb)) return;

            var props = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = DwmTnpFlags.RectDestination |
                          DwmTnpFlags.Visible |
                          DwmTnpFlags.Opacity |
                          DwmTnpFlags.SourceClientAreaOnly,
                rcDestination = new RECT
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

            CheckHr(DwmUpdateThumbnailProperties(thumb, ref props));
        }

        public void Remove(WindowId sourceId)
        {
            if (map.Remove(sourceId.Hwnd, out var thumb))
            {
                _ = DwmUnregisterThumbnail(thumb);
            }
        }

        public void Dispose()
        {
            foreach (var t in map.Values)
                _ = DwmUnregisterThumbnail(t);

            map.Clear();
        }
    }

    internal sealed class DwmThumbnailSurfaceFactory : IDwmThumbnailSurfaceFactory
    {
        public IThumbnailSurface Create(IntPtr destinationHwnd) => new ThumbnailSurface(destinationHwnd);
    }
}

