namespace VoilaTile.Snapper.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows.Media;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// View model for a single window card (Power Mode).
    /// </summary>
    internal sealed class WindowCardViewModel : ObservableObject
    {
        private bool isMatch = true;
        private bool isSelected;
        private double thumbHeight = 50; // Start with a small thumb height, layout logic will adjust it to optimize layout.
        private double thumbWidth = 180;

        public WindowCardViewModel(WindowEntry entry, string hint)
        {
            this.Entry = entry;
            this.Hint = hint.ToUpperInvariant();
            this.Icon = entry.AppIcon?.Source;
            this.Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.ProcessName : entry.Title;
            this.Subtitle = entry.ProcessName;
        }

        public WindowEntry Entry { get; }

        public string Hint { get; }

        public string Title { get; }

        public string Subtitle { get; }

        public ImageSource? Icon { get; }

        public bool IsMatch
        {
            get => this.isMatch;
            set
            {
                if (value == this.isMatch) return;
                this.isMatch = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => this.isSelected;
            set
            {
                if (value == this.isSelected) return;
                this.isSelected = value;
                this.OnPropertyChanged();
            }
        }

        public double ThumbHeight
        {
            get => thumbHeight;
            set { if (value == thumbHeight) return; thumbHeight = value; OnPropertyChanged(); }
        }

        public double ThumbWidth
        {
            get => thumbWidth;
            private set { if (Math.Abs(value - thumbWidth) < 0.5) return; thumbWidth = value; OnPropertyChanged(); }
        }

        // Call this when we learn the source size (from DWM)
        public void SetSourceSize(SizePx src)
        {
            if (src.W <= 0 || src.H <= 0) return;
            double w = ThumbHeight * (double)src.W / src.H;

            // Optional clamp so headers never look silly; tune as you like
            const double minW = 160, maxW = 420;
            if (w < minW) w = minW;
            if (w > maxW) w = maxW;

            ThumbWidth = w;
        }
    }
}
