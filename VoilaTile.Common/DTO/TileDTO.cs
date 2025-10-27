namespace VoilaTile.Common.DTO
{
    /// <summary>
    /// The DTO representing a tile.
    /// </summary>
    public class TileDTO
    {
        /// <summary>
        /// Gets or sets the hint associated with the tile.
        /// </summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the X-coordinate value.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate value.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the width of the tile.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the tile.
        /// </summary>
        public double Height { get; set; }
    }
}
