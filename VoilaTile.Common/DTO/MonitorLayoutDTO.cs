namespace VoilaTile.Common.DTO
{
    public class MonitorLayoutDTO
    {
        public string MonitorID { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<TileDTO> Tiles { get; set; } = new();
    }
}
