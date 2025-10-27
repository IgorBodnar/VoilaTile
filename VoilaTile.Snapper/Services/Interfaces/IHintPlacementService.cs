namespace VoilaTile.Snapper.Services.Interfaces
{
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.Records;

    public interface IHintPlacementService
    {
        IReadOnlyList<HintPlacement> ComputePlacements(
            IReadOnlyList<WindowEntry> windows,
            IReadOnlyList<MonitorInfo> monitors);
    }
}
