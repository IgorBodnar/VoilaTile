namespace VoilaTile.Configurator.Views
{
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using System.Windows.Threading;
    using VoilaTile.Configurator.ViewModels;

    /// <summary>
    /// A view that displays a divider by injecting its visuals into external canvas layers.
    /// </summary>
    public partial class DividerView : UserControl
    {
        private readonly double canvasWidth;
        private readonly double canvasHeight;

        private Canvas linesLayer;
        private Canvas handlesLayer;

        private Line line1;
        private Line line2;
        private Path handle;

        private DividerViewModel viewModel;
        private PropertyChangedEventHandler viewModelHandler;

        public DividerViewModel ViewModel => viewModel;

        public DividerView(double canvasWidth, double canvasHeight)
        {
            InitializeComponent();
            this.canvasWidth = canvasWidth;
            this.canvasHeight = canvasHeight;
        }

        public void InitializeVisualLayers(Canvas linesLayer, Canvas handlesLayer)
        {
            this.linesLayer = linesLayer;
            this.handlesLayer = handlesLayer;
        }

        public void RequestRender(DividerViewModel newViewModel)
        {
            // Detach old handler
            if (viewModel != null && viewModelHandler != null)
            {
                viewModel.PropertyChanged -= viewModelHandler;
            }

            // Set new ViewModel
            this.DataContext = newViewModel;
            this.viewModel = newViewModel;

            // Attach new handler
            viewModelHandler = (sender, args) =>
            {
                if (args.PropertyName == nameof(DividerViewModel.IsSelected) ||
                    args.PropertyName == nameof(DividerViewModel.IsPlaceholder))
                {
                    Application.Current.Dispatcher.Invoke(RenderDivider, DispatcherPriority.Render);
                }
            };

            if (viewModel != null)
            {
                viewModel.PropertyChanged += viewModelHandler;
            }

            RenderDivider();
        }

        public void SetVisibility(Visibility visibility)
        {
            if (line1 != null) line1.Visibility = visibility;
            if (line2 != null) line2.Visibility = visibility;
            if (handle != null) handle.Visibility = visibility;
        }

        public void RenderDivider()
        {
            if (viewModel == null)
            {
                return;
            }

            double radius = this.Width / 2;
            var center = viewModel.HandleCenter;

            Brush dividerBrush = ResolveDividerBrush(viewModel);
            Brush handleBrush = ResolveHandleBrush(viewModel);

            // Remove existing visuals
            if (line1 != null) linesLayer.Children.Remove(line1);
            if (line2 != null) linesLayer.Children.Remove(line2);
            if (handle != null) handlesLayer.Children.Remove(handle);

            // Create lines
            line1 = new Line { Stroke = dividerBrush, StrokeThickness = 2, IsHitTestVisible = false };
            line2 = new Line { Stroke = dividerBrush, StrokeThickness = 2, IsHitTestVisible = false };

            double handleX = center.X * canvasWidth;
            double handleY = center.Y * canvasHeight;

            if (viewModel.IsVertical)
            {
                double x = handleX;
                double y1 = viewModel.BoundStart * canvasHeight;
                double y2 = viewModel.BoundEnd * canvasHeight;

                line1.X1 = x; line1.X2 = x;
                line1.Y1 = y1; line1.Y2 = handleY - radius;

                line2.X1 = x; line2.X2 = x;
                line2.Y1 = handleY + radius; line2.Y2 = y2;
            }
            else
            {
                double y = handleY;
                double x1 = viewModel.BoundStart * canvasWidth;
                double x2 = viewModel.BoundEnd * canvasWidth;

                line1.Y1 = y; line1.Y2 = y;
                line1.X1 = x1; line1.X2 = handleX - radius;

                line2.Y1 = y; line2.Y2 = y;
                line2.X1 = handleX + radius; line2.X2 = x2;
            }

            linesLayer.Children.Add(line1);
            linesLayer.Children.Add(line2);

            // Get geometry
            var geometry = (GeometryGroup)this.Resources[viewModel.IsVertical
                ? "HorizontalArrowGeometry"
                : "VerticalArrowGeometry"];

            // Create handle
            handle = new Path
            {
                Width = 36,
                Height = 36,
                Stretch = Stretch.Fill,
                Fill = handleBrush,
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Data = geometry
            };

            Canvas.SetLeft(handle, handleX - radius);
            Canvas.SetTop(handle, handleY - radius);
            handle.DataContext = viewModel;

            if (viewModel.IsPlaceholder)
            {
                handle.IsHitTestVisible = false;
                handle.Stroke = new SolidColorBrush(Colors.White) { Opacity = 0.5 };
            }
            else
            {
                handle.MouseEnter += OnMouseEnter;
                handle.MouseLeave += OnMouseLeave;
                handle.MouseLeftButtonDown += OnMouseLeftButtonDown;
            }

            handlesLayer.Children.Add(handle);
        }

        private Brush ResolveDividerBrush(DividerViewModel vm)
        {
            if (vm.IsSelected)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e5383b"));
            }

            if (vm.IsPlaceholder)
            {
                return new SolidColorBrush(Colors.White) { Opacity = 0.5 };
            }

            return new SolidColorBrush(Colors.White);
        }

        private Brush ResolveHandleBrush(DividerViewModel vm)
        {
            if (vm.IsSelected)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e5383b"));
            }

            if (vm.IsPlaceholder)
            {
                return new SolidColorBrush(Colors.Gray) { Opacity = 0.5 };
            }

            return new SolidColorBrush(Colors.Gray);
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            viewModel?.RaiseMouseEnter();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                viewModel?.RaiseMouseLeave();
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            viewModel?.RaiseSelected();
        }
    }
}

