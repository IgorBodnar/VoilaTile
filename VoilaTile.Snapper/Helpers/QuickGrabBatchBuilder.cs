namespace VoilaTile.Snapper.Helpers
{
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.Models;
    using VoilaTile.Snapper.Records;

    public static class QuickGrabBatchBuilder
    {
        public static IReadOnlyList<QuickGrabOverlayBatch> BuildBatches(
            IReadOnlyList<PlacedHintWithText> placedWithHints,
            IReadOnlyList<MonitorInfo> monitors)
        {
            var monIndex = monitors.ToDictionary(m => m.DeviceID, m => m, StringComparer.Ordinal);

            var groups = placedWithHints
                .GroupBy(p => p.MonitorDeviceID, StringComparer.Ordinal)
                .Select(g =>
                {
                    if (!monIndex.TryGetValue(g.Key, out var mon))
                    {
                        // Fallback: treat as 1.0 scale, origin 0/0
                        double dx = 1.0, dy = 1.0;
                        var badgesFallback = g
                            .OrderBy(x => x.Z).ThenBy(x => x.Xpx).ThenBy(x => x.Ypx)
                            .Select(x => new QuickGrabBadge(
                                x.Id,
                                x.HintText,
                                Xdip: x.Xpx / dx,
                                Ydip: x.Ypx / dy,
                                x.Z))
                            .ToList();
                        return new QuickGrabOverlayBatch(g.Key, badgesFallback);
                    }

                    // Translate from absolute screen px -> monitor-local px
                    int originX = mon.WorkX;
                    int originY = mon.WorkY;

                    // Then px -> DIP using that monitor’s DPI
                    double dxScale = mon.DpiX / 96.0;
                    double dyScale = mon.DpiY / 96.0;

                    var badges = g
                        .OrderBy(x => x.Z).ThenBy(x => x.Xpx).ThenBy(x => x.Ypx)
                        .Select(x =>
                        {
                            int localPxX = x.Xpx - originX;
                            int localPxY = x.Ypx - originY;

                            // Optional: clamp to canvas
                            // localPxX = Math.Max(0, Math.Min(localPxX, mon.WorkWidth));
                            // localPxY = Math.Max(0, Math.Min(localPxY, mon.WorkHeight));

                            return new QuickGrabBadge(
                                x.Id,
                                x.HintText,
                                Xdip: localPxX / dxScale,
                                Ydip: localPxY / dyScale,
                                x.Z);
                        })
                        .ToList();

                    return new QuickGrabOverlayBatch(g.Key, badges);
                })
                .ToList();

            return groups;
        }

    }
}
