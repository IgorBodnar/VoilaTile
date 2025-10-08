namespace VoilaTile.Snapper.Views
{
    using System.Windows;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// Code-behind for a single monitor's transparent overlay.
    /// </summary>
    public partial class SnapOverlayWindow : Window
    {
        public SnapOverlayViewModel ViewModel { get; }

        public SnapOverlayWindow(SnapOverlayViewModel viewModel)
        {
            InitializeComponent();
            this.ViewModel = viewModel;
            this.DataContext = viewModel;
        }
    }
}

