namespace VoilaTile.Snapper.Records
{
    /// <summary>
    /// A record representing the placement of a hint on a monitor.
    /// </summary>
    /// <param name="Id">The id of a window at which the hint is pointing.</param>
    /// <param name="MonitorDeviceID">The id of the monitor at which the hint needs to be placed.</param>
    /// <param name="Xpx">The x position of the hint in device pixels.</param>
    /// <param name="Ypx">The y position of the hint in device pixels.</param>
    /// <param name="Z">The z order of the hint.</param>
    public sealed record HintPlacement(
        WindowId Id,
        string MonitorDeviceID,
        int Xpx,
        int Ypx,
        int Z);
}
