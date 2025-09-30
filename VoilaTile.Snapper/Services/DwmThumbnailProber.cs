using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoilaTile.Snapper.Services
{
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services.Interfaces;

    /// <summary>
    /// Cheap probe: tries DwmRegisterThumbnail against a host hwnd and
    /// immediately unregisters. Results are cached briefly.
    /// </summary>
    internal sealed class DwmThumbnailProber : IDwmThumbnailProber, IDisposable
    {
        private readonly Func<IntPtr> hostHwndProvider;
        private readonly Dictionary<IntPtr, (bool ok, long ticks)> cache = new();
        private readonly long ttlTicks;

        public DwmThumbnailProber(Func<IntPtr> hostHwndProvider, TimeSpan? ttl = null)
        {
            this.hostHwndProvider = hostHwndProvider ?? throw new ArgumentNullException(nameof(hostHwndProvider));
            this.ttlTicks = (ttl ?? TimeSpan.FromSeconds(5)).Ticks;
        }

        public bool CanRegister(WindowId source)
        {
            var host = hostHwndProvider();
            if (host == IntPtr.Zero || source.Hwnd == IntPtr.Zero) return false;

            var now = DateTime.UtcNow.Ticks;
            if (cache.TryGetValue(source.Hwnd, out var hit) && (now - hit.ticks) < ttlTicks)
                return hit.ok;

            DwmInterop.HTHUMBNAIL thumb;
            int hr = DwmInterop.DwmRegisterThumbnail(host, source.Hwnd, out thumb);

            bool ok = hr >= 0 && thumb.Value != IntPtr.Zero;
            if (ok)
            {
                // Best-effort unwrap; ignore errors here.
                try { _ = DwmInterop.DwmUnregisterThumbnail(thumb); } catch { }
            }

            cache[source.Hwnd] = (ok, now);
            return ok;
        }

        public void Dispose() => cache.Clear();
    }
}
