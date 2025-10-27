namespace VoilaTile.Common.Records
{
    /// <summary>
    /// Represents information about a display device as reported by the system.
    /// </summary>
    /// <param name="DeviceName">The name of the display device.</param>
    /// <param name="DeviceString">A human-readable description of the device.</param>
    /// <param name="DeviceID">The unique device identifier string.</param>
    public record MonitorDeviceInfo(string DeviceName, string DeviceString, string DeviceID);
}

