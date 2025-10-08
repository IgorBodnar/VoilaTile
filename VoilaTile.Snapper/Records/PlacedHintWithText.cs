namespace VoilaTile.Snapper.Records
{
    public sealed record PlacedHintWithText(
    WindowId Id,
    string MonitorDeviceID,
    int Xpx,
    int Ypx,
    int Z,
    string HintText);
}
