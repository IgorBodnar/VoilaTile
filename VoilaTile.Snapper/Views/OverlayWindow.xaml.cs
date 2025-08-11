namespace VoilaTile.Snapper.Views
{
    using System.Windows;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// Code-behind for a single monitor's transparent overlay.
    /// </summary>
    public partial class OverlayWindow : Window
    {
        public OverlayViewModel ViewModel { get; }

        public OverlayWindow(OverlayViewModel viewModel)
        {
            InitializeComponent();
            this.ViewModel = viewModel;
            this.DataContext = viewModel;
        }
    }
}

