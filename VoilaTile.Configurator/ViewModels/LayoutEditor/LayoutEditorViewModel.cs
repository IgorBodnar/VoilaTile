namespace VoilaTile.Configurator.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Models;
    using VoilaTile.Configurator.Records;

    /// <summary>
    /// ViewModel for editing a layout template interactively using dividers.
    /// </summary>
    public partial class LayoutEditorViewModel : ObservableObject
    {
        #region Fields

        const int MinimumTileDimensionPixel = 200;

        const int DividerSnapInThresholdPixel = 10;

        const int DividerSnapOutThresholdPixel = 20;

        private readonly ZoneTemplate editedTemplate;

        /// <summary>
        /// The layout editor state.
        /// </summary>
        private LayoutEditorState layoutEditorState = LayoutEditorState.PlacingDivider;

        /// <summary>
        /// The layout editor state prior to entering the card with a mouse.
        /// </summary>
        private LayoutEditorState preCardMouseEnterState = LayoutEditorState.PlacingDivider;

        /// <summary>
        /// Gets or sets the dialog card opacity.
        /// </summary>
        [ObservableProperty]
        private double dialogCardOpacity = 1.0;

        /// <summary>
        /// Gets or sets the placeholder divider used during placement preview.
        /// </summary>
        [ObservableProperty]
        private DividerViewModel placeholderDivider = new DividerViewModel();

        /// <summary>
        /// Gets or sets the currently selected divider.
        /// </summary>
        [ObservableProperty]
        private DividerViewModel? selectedDivider;

        /// <summary>
        /// The tuple containing the selected divider envelope.
        /// </summary>
        private (double Lower, double Upper) selectedDividerEnvelope;

        #endregion Fields

        #region Constuctors

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutEditorViewModel"/> class.
        /// </summary>
        /// <param name="template">The zone template to be edited.</param>
        public LayoutEditorViewModel(ZoneTemplate template)
        {
            this.editedTemplate = template;
            this.InitializeDividers(template);
            this.AttachDividerEventHandlers();
        }

        #endregion Constructors

        #region Events

        /// <summary>
        /// Raised when zone labels need to be refreshed in the view.
        /// </summary>
        public event Action? UpdateZoneLabels;

        /// <summary>
        /// Raised when zone grid layout needs to be updated in the view.
        /// </summary>
        public event Action? UpdateGridLayout;

        /// <summary>
        /// Raised when the dividers need to be updated in the view.
        /// </summary>
        public event Action? UpdateDividers;

        /// <summary>
        /// Raised when the placeholder divider needs to be cleaned up in the view.
        /// </summary>
        public event Action? CleanupPlaceholderDivider;

        #endregion Events

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether to save changes.
        /// </summary>
        public bool SaveChanges { get; set; }

        /// <summary>
        /// Gets the layout editor state.
        /// </summary>
        public LayoutEditorState State => this.layoutEditorState;

        /// <summary>
        /// Gets the collection of currently active dividers.
        /// </summary>
        public ObservableCollection<DividerViewModel> Dividers { get; } = new ObservableCollection<DividerViewModel>();

        #endregion Properties

        #region Methods

        public ZoneTemplate ToZoneTemplate()
        {
            List<DividerModel> dividers = new List<DividerModel>();

            foreach (var dividerVm in this.Dividers)
            {
                dividers.Add(dividerVm.ToModel());
            }

            ZoneTemplate zoneTemplate = new ZoneTemplate
            {
                Name = this.editedTemplate.Name,
                Dividers = dividers,
                IsDefault = false,
            };

            return zoneTemplate;
        }

        /// <summary>
        /// Recomputes zone layout information and triggers label update.
        /// </summary>
        /// <returns>List of zone render infos with correct row/column spans.</returns>
        public List<LayoutEditorZoneRenderInfo> ResolveZoneRenderInfos()
        {
            List<LayoutEditorZoneRenderInfo> zoneRenderInfos = new List<LayoutEditorZoneRenderInfo>();

            var (xCuts, yCuts) = this.ResolveGridCuts();

            // Helper: checks if there's a vertical divider between col and col+1 across [boundStart, boundEnd]
            bool IsVerticallyBound(int rowIndex, int colIndex, double yStart, double yEnd)
            {
                double x = xCuts[colIndex];
                return this.Dividers.Any(d =>
                    d.IsVertical &&
                    Math.Abs(d.Position - x) < 0.0001 &&
                    d.BoundStart <= yStart &&
                    d.BoundEnd >= yEnd);
            }

            // Helper: checks if there's a horizontal divider between row and row+1 across [boundStart, boundEnd]
            bool IsHorizontallyBound(int rowIndex, int colIndex, double xStart, double xEnd)
            {
                double y = yCuts[rowIndex];
                return this.Dividers.Any(d =>
                    !d.IsVertical &&
                    Math.Abs(d.Position - y) < 0.0001 &&
                    d.BoundStart <= xStart &&
                    d.BoundEnd >= xEnd);
            }

            int rowCount = yCuts.Count - 1;
            int colCount = xCuts.Count - 1;

            bool[,] visited = new bool[rowCount, colCount];
            int zoneNumber = 1;

            for (int col = 0; col < colCount; col++)
            {
                for (int row = 0; row < rowCount; row++)
                {
                    if (visited[row, col])
                    {
                        continue;
                    }

                    int maxRowSpan = 1;
                    int maxColSpan = 1;

                    // Try to extend column span to the right
                    for (int c = col + 1; c < colCount; c++)
                    {
                        if (IsVerticallyBound(row, c, yCuts[row], yCuts[row + 1]))
                        {
                            break;
                        }

                        bool blocked = false;
                        for (int r = row; r < row + maxRowSpan; r++)
                        {
                            if (visited[r, c])
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                        {
                            break;
                        }

                        maxColSpan++;
                    }

                    // Try to extend row span downwards
                    for (int r = row + 1; r < rowCount; r++)
                    {
                        if (IsHorizontallyBound(r, col, xCuts[col], xCuts[col + maxColSpan]))
                        {
                            break;
                        }

                        bool blocked = false;
                        for (int c = col; c < col + maxColSpan; c++)
                        {
                            if (visited[r, c])
                            {
                                blocked = true;
                                break;
                            }
                        }

                        if (blocked)
                        {
                            break;
                        }

                        maxRowSpan++;
                    }

                    // Mark all covered cells
                    for (int r = row; r < row + maxRowSpan; r++)
                    {
                        for (int c = col; c < col + maxColSpan; c++)
                        {
                            visited[r, c] = true;
                        }
                    }

                    zoneRenderInfos.Add(new LayoutEditorZoneRenderInfo(
                        Row: row,
                        Column: col,
                        RowSpan: maxRowSpan,
                        ColumnSpan: maxColSpan,
                        ZoneNumber: zoneNumber++));
                }
            }

            return zoneRenderInfos;
        }

        /// <summary>
        /// Computes the row and column definitions for the zone label grid based on divider positions.
        /// </summary>
        /// <returns>Tuple of RowDefinitions and ColumnDefinitions lists.</returns>
        public (List<RowDefinition> Rows, List<ColumnDefinition> Columns) RecomputeZoneGridStructure()
        {
            var rowDefs = new List<RowDefinition>();
            var colDefs = new List<ColumnDefinition>();

            var (xCuts, yCuts) = this.ResolveGridCuts();

            for (int i = 0; i < xCuts.Count - 1; i++)
            {
                double widthRatio = xCuts[i + 1] - xCuts[i];
                colDefs.Add(new ColumnDefinition
                {
                    Width = new GridLength(widthRatio, GridUnitType.Star),
                });
            }

            for (int i = 0; i < yCuts.Count - 1; i++)
            {
                double heightRatio = yCuts[i + 1] - yCuts[i];
                rowDefs.Add(new RowDefinition
                {
                    Height = new GridLength(heightRatio, GridUnitType.Star),
                });
            }

            return (rowDefs, colDefs);
        }

        /// <summary>
        /// Handles the state transition and the necessary clean up.
        /// </summary>
        /// <param name="layoutEditorState">The new layout editor state.</param>
        public void HandleStateTransition(LayoutEditorState layoutEditorState)
        {
            switch (layoutEditorState)
            {
                case LayoutEditorState.PlacingDivider:
                    // Clean up the selection from if coming from dragging the divider.
                    if (this.State == LayoutEditorState.DraggingDivider)
                    {
                        foreach (var divider in this.Dividers)
                        {
                            divider.IsSelected = false;
                        }

                        this.SelectedDivider = null;
                    }

                    this.DialogCardOpacity = 1.0;

                    break;

                case LayoutEditorState.DraggingDivider:
                    // Cleanup placeholder divider if coming from placing divider state.
                    if (this.State == LayoutEditorState.PlacingDivider)
                    {
                        this.CleanupPlaceholderDivider?.Invoke();
                    }

                    this.DialogCardOpacity = 0.4;
                    break;

                case LayoutEditorState.HoveringHandle:
                    switch (this.State)
                    {
                        case LayoutEditorState.PlacingDivider:

                            this.CleanupPlaceholderDivider?.Invoke();
                            break;

                        case LayoutEditorState.DraggingDivider:
                            // Transition from dragging divider to hovering handle is not allowed.
                            return;

                        default:
                            break;
                    }

                    this.DialogCardOpacity = 1.0;

                    break;

                case LayoutEditorState.HoveringCard:
                    switch (this.State)
                    {
                        case LayoutEditorState.PlacingDivider:

                            this.CleanupPlaceholderDivider?.Invoke();
                            break;

                        case LayoutEditorState.DraggingDivider:
                            // Transition from dragging divider to hovering card is not allowed.
                            return;

                        default:
                            break;
                    }

                    this.DialogCardOpacity = 1.0;

                    break;
            }

            this.layoutEditorState = layoutEditorState;
        }

        /// <summary>
        /// Updates the position of the selected divider to a given normalized point,
        /// applying snapping behavior and updating any dependent dividers.
        /// </summary>
        /// <param name="mousePosition">The record capturing mouse position information at invocation.</param>
        public void OnDividerDragTo(CanvasMousePositionRecord mousePosition)
        {
            if (this.SelectedDivider == null)
            {
                return;
            }

            var divider = this.SelectedDivider;

            // Resolve the target position.
            double oldPosition = divider.Position;
            var primaryCanvasDimension = divider.IsVertical ? mousePosition.CanvasWidth : mousePosition.CanvasHeight;
            var candidatePlaceholderPosition = divider.IsVertical ? mousePosition.NormalizedMousePositon.X : mousePosition.NormalizedMousePositon.Y;
            var candidatePlaceholderSecondaryPosition = divider.IsVertical ? mousePosition.NormalizedMousePositon.Y : mousePosition.NormalizedMousePositon.X;
            var normalizedProximityLimit = MinimumTileDimensionPixel / primaryCanvasDimension;

            var lowerBound = this.selectedDividerEnvelope.Lower + normalizedProximityLimit;
            var upperBound = this.selectedDividerEnvelope.Upper - normalizedProximityLimit;

            var newPosition = Math.Clamp(candidatePlaceholderPosition, lowerBound, upperBound);

            double snapInThreshold = DividerSnapInThresholdPixel / primaryCanvasDimension;
            double snapOutThreshold = DividerSnapOutThresholdPixel / primaryCanvasDimension;

            double targetPosition = newPosition;

            // Check for snapping to other dividers.
            foreach (var other in this.Dividers)
            {
                // Potentially extend the snapping logic to the midpoints of dividers with opposite orientation.
                if (other == divider || other.IsVertical != divider.IsVertical)
                {
                    continue;
                }

                double distanceToOther = other.Position - newPosition;
                double previousDistance = other.Position - oldPosition;

                bool wasAligned = Math.Abs(previousDistance) < snapInThreshold;
                bool isApproaching = Math.Abs(distanceToOther) < Math.Abs(previousDistance);
                bool isClose = Math.Abs(distanceToOther) < snapInThreshold;
                bool isStillSnapped = Math.Abs(distanceToOther) < snapOutThreshold;

                if ((!wasAligned && isApproaching && isClose) || (wasAligned && isStillSnapped))
                {
                    targetPosition = other.Position;
                    break;
                }
            }

            divider.Position = targetPosition;

            // Adjust the boundaries of other dividers.
            foreach (var other in this.Dividers)
            {
                if (other == divider || other.IsVertical == divider.IsVertical || !(divider.BoundStart <= other.Position && divider.BoundEnd >= other.Position))
                {
                    continue;
                }

                if (Math.Abs(other.BoundStart - oldPosition) < 0.001)
                {
                    other.BoundStart = targetPosition;
                }

                if (Math.Abs(other.BoundEnd - oldPosition) < 0.001)
                {
                    other.BoundEnd = targetPosition;
                }
            }

            this.UpdateZones();

            this.UpdateDividers?.Invoke();
        }

        /// <summary>
        /// Adds a divider to the layout based on the given view model.
        /// </summary>
        /// <param name="viewModel">The source divider view model to duplicate and add.</param>
        public void AddDivider(DividerViewModel viewModel)
        {
            DividerViewModel newDividerViewModel = (DividerViewModel)viewModel.Clone();

            newDividerViewModel.Selected += this.OnDividerSelected;
            newDividerViewModel.MouseEntered += this.OnDividerHandleEnter;
            newDividerViewModel.MouseLeft += this.OnDividerHandleLeave;

            this.Dividers.Add(newDividerViewModel);

            this.UpdateZones();
        }

        /// <summary>
        /// Deletes the currently selected divider.
        /// </summary>
        public void DeleteSelectedDivider()
        {
            // Check that selected divider exists.
            if (this.SelectedDivider == null)
            {
                return;
            }

            // Check that it can be deleted (i. e. its position is not being used as a boundary in other deviders.)
            if (this.Dividers.Any(divider => (divider.IsVertical != this.SelectedDivider.IsVertical && (divider.BoundStart == this.SelectedDivider.Position || divider.BoundEnd == this.SelectedDivider.Position))))
            {
                return;
            }

            // Unsubscribe from the divider events.
            this.SelectedDivider.Selected -= this.OnDividerSelected;
            this.SelectedDivider.MouseEntered -= this.OnDividerHandleEnter;
            this.SelectedDivider.MouseLeft -= this.OnDividerHandleLeave;

            this.Dividers.Remove(this.SelectedDivider);

            this.UpdateZones();

            this.HandleStateTransition(LayoutEditorState.PlacingDivider);
        }

        /// <summary>
        /// Handles the event of mouse entering the dialog card.
        /// </summary>
        public void OnCardMouseEnter()
        {
            this.preCardMouseEnterState = this.layoutEditorState;
            this.HandleStateTransition(LayoutEditorState.HoveringCard);
        }

        /// <summary>
        /// Handles the event of mouse leaving the dialog card.
        /// </summary>
        public void OnCardMouseLeave()
        {
            this.HandleStateTransition(this.preCardMouseEnterState);
        }

        /// <summary>
        /// Computes a preview placeholder divider at the specified position.
        /// </summary>
        /// <param name="mousePositon">The record capturing mouse position information at invocation.</param>
        /// <param name="isVertical">Whether the divider is vertical.</param>
        /// <returns>The configured placeholder divider view model.</returns>
        public DividerViewModel? GetPlaceholderDivider(CanvasMousePositionRecord mousePositon, bool isVertical)
        {
            if (mousePositon.NormalizedMousePositon.X <= 0 || mousePositon.NormalizedMousePositon.X >= 1 || mousePositon.NormalizedMousePositon.Y <= 0 || mousePositon.NormalizedMousePositon.Y >= 1)
            {
                // Position outside the canvas area, return null to cleanup the placeholder.
                return null;
            }

            var candidatePlaceholderPosition = isVertical ? mousePositon.NormalizedMousePositon.X : mousePositon.NormalizedMousePositon.Y;
            var candidatePlaceholderSecondaryPosition = isVertical ? mousePositon.NormalizedMousePositon.Y : mousePositon.NormalizedMousePositon.X;
            var normalizedProximityLimit = isVertical ? 100 / mousePositon.CanvasWidth : 100 / mousePositon.CanvasHeight;

            // Resolve the motion limits.
            var candidatePositionBounds = this.Dividers
                .Where(other => other.IsVertical == isVertical)
                .Where(other => candidatePlaceholderSecondaryPosition >= other.BoundStart && candidatePlaceholderSecondaryPosition <= other.BoundEnd)
                .Select(other => other.Position).ToList();

            candidatePositionBounds.AddRange([0, 1]);
            candidatePositionBounds = candidatePositionBounds.OrderBy(other => other).ToList();

            var lowerBound = candidatePositionBounds.Last(limit => limit < candidatePlaceholderPosition) + normalizedProximityLimit;
            var upperBound = candidatePositionBounds.First(limit => limit > candidatePlaceholderPosition) - normalizedProximityLimit;

            if (upperBound <= lowerBound)
            {
                // Invalid placeholder position within our tile placement constrains.
                return null;
            }

            this.PlaceholderDivider.Position = Math.Clamp(candidatePlaceholderPosition, lowerBound, upperBound);

            // Only dividers with opposite orientation to placeholder and with placeholder position lying within their bounds are valid boundary candidates.
            var bounds = this.Dividers
                .Where(div => div.IsVertical != isVertical)
                .Where(div => ((div.BoundStart <= this.PlaceholderDivider.Position) && (div.BoundEnd >= this.PlaceholderDivider.Position)))
                .Select(div => div.Position)
                .ToList();

            bounds.AddRange([0, 1]);

            var extendedBounds = bounds.OrderBy(p => p).ToList();
            double boundStart = extendedBounds.Last(b => b <= candidatePlaceholderSecondaryPosition);
            double boundEnd = extendedBounds.First(b => b >= candidatePlaceholderSecondaryPosition);

            this.PlaceholderDivider.BoundStart = boundStart;
            this.PlaceholderDivider.BoundEnd = boundEnd;
            this.PlaceholderDivider.IsVertical = isVertical;
            this.PlaceholderDivider.IsPlaceholder = true;

            return this.PlaceholderDivider;
        }

        /// <summary>
        /// Resolves the cuts for the grid control based on the internal collection of dividers.
        /// </summary>
        /// <returns>A tuple containing lists of x and y cuts.</returns>
        private (List<double> XCuts, List<double> YCuts) ResolveGridCuts()
        {
            var xCuts = this.Dividers
                .Where(d => d.IsVertical)
                .Select(d => d.Position)
                .Append(0.0).Append(1.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var yCuts = this.Dividers
                .Where(d => !d.IsVertical)
                .Select(d => d.Position)
                .Append(0.0).Append(1.0)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            return (xCuts, yCuts);
        }

        /// <summary>
        /// Updates the zones.
        /// </summary>
        private void UpdateZones()
        {
            this.UpdateGridLayout?.Invoke();
            this.UpdateZoneLabels?.Invoke();
        }

        /// <summary>
        /// Initializes the internal collection of dividers from a provided <see cref="ZoneTemplate"/>.
        /// </summary>
        private void InitializeDividers(ZoneTemplate template)
        {
            this.Dividers.Clear();

            foreach (var model in template.Dividers)
            {
                this.Dividers.Add(new DividerViewModel(model));
            }
        }

        /// <summary>
        /// Attaches the handlers to the dividers in the internal collection.
        /// </summary>
        private void AttachDividerEventHandlers()
        {
            foreach (var divider in this.Dividers)
            {
                divider.Selected += this.OnDividerSelected;
                divider.MouseEntered += this.OnDividerHandleEnter;
                divider.MouseLeft += this.OnDividerHandleLeave;
            }
        }

        private void CalculateSelectedDividerEnvelope()
        {
            // TODO: Something is wrong here.
            var divider = this.SelectedDivider;

            var envelopeCandidates = this.Dividers
                .Where(other => other != divider)
                .Where(other => (other.BoundStart <= divider.BoundStart && other.BoundEnd >= divider.BoundEnd))
                .Where(other => other.IsVertical == divider.IsVertical)
                .Select(other => other.Position)
                .ToList();

            envelopeCandidates.AddRange([0, 1]);

            var orderedEnvelopeCandidates = envelopeCandidates.OrderBy(position => position).ToList();

            var lower = orderedEnvelopeCandidates.Last(position => position < divider.Position);
            var upper = orderedEnvelopeCandidates.First(position => position > divider.Position);

            this.selectedDividerEnvelope = (lower, upper);
        }

        /// <summary>
        /// Handles the event of a divider being selected.
        /// </summary>
        private void OnDividerSelected(DividerViewModel vm)
        {
            // Push the selected divider to the end of the collection such that it is rendered last.
            this.Dividers.Remove(vm);
            this.Dividers.Add(vm);

            this.SelectedDivider = vm;
            this.SelectedDivider.IsSelected = true;

            this.CalculateSelectedDividerEnvelope();

            this.HandleStateTransition(LayoutEditorState.DraggingDivider);
        }

        /// <summary>
        /// Handles the event of mouse entering the divider handle.
        /// </summary>
        private void OnDividerHandleEnter()
        {
            this.HandleStateTransition(LayoutEditorState.HoveringHandle);
        }

        /// <summary>
        /// Handles the event of mouse leaving the divider handle.
        /// </summary>
        private void OnDividerHandleLeave()
        {
            this.HandleStateTransition(LayoutEditorState.PlacingDivider);
        }

        #endregion Methods
    }
}
