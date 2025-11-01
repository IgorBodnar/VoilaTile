namespace VoilaTile.Settings.Models
{
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Models;

    /// <summary>
    /// A tile model used for layout persistence.
    /// </summary>
    /// <param name="X">The x coordinate of the tile.</param>
    /// <param name="Y">The y coordicate of the tile.</param>
    /// <param name="Width">The width of the tile.</param>
    /// <param name="Height">The height of the tile.</param>
    /// <param name="Ancestors">The hash set of tile ancestor guids.</param>
    public record Tile(double X, double Y, double Width, double Height, HashSet<Guid> Ancestors)
    {
        /// <summary>
        /// The tile ancestor set key.
        /// </summary>
        public TileAncestorSetKey Key => new(Ancestors);

        /// <summary>
        /// Gets or sets the hint associated with the tile.
        /// </summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>
        /// Attempts to merge two tiles into one.
        /// </summary>
        /// <param name="a">The first tile.</param>
        /// <param name="b">The second tile.</param>
        /// <param name="dividers">The list of divider models occluding a tile.</param>
        /// <param name="merged">The merged tile.</param>
        /// <returns>A value indicating whether the merging succeded.</returns>
        public static bool TryMerge(Tile a, Tile b, List<DividerModel> dividers, out Tile merged)
        {
            merged = default!;
            if (a.Ancestors.Overlaps(b.Ancestors))
                return false;

            var combinedAncestors = a.Ancestors.Union(b.Ancestors).ToHashSet();

            if (a.Y == b.Y && a.Height == b.Height &&
                (a.X + a.Width == b.X || b.X + b.Width == a.X))
            {
                double x = Math.Min(a.X, b.X);
                double width = a.Width + b.Width;
                merged = new Tile(x, a.Y, width, a.Height, combinedAncestors);
                return true;
            }

            if (a.X == b.X && a.Width == b.Width &&
                (a.Y + a.Height == b.Y || b.Y + b.Height == a.Y))
            {
                double y = Math.Min(a.Y, b.Y);
                double height = a.Height + b.Height;
                merged = new Tile(a.X, y, a.Width, height, combinedAncestors);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Convers a tile model to a tile DTO.
        /// </summary>
        /// <returns>A tile DTO.</returns>
        public TileDTO ToDTO()
        {
            return new TileDTO()
            {
                Hint = this.Hint,
                X = this.X,
                Y = this.Y,
                Width = this.Width,
                Height = this.Height,
            };
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"X = {this.X}, Y = {this.Y}, Width = {this.Width}, Height = {this.Height}, Hint = {this.Hint}";
        }
    }
}
