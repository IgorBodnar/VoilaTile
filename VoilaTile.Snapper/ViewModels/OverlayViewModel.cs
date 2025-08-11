namespace VoilaTile.Snapper.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Snapper.Layout;

    /// <summary>
    /// View model for a single monitor overlay, including its tiles and input.
    /// </summary>
    public partial class OverlayViewModel : ObservableObject
    {
        private readonly ZoneLayoutModel layout;
        private readonly Action<ResolvedTileModel?> matchReportedCallback;

        /// <summary>
        /// Gets the collection of tiles on this overlay.
        /// </summary>
        public ObservableCollection<TileViewModel> Tiles { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OverlayViewModel"/> class.
        /// </summary>
        /// <param name="layout">The resolved layout for a single monitor.</param>
        /// <param name="reportMatch">A callback to report best match to coordinator.</param>
        public OverlayViewModel(ZoneLayoutModel layout, Action<ResolvedTileModel?> reportMatch)
        {
            this.layout = layout;
            this.matchReportedCallback = reportMatch;
            this.Tiles = new ObservableCollection<TileViewModel>(
                layout.Tiles.Select(t => new TileViewModel(t)));
        }

        public ZoneLayoutModel Layout => this.layout;

        /// <summary>
        /// Gets or sets the current typed input (e.g. "a", "sd", etc.).
        /// </summary>
        [ObservableProperty]
        private string currentInput = string.Empty;

        /// <summary>
        /// Handles a new character input from the global input listener.
        /// </summary>
        public void AppendCharacter(char c)
        {
            CurrentInput += c;
            EvaluateMatches();
        }

        /// <summary>
        /// Handles a backspace key press.
        /// </summary>
        public void Backspace()
        {
            if (CurrentInput.Length <= 1)
            {
                ClearInput();
                return;
            }

            CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
            EvaluateMatches();
        }

        /// <summary>
        /// Clears the current input and match highlights.
        /// </summary>
        public void ClearInput()
        {
            CurrentInput = string.Empty;
            foreach (var tile in Tiles)
            {
                tile.IsHighlighted = false;
                tile.IsMatched = false;
            }

            matchReportedCallback(null);
        }

        /// <summary>
        /// Evaluates current input and updates tile highlighting.
        /// </summary>
        private void EvaluateMatches()
        {
            ResolvedTileModel? bestMatch = null;
            ResolvedTileModel? firstPrefixMatch = null;

            foreach (var tile in Tiles)
            {
                bool isPrefixMatch = tile.Hint.StartsWith(CurrentInput, StringComparison.OrdinalIgnoreCase);
                bool isExactMatch = tile.Hint.Equals(CurrentInput, StringComparison.OrdinalIgnoreCase);

                tile.IsHighlighted = isPrefixMatch;
                tile.IsMatched = isExactMatch;

                if (isExactMatch)
                {
                    bestMatch = layout.Tiles.First(t => t.Hint == tile.Hint);
                }
                else if (isPrefixMatch && firstPrefixMatch == null)
                {
                    firstPrefixMatch = layout.Tiles.First(t => t.Hint == tile.Hint);
                }
            }

            if (bestMatch != null)
            {
                matchReportedCallback(bestMatch);
                return;
            }

            if (firstPrefixMatch != null)
            {
                matchReportedCallback(firstPrefixMatch);
            }
        }
    }
}

