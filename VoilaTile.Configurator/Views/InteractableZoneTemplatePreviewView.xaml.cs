namespace VoilaTile.Configurator.Views
{
    using VoilaTile.Configurator.ViewModels;
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for ZoneTemplatePreviewView.xaml.
    /// </summary>
    public partial class InteractableZoneTemplatePreviewView : UserControl
    {
        private const double MaxPreviewWidth = 250;
        private const double MaxPreviewHeight = 250;

        public InteractableZoneTemplatePreviewView()
        {
            InitializeComponent();

            EventManager.RegisterClassHandler(
                typeof(InteractableZoneTemplatePreviewView),
                Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OnMouseDownOutsideCapturedElement));

            SizeChanged += OnSizeChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnMouseDownOutsideCapturedElement(object sender, MouseButtonEventArgs e)
        {
            if (OverlayContainer.Visibility == Visibility.Visible)
            {
                OverlayContainer.Visibility = Visibility.Collapsed;
                Mouse.Capture(null);
            }
        }

        private void ToggleOverlay()
        {
            if (OverlayContainer.Visibility == Visibility.Visible)
            {
                OverlayContainer.Visibility = Visibility.Collapsed;
                OverlayContainer.IsHitTestVisible = false;
                Mouse.Capture(null);
            }
            else
            {
                OverlayContainer.Visibility = Visibility.Visible;
                OverlayContainer.IsHitTestVisible = true;
                Mouse.Capture(this, CaptureMode.SubTree);
            }
        }

        private void HideOverlay()
        {
            OverlayContainer.Visibility = Visibility.Collapsed;
            OverlayContainer.IsHitTestVisible = false;
        }

        private void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (this.DataContext is not ZoneTemplatePreviewViewModel vm)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                vm.RaiseDoubleClicked();
                return;
            }

            vm.RaiseClicked();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AdjustPreviewSize();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustPreviewSize();
        }

        private void AdjustPreviewSize()
        {
            if (DataContext is not ZoneTemplatePreviewViewModel vm || vm.AspectRatio <= 0)
                return;

            double aspectRatio = vm.AspectRatio;

            double width, height;

            if (aspectRatio >= 1.0)
            {
                // Landscape: constrain width
                width = MaxPreviewWidth;
                height = width / aspectRatio;
            }
            else
            {
                // Portrait: constrain height
                height = MaxPreviewHeight;
                width = height * aspectRatio;
            }

            previewBorder.Width = width;
            previewBorder.Height = height;
        }

        private void OnMenuButtonClick(object sender, RoutedEventArgs e)
        {
            this.ToggleOverlay();
        }

        private void OnMenuItemClick(object sender, RoutedEventArgs e)
        {
            this.HideOverlay();
        }
    }
}
