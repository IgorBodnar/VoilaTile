namespace VoilaTile.Common.Records
{
    /// <summary>
    /// Represents the dots-per-inch (DPI) information for a specific monitor.
    /// </summary>
    /// <param name="DeviceName">The name of the display device.</param>
    /// <param name="DpiX">The horizontal DPI value.</param>
    /// <param name="DpiY">The vertical DPI value.</param>
    public record MonitorDpiInfo(string DeviceName, uint DpiX, uint DpiY);
}

