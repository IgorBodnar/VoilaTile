namespace VoilaTile.Snapper.Helpers
{
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services;

    public static class HintAssignment
    {
        public static IReadOnlyList<PlacedHintWithText> AttachHints(
            IReadOnlyList<HintPlacement> placements,
            IHintService hintService)
        {
            var ordered = placements
                .OrderBy(p => p.Z)
                .ThenBy(p => p.MonitorDeviceID, StringComparer.Ordinal)
                .ThenBy(p => p.Xpx)
                .ThenBy(p => p.Ypx)
                .ToList();

            var hints = hintService.AssignHints(ordered.Count);

            var result = new List<PlacedHintWithText>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                result.Add(new PlacedHintWithText(
                    p.Id,
                    p.MonitorDeviceID,
                    p.Xpx,
                    p.Ypx,
                    p.Z,
                    hints[i]));
            }

            return result;
        }
    }
}
