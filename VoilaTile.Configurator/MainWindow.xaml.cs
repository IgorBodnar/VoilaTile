namespace VoilaTile.Configurator
{
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Interop;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Helpers;
    using VoilaTile.Configurator.ViewModels;

    /// <summary>
    /// The main window of the application.
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isManuallyMaximized = false;
        private Rectangle restoreBounds;
        private MainViewModel viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow(MainViewModel viewModel)
        {
            this.InitializeComponent();
            this.viewModel = viewModel;
            this.DataContext = this.viewModel;

            this.viewModel.RequestTemplateLibraryScroll += this.OnRequestTemplateLibraryScroll;
        }

        private void OnRequestTemplateLibraryScroll()
        {
            this.TemplatePreviewsScrollViewer.ScrollToEnd();
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                this.DragMove();
            }
        }

        private void OnClickMinimize(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void OnClickMaximize(object sender, RoutedEventArgs e)
        {
            if (this.isManuallyMaximized)
            {
                // Restore previous size and position
                this.WindowState = WindowState.Normal;
                WindowExtensions.SetWindowBounds(this, this.restoreBounds);

                this.isManuallyMaximized = false;
            }
            else
            {
                // Save current position and size before maximizing
                this.restoreBounds = new Rectangle((int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height);

                var screen = Screen.FromHandle(new WindowInteropHelper(this).Handle);
                var workingArea = screen.WorkingArea;

                this.WindowState = WindowState.Normal;
                WindowExtensions.SetWindowBounds(this, workingArea);

                this.isManuallyMaximized = true;
            }
        }

        private async void Close_Click(object sender, RoutedEventArgs e)
        {
            // Check if the user actually wants to exit the configurator.
            var confirmExitDialog = new ConfirmationDialogViewModel("Confirm Exit", "Are you sure you have finished setting up your layouts?\nOn exit the layouts will be persisted.");
            var (result, _) = await App.DialogService.ShowAsync(confirmExitDialog);

            // Ask the view model to check if the snapper is running.
            await this.viewModel.CheckSnapperRunningAsync();

            if (result == DialogDecision.Negative)
            {
                return;
            }

            OverlayManager.HideOverlay();
            this.Close();
        }
    }
}
