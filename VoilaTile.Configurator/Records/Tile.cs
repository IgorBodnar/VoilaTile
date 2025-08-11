namespace VoilaTile.Configurator.Records
{
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Models;
    using VoilaTile.Configurator.Models;

    public record Tile(double X, double Y, double Width, double Height, HashSet<Guid> Ancestors)
    {
        public TileAncestorSetKey Key => new(Ancestors);

        public string Hint { get; set; }

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

        public TileDTO ToDTO(MonitorInfo monitor)
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

        public override string ToString()
        {
            return $"X = {this.X}, Y = {this.Y}, Width = {this.Width}, Height = {this.Height}, Hint = {this.Hint}";
        }
    }
}
