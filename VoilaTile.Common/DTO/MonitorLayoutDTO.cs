namespace VoilaTile.Common.DTO
{
    using VoilaTile.Common.Helpers;

    public class MonitorLayoutDTO
    {
        public string MonitorID { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double DpiX { get; set; } = Defaults.StandardDpi;
        public double DpiY { get; set; } = Defaults.StandardDpi;
        public List<TileDTO> Tiles { get; set; } = new();
    }
}
