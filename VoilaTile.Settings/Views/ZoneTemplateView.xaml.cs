namespace VoilaTile.Settings.Views
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for Page1.xaml.
    /// </summary>
    public partial class ZoneTemplateView : UserControl
    {
        public ZoneTemplateView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateScale();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScale();
        }

        private void UpdateScale()
        {
            if (ActualWidth == 0 || ActualHeight == 0)
                return;

            var parent = Parent as FrameworkElement;
            if (parent == null || parent.ActualWidth == 0 || parent.ActualHeight == 0)
                return;

            // Calculate scale to fit within parent while preserving aspect ratio
            double scaleX = parent.ActualWidth;
            double scaleY = parent.ActualHeight;

            double minScale = Math.Min(scaleX, scaleY); // Use whichever fits

            CanvasScaleTransform.ScaleX = scaleX;
            CanvasScaleTransform.ScaleY = scaleY;
        }
    }
}
