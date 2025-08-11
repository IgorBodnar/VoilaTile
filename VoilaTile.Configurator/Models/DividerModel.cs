using VoilaTile.Configurator.DTO;

namespace VoilaTile.Configurator.Models
{
    /// <summary>
    /// Core data model representing a divider with orientation and bounds.
    /// </summary>
    public class DividerModel : ICloneable
    {
        public bool IsVertical { get; set; }

        /// <summary>
        /// Normalized position of the divider (X if vertical, Y if horizontal).
        /// </summary>
        public double Position { get; set; }

        /// <summary>
        /// Start bound along the perpendicular axis (Top for vertical, Left for horizontal).
        /// </summary>
        public double BoundStart { get; set; }

        /// <summary>
        /// End bound along the perpendicular axis (Bottom for vertical, Right for horizontal).
        /// </summary>
        public double BoundEnd { get; set; }

        public DividerDTO ToDTO()
        {
            return new DividerDTO
            {
                IsVertical = this.IsVertical,
                Position = this.Position,
                BoundStart = this.BoundStart,
                BoundEnd = this.BoundEnd,
            };
        }

        public object Clone()
        {
            return new DividerModel()
            {
                IsVertical = this.IsVertical,
                Position = this.Position,
                BoundStart = this.BoundStart,
                BoundEnd = this.BoundEnd,
            };
        }
    }
}
