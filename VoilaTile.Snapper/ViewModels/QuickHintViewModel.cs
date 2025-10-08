namespace VoilaTile.Snapper.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// A single hint badge to render on an overlay canvas.
    /// </summary>
    public sealed partial class QuickHintViewModel : ObservableObject
    {
        public QuickHintViewModel(WindowId id, string hintText, double xdip, double ydip, int z)
        {
            Id = id;
            HintText = hintText ?? string.Empty;
            Xdip = xdip;
            Ydip = ydip;
            Z = z;
            isVisible = true;     // default visible
            isSelected = false;   // default not selected
        }

        /// <summary>Target window id.</summary>
        public WindowId Id { get; }

        /// <summary>Hint string (e.g., "a", "s", "ad").</summary>
        public string HintText { get; }

        /// <summary>Canvas coordinates in device-independent pixels.</summary>
        public double Xdip { get; }
        public double Ydip { get; }

        /// <summary>Z ordering (larger means nearer top).</summary>
        public int Z { get; }

        [ObservableProperty]
        private bool isVisible;

        /// <summary>Whether this hint is the unique match (selected) for visual feedback/glow.</summary>
        [ObservableProperty]
        private bool isSelected;
    }
}

