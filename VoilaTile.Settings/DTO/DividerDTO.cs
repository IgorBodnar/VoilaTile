namespace VoilaTile.Settings.DTO
{
    using VoilaTile.Settings.Models;

    /// <summary>
    /// The DTO representing a divider between different zones defining the tiles.
    /// </summary>
    public class DividerDTO
    {
        /// <summary>
        /// Gets or sets a value indicating whether the divider is vertical.
        /// </summary>
        public bool IsVertical { get; set; }

        /// <summary>
        /// The divider position.
        /// </summary>
        public double Position { get; set; }

        /// <summary>
        /// The divider bound start.
        /// </summary>
        public double BoundStart { get; set; }

        /// <summary>
        /// The divider bound end.
        /// </summary>
        public double BoundEnd { get; set; }

        /// <summary>
        /// Converts the divider DTO into a <see cref="DividerModel"/>.
        /// </summary>
        /// <returns>The divider model initialized from the DTO.</returns>
        public DividerModel ToModel()
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
