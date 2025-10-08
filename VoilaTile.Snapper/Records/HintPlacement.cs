namespace VoilaTile.Snapper.Records
{
    public sealed record HintPlacement(
        WindowId Id,
        string MonitorDeviceID,
        int Xpx,
        int Ypx,
        int Z);
}
