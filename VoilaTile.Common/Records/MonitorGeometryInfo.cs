namespace VoilaTile.Common.Records
{
    using static VoilaTile.Common.Interop.MonitorInteropStructs;

    /// <summary>
    /// Represents monitor geometry information including device name, monitor bounds, work area, and handle.
    /// </summary>
    /// <param name="DeviceName">The name of the display device.</param>
    /// <param name="MonitorBounds">The full bounds of the monitor in device-independent pixels.</param>
    /// <param name="WorkBounds">The work area bounds excluding taskbar and docked elements.</param>
    /// <param name="HMonitor">The handle to the monitor.</param>
    public record MonitorGeometryInfo(string DeviceName, Rect MonitorBounds, Rect WorkBounds, IntPtr HMonitor);
}

