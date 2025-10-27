namespace VoilaTile.Snapper.ViewModels
{
    using System;
    using System.Windows.Media;
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// View model for a single window card (Power Mode).
    /// </summary>
    internal sealed class WindowCardViewModel : ObservableObject
    {
        #region Fields

        /// <summary>
        /// Backing field for <see cref="IsMatch"/>.
        /// </summary>
        private bool isMatch = true;

        /// <summary>
        /// Backing field for <see cref="IsSelected"/>.
        /// </summary>
        private bool isSelected;

        /// <summary>
        /// Backing field for <see cref="ThumbHeight"/> (in device-independent pixels).
        /// Initialized to a small default; the overlay will update this per row height.
        /// </summary>
        private double thumbHeight = 50;

        /// <summary>
        /// Backing field for <see cref="ThumbWidth"/> (in device-independent pixels).
        /// </summary>
        private double thumbWidth = 180;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowCardViewModel"/> class.
        /// </summary>
        /// <param name="entry">The underlying window entry.</param>
        /// <param name="hint">The keyboard hint string.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entry"/> is <c>null</c>.</exception>
        public WindowCardViewModel(WindowEntry entry, string hint)
        {
            this.Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            this.Hint = hint.ToUpperInvariant();
            this.Icon = entry.AppIcon?.Source;
            this.Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.ProcessName : entry.Title;
            this.Subtitle = entry.ProcessName;

            if (this.Entry.IsToolWindow)
            {
                this.TypeLabel = "Tool window";
            }
            else if (this.Entry.OwnerHwnd != IntPtr.Zero)
            {
                this.TypeLabel = "Owned window";
            }
            else
            {
                this.TypeLabel = "Top-level window";
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the underlying window entry.
        /// </summary>
        public WindowEntry Entry { get; }

        /// <summary>
        /// Gets the keyboard hint.
        /// </summary>
        public string Hint { get; }

        /// <summary>
        /// Gets the title.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the subtitle (usually process name).
        /// </summary>
        public string Subtitle { get; }

        /// <summary>
        /// Gets the application icon.
        /// </summary>
        public ImageSource? Icon { get; }

        /// <summary>
        /// Gets a short label describing the window type.
        /// Returns "Tool window", "Owned window", or "Top-level window".
        /// </summary>
        public string TypeLabel { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this card matches the current filter.
        /// </summary>
        public bool IsMatch
        {
            get => this.isMatch;
            set
            {
                if (value == this.isMatch)
                {
                    return;
                }

                this.isMatch = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this card is currently selected.
        /// </summary>
        public bool IsSelected
        {
            get => this.isSelected;
            set
            {
                if (value == this.isSelected)
                {
                    return;
                }

                this.isSelected = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the thumbnail height in device-independent pixels.
        /// </summary>
        public double ThumbHeight
        {
            get => this.thumbHeight;
            set
            {
                if (Math.Abs(value - this.thumbHeight) < 0.1)
                {
                    return;
                }

                this.thumbHeight = value;
                this.OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the thumbnail width in device-independent pixels.
        /// </summary>
        public double ThumbWidth
        {
            get => this.thumbWidth;
            private set
            {
                if (Math.Abs(value - this.thumbWidth) < 0.5)
                {
                    return;
                }

                this.thumbWidth = value;
                this.OnPropertyChanged();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the source size (from DWM). Width is derived from the shared row height
        /// to preserve aspect ratio tightly.
        /// </summary>
        /// <param name="src">The source window size in pixels.</param>
        public void SetSourceSize(SizePx src)
        {
            if (src.W <= 0 || src.H <= 0)
            {
                return;
            }

            double w = this.ThumbHeight * (double)src.W / src.H;

            const double MinW = 120;
            const double MaxW = 900;

            this.ThumbWidth = Math.Clamp(w, MinW, MaxW);
        }

        #endregion
    }
}

