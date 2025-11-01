namespace VoilaTile.Settings.Views
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// A lightweight view-only control for displaying zone number and size.
    /// </summary>
    public partial class ZoneLabelView : UserControl
    {
        public int ZoneNumber { get; private set; }

        public ZoneLabelView(int zoneNumber)
        {
            InitializeComponent();
            this.ZoneNumber = zoneNumber;
            this.ZoneNumberText.Text = zoneNumber.ToString();

            this.SizeChanged += OnSizeChanged;
        }

        public void UpdateZoneNumber(int zoneNumber)
        {
            this.ZoneNumber = zoneNumber;
            this.ZoneNumberText.Text = zoneNumber.ToString();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var width = e.NewSize.Width;
            var height = e.NewSize.Height;

            // Optionally format to reduce decimal clutter
            this.ZoneSizeText.Text = $"({(int)width} × {(int)height})";
        }
    }
}

