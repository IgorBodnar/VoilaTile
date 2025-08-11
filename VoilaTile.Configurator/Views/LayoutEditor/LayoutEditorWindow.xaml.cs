namespace VoilaTile.Configurator.Views
{
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Records;
    using VoilaTile.Configurator.ViewModels;

    /// <summary>
    /// Interaction logic for LayoutEditorWindow.xaml.
    /// </summary>
    public partial class LayoutEditorWindow : Window
    {
        private readonly LayoutEditorViewModel viewModel;
        private readonly List<ZoneLabelView> zoneLabelPool = new();
        private readonly List<DividerView> dividerViewPool = new();
        private DividerView? placeholderDivider;
        private EditorFloatingCard? card;

        public LayoutEditorWindow(LayoutEditorViewModel vm)
        {
            InitializeComponent();
            this.DataContext = vm;
            this.viewModel = vm;

            this.viewModel.Dividers.CollectionChanged += OnCollectionChanged;
            this.viewModel.UpdateDividers += () => this.RenderLayout();
            this.viewModel.CleanupPlaceholderDivider += () => this.CleanupPlaceholder();

            this.viewModel.UpdateZoneLabels += this.UpdateZoneLabels;
            this.viewModel.UpdateGridLayout += this.UpdateGridLayout;

            this.MouseMove += OnMouseMove;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;

            this.UpdateGridLayout();
            this.UpdateZoneLabels();
        }

        private void InitializeCard()
        {
            this.card = new EditorFloatingCard
            {
                DataContext = this.viewModel,
            };

            this.card.SaveClicked += (s, args) => this.OnEditorCardSaveClicked();
            this.card.CancelClicked += (s, args) => this.Close();

            double canvasWidth = this.RootCanvas.ActualWidth;
            double canvasHeight = this.RootCanvas.ActualHeight;
            double cardWidth = this.card.Width;
            double cardHeight = this.card.Height;

            Canvas.SetLeft(this.card, (canvasWidth / 2) - (cardWidth / 2));
            Canvas.SetTop(this.card, (canvasHeight / 2) - (cardHeight / 2));

            this.RootCanvas.Children.Add(this.card);
        }

        private void OnEditorCardSaveClicked()
        {
            this.viewModel.SaveChanges = true;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.InitializeCard();
            this.RenderLayout();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RenderLayout();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            this.RenderLayout();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (this.viewModel.State == LayoutEditorState.PlacingDivider)
            {
                this.viewModel.AddDivider(this.placeholderDivider.ViewModel);
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.viewModel.State == LayoutEditorState.DraggingDivider)
            {
                this.viewModel.HandleStateTransition(LayoutEditorState.PlacingDivider);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            // Capture the current normalized position of the mouse.
            CanvasMousePositionRecord mousePosition = new CanvasMousePositionRecord(e.GetPosition(this.RootCanvas), this.RootCanvas.ActualWidth, this.RootCanvas.ActualHeight);

            // Mouse move handling is dependent on the layout editor state.
            switch (this.viewModel.State)
            {
                case LayoutEditorState.PlacingDivider:
                    DividerViewModel? placeholderDividerViewModel = this.viewModel.GetPlaceholderDivider(mousePosition, isVertical: Keyboard.IsKeyDown(Key.LeftShift));
                    this.RenderPlaceholder(placeholderDividerViewModel);

                    break;

                case LayoutEditorState.DraggingDivider:
                    // Check whether the state conditions were not breahed
                    if (e.LeftButton == MouseButtonState.Released)
                    {
                        this.viewModel.HandleStateTransition(LayoutEditorState.PlacingDivider);
                        return;
                    }

                    this.viewModel.OnDividerDragTo(mousePosition);
                    break;

                case LayoutEditorState.HoveringHandle:
                case LayoutEditorState.HoveringCard:
                    break;
            }
        }

        private void RenderPlaceholder(DividerViewModel? vm)
        {
            if (vm == null)
            {
                this.CleanupPlaceholder();
                return;
            }

            if (this.placeholderDivider == null)
            {
                double canvasWidth = this.RootCanvas.ActualWidth;
                double canvasHeight = this.RootCanvas.ActualHeight;

                this.RootCanvas.Children.Remove(this.placeholderDivider);
                this.placeholderDivider = new DividerView(canvasWidth, canvasHeight);
                this.placeholderDivider.InitializeVisualLayers(DividerLinesLayer, DividerHandlesLayer);
                this.placeholderDivider.RequestRender(vm);
                this.RootCanvas.Children.Add(this.placeholderDivider);

                return;
            }

            this.placeholderDivider.RequestRender(vm);
            this.placeholderDivider.SetVisibility(Visibility.Visible);

        }

        private void CleanupPlaceholder()
        {
            if (this.placeholderDivider != null)
            {
                this.placeholderDivider.SetVisibility(Visibility.Collapsed);
            }
        }

        private void RenderLayout()
        {
            double canvasWidth = this.RootCanvas.ActualWidth;
            double canvasHeight = this.RootCanvas.ActualHeight;

            // Ensure the divider pool size is enough to display all divider view models.
            while (this.dividerViewPool.Count < this.viewModel.Dividers.Count)
            {
                var divider = new DividerView(canvasWidth, canvasHeight);
                divider.InitializeVisualLayers(DividerLinesLayer, DividerHandlesLayer);
                this.dividerViewPool.Add(divider);
                this.RootCanvas.Children.Add(divider);
            }

            // Render the dividers.
            for (int dividerIndex = 0; dividerIndex < this.viewModel.Dividers.Count; dividerIndex++)
            {
                var viewModel = this.viewModel.Dividers[dividerIndex];
                var view = this.dividerViewPool[dividerIndex];
                view.SetVisibility(Visibility.Visible);
                view.RequestRender(viewModel);
            }

            for (int dividerIndex = this.viewModel.Dividers.Count; dividerIndex < this.dividerViewPool.Count; dividerIndex++)
            {
                var view = this.dividerViewPool[dividerIndex];
                view.SetVisibility(Visibility.Collapsed);
            }
        }

        private void UpdateZoneLabels()
        {
            if (this.ZoneLabelGrid == null)
                return;

            // Get the zone infos.
            var zones = this.viewModel.ResolveZoneRenderInfos();

            // Ensure pool size
            int zoneIndex = 0;

            while (this.zoneLabelPool.Count < zones.Count)
            {
                var zone = new ZoneLabelView(++zoneIndex);
                this.zoneLabelPool.Add(zone);
                this.ZoneLabelGrid.Children.Add(zone);
            }

            // Apply layout
            for (int i = 0; i < zones.Count; i++)
            {
                var info = zones[i];
                var label = this.zoneLabelPool[i];
                label.UpdateZoneNumber(i + 1);

                Grid.SetRow(label, info.Row);
                Grid.SetColumn(label, info.Column);
                Grid.SetRowSpan(label, info.RowSpan);
                Grid.SetColumnSpan(label, info.ColumnSpan);

                label.Visibility = Visibility.Visible;
            }

            // Hide unused labels
            for (int i = zones.Count; i < this.zoneLabelPool.Count; i++)
            {
                this.zoneLabelPool[i].Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateGridLayout()
        {
            if (this.ZoneLabelGrid == null)
            {
                return;
            }

            var (rows, cols) = this.viewModel.RecomputeZoneGridStructure();

            this.ZoneLabelGrid.RowDefinitions.Clear();
            this.ZoneLabelGrid.ColumnDefinitions.Clear();

            foreach (var row in rows)
            {
                this.ZoneLabelGrid.RowDefinitions.Add(row);
            }

            foreach (var col in cols)
            {
                this.ZoneLabelGrid.ColumnDefinitions.Add(col);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }

            if (this.viewModel.State == LayoutEditorState.DraggingDivider && e.Key == Key.R)
            {
                this.viewModel.DeleteSelectedDivider();
            }

            if (this.viewModel.State == LayoutEditorState.PlacingDivider && e.Key == Key.LeftShift)
            {
                CanvasMousePositionRecord mousePosition = new CanvasMousePositionRecord(Mouse.GetPosition(this.RootCanvas), this.RootCanvas.ActualWidth, this.RootCanvas.ActualHeight);

                var vm = this.viewModel.GetPlaceholderDivider(mousePosition, true);
                this.RenderPlaceholder(vm);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.viewModel.State == LayoutEditorState.PlacingDivider && e.Key == Key.LeftShift)
            {
                CanvasMousePositionRecord mousePosition = new CanvasMousePositionRecord(Mouse.GetPosition(this.RootCanvas), this.RootCanvas.ActualWidth, this.RootCanvas.ActualHeight);

                var vm = this.viewModel.GetPlaceholderDivider(mousePosition, false);
                this.RenderPlaceholder(vm);
            }
        }
    }
}
