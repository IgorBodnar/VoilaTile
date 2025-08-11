namespace VoilaTile.Snapper.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Snapper.Layout;

    /// <summary>
    /// View model representing a single tile and its visual state.
    /// </summary>
    public partial class TileViewModel : ObservableObject
    {
        private readonly ResolvedTileModel model;

        /// <summary>
        /// Initializes a new instance of the <see cref="TileViewModel"/> class.
        /// </summary>
        /// <param name="model">The resolved tile model to wrap.</param>
        public TileViewModel(ResolvedTileModel model)
        {
            this.model = model;
        }

        /// <summary>
        /// Gets the X coordinate of the tile.
        /// </summary>
        public double X => model.X;

        /// <summary>
        /// Gets the Y coordinate of the tile.
        /// </summary>
        public double Y => model.Y;

        /// <summary>
        /// Gets the width of the tile.
        /// </summary>
        public double Width => model.Width;

        /// <summary>
        /// Gets the height of the tile.
        /// </summary>
        public double Height => model.Height;

        /// <summary>
        /// Gets the hint label for this tile.
        /// </summary>
        public string Hint => model.Hint;

        /// <summary>
        /// Gets the X coordinate of the tile center.
        /// </summary>
        public double CenterX => X + Width / 2;

        /// <summary>
        /// Gets the Y coordinate of the tile center.
        /// </summary>
        public double CenterY => Y + Height / 2;

        /// <summary>
        /// Gets or sets a value indicating whether this tile is currently highlighted.
        /// </summary>
        [ObservableProperty]
        private bool isHighlighted;

        /// <summary>
        /// Gets or sets a value indicating whether this tile is currently matched.
        /// </summary>
        [ObservableProperty]
        private bool isMatched;
    }
}

