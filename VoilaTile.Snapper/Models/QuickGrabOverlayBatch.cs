namespace VoilaTile.Snapper.Models
{
    using VoilaTile.Snapper.Records;

    public sealed class QuickGrabOverlayBatch
    {
        public string MonitorDeviceID { get; }
        public IReadOnlyList<QuickGrabBadge> Badges { get; }

        public QuickGrabOverlayBatch(string monitorDeviceID, IReadOnlyList<QuickGrabBadge> badges)
        {
            MonitorDeviceID = monitorDeviceID ?? throw new ArgumentNullException(nameof(monitorDeviceID));
            Badges = badges ?? Array.Empty<QuickGrabBadge>();
        }
    }
}
