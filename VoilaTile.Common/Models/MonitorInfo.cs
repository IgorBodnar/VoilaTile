namespace VoilaTile.Common.Models
{
    public class MonitorInfo
    {
        public int MonitorNumber { get; set; }

        public string DeviceName { get; set; } = string.Empty;
        public string DeviceID { get; set; } = string.Empty;
        public string DeviceString { get; set; } = string.Empty;

        public string EdidManufacturer { get; set; } = string.Empty;
        public string EdidProductCode { get; set; } = string.Empty;
        public string EdidSerial { get; set; } = string.Empty;

        public int MonitorX { get; set; }
        public int MonitorY { get; set; }
        public int MonitorWidth { get; set; }
        public int MonitorHeight { get; set; }

        public int WorkX { get; set; }
        public int WorkY { get; set; }
        public int WorkWidth { get; set; }
        public int WorkHeight { get; set; }

        public uint DpiX { get; set; }
        public uint DpiY { get; set; }

        public string MonitorBounds => $"({MonitorWidth}x{MonitorHeight}) at ({MonitorX},{MonitorY})";
        public string WorkBounds => $"({WorkWidth}x{WorkHeight}) at ({WorkX},{WorkY})";
    }
}