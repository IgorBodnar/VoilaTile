using VoilaTile.Configurator.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace VoilaTile.Configurator.Views
{
    /// <summary>
    /// Interaction logic for ZoneTemplatePreviewView.xaml
    /// </summary>
    public partial class ZoneTemplatePreviewView : System.Windows.Controls.UserControl
    {
        private const double MaxPreviewWidth = 250;
        private const double MaxPreviewHeight = 250;

        public ZoneTemplatePreviewView()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ZoneTemplatePreviewViewModel vm)
            {
                vm.RaiseClicked();
            }
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
    }
}

