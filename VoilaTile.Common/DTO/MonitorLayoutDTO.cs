namespace VoilaTile.Common.DTO
{
    using VoilaTile.Common.Helpers;

    /// <summary>
    /// A DTO representing the layout of a monitor and its tiles.
    /// </summary>
    public class MonitorLayoutDTO
    {
        /// <summary>
        /// Gets or sets the unique identifier for the monitor.
        /// </summary>
        public string MonitorID { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the X-coordinate value in device pixels.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate value in device pixels.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the width of the object in device pixels.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the object in device pixels.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Gets or sets the horizontal dots per inch (DPI) setting for the display.
        /// </summary>
        public double DpiX { get; set; } = Defaults.StandardDpi;

        /// <summary>
        /// Gets or sets the vertical dots per inch (DPI) setting for the display.
        /// </summary>
        public double DpiY { get; set; } = Defaults.StandardDpi;

        /// <summary>
        /// Gets or sets the collection of tiles.
        /// </summary>
        public List<TileDTO> Tiles { get; set; } = new();
    }
}
