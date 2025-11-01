namespace VoilaTile.Settings.ViewModels.Panels
{
    using System.Collections.ObjectModel;
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using VoilaTile.Settings.DTO;
    using VoilaTile.Settings.Enumerations;
    using VoilaTile.Settings.Interfaces;
    using VoilaTile.Settings.Models;
    using VoilaTile.Settings.Services;

    /// <summary>
    /// Layouts settings panel.
    /// </summary>
    public partial class LayoutsSettingsPanelViewModel : SettingsPanelViewModel
    {
        #region Fields

        /// <summary>
        /// The model backing this view model.
        /// </summary>
        private readonly LayoutsSettingsPanelModel model;

        /// <summary>
        /// Gets or sets the total canvas width.
        /// </summary>
        [ObservableProperty]
        private int totalCanvasWidth;

        /// <summary>
        /// Gets or sets the total canvas height.
        /// </summary>
        [ObservableProperty]
        private int totalCanvasHeight;

        /// <summary>
        /// Backing field for an observable property to trigger scroll on template list.
        /// </summary>
        /// <remarks>
        /// Incrementing this value will trigger a scroll to the bottom of the template list.
        /// </remarks>
        [ObservableProperty]
        private long templateScrollVersion;

        /// <summary>
        /// The currently selected monitor.
        /// </summary>
        private MonitorViewModel? selectedMonitor;

        /// <summary>
        /// The currently highlighted template preview.
        /// </summary>
        private ZoneTemplatePreviewViewModel? highlightedPreview;

        /// <summary>
        /// The command to launch the layout editor for the highlighted template on the selected.
        /// </summary>
        private RelayCommand? editLayoutCommand;

        /// <summary>
        /// The command to launch the layout editor for the template selected on the current monitor.
        /// </summary>
        private RelayCommand? editCurrentMonitorLayoutCommand;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutsSettingsPanelViewModel"/> class.
        /// </summary>
        /// <param name="model">The model backing this view model.</param>
        public LayoutsSettingsPanelViewModel(LayoutsSettingsPanelModel model)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));

            this.Title = "Layouts";

            this.InitializeTemplates();
            this.InitializeMonitorLayout();
            this.InitializeMonitorSelection();
            this.InitializeTemplatePreviews();
        }

        #endregion Constructors

        #region Propeties

        /// <summary>
        /// Gets the collection of monitor view models.
        /// </summary>
        public ObservableCollection<MonitorViewModel> Monitors { get; } = new();

        /// <summary>
        /// Gets the template library shared across monitors.
        /// </summary>
        public ObservableCollection<ZoneTemplateViewModel> TemplateLibrary { get; } = new();

        /// <summary>
        /// Gets the previews of all templates not currently selected.
        /// </summary>
        public ObservableCollection<ZoneTemplatePreviewViewModel> TemplatePreviews { get; } = new();

        /// <summary>
        /// The currently highlighted template preview.
        /// </summary>
        private ZoneTemplatePreviewViewModel? HighlightedPreview
        {
            get => this.highlightedPreview;
            set
            {
                this.highlightedPreview = value;
                this.editLayoutCommand?.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Gets the command to launch the layout editor for the selected monitor and template.
        /// </summary>
        public IRelayCommand EditLayoutCommand => this.editLayoutCommand ??= new RelayCommand(this.EditLayout, () => this.CanEditLayout);

        /// <summary>
        /// Gets the command to launch the layout editor for the template selected on the current monitor.
        /// </summary>
        public IRelayCommand EditCurrentMonitorLayoutCommand => this.editCurrentMonitorLayoutCommand ??= new RelayCommand(this.EditSelectedLayout, () => this.CanEditCurrentMonitorLayout);

        /// <summary>
        /// Gets the command to add a new layout and launch an editor for it.
        /// </summary>
        public IRelayCommand AddNewLayoutCommand => new RelayCommand(this.AddNewLayout);

        /// <summary>
        /// The value indicating whether the highlighted layout can be edited.
        /// </summary>
        private bool CanEditLayout => this.highlightedPreview != null && this.highlightedPreview.IsUserAdded;

        /// <summary>
        /// The value indicating whether the selected monitor's current layout can be edited.
        /// </summary>
        private bool CanEditCurrentMonitorLayout => this.selectedMonitor != null && this.selectedMonitor.SelectedTemplate.Template.IsUserAdded;

        #endregion

        #region Methods

        /// <summary>
        /// Saves the monitor template selections to the model asynchronously.
        /// </summary>
        /// <returns>An instance of <see cref="Task"/> representing an asynchronous operation.</returns>
        public async Task SaveMonitorTemplateSelectionAsync()
        {
            SelectionCollectionDTO selectionCollectionDTO = new SelectionCollectionDTO
            {
                Selections = this.Monitors.Select(m =>
                new MonitorTemplateSelectionDTO
                {
                    MonitorID = m.MonitorInfo.DeviceID,
                    TemplateName = m.SelectedTemplate!.Name,
                }).ToList(),
            };

            await this.model.SaveMonitorTemplateSelectionAsync(selectionCollectionDTO);
        }

        /// <summary>
        /// Saves the user-added templates to the model asynchronously.
        /// </summary>
        /// <returns>An instance of <see cref="Task"/> representing an asynchronous operation.</returns>
        public async Task SaveTemplatesAsync()
        {
            var templatesToPersist = this.TemplateLibrary.Select(vm => vm.Template).Where(m => m.IsUserAdded).ToList();

            await this.model.SaveTemplatesAsync(TemplatesMapper.MapToDTO(templatesToPersist));
        }

        /// <summary>
        /// Initializes the template library with imported and default templates.
        /// </summary>
        private void InitializeTemplates()
        {
            // Add default templates.
            var fullScreen = new ZoneTemplate
            {
                Name = "Full Screen",
                IsUserAdded = false,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(fullScreen));

            var twoColumns = new ZoneTemplate
            {
                Name = "Two Columns",
                Dividers = new List<DividerModel>()
                {
                    new DividerModel(){IsVertical = true, Position = 0.5, BoundStart = 0, BoundEnd = 1},
                },
                IsUserAdded = false,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(twoColumns));

            var threeColumns = new ZoneTemplate
            {
                Name = "Three Columns",
                Dividers = new List<DividerModel>()
                {
                    new DividerModel(){IsVertical = true, Position = 1d / 3, BoundStart = 0, BoundEnd = 1},
                    new DividerModel(){IsVertical = true, Position = 2d / 3, BoundStart = 0, BoundEnd = 1},
                },
                IsUserAdded = false,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(threeColumns));

            // Add imported templates.
            var templates = this.model.Templates;

            foreach (var template in templates)
            {
                this.TemplateLibrary.Add(new ZoneTemplateViewModel(template));
            }
        }

        /// <summary>
        /// Initializes the monitor layout based on the model data.
        /// </summary>
        private void InitializeMonitorLayout()
        {
            // Load monitors and selections from the model.
            var monitors = this.model.Monitors;
            var selection = this.model.MonitorTemplateSelection;

            int minX = monitors.Min(m => m.MonitorX);
            int maxX = monitors.Max(m => m.MonitorX + m.MonitorWidth);
            int minY = monitors.Min(m => m.MonitorY);
            int maxY = monitors.Max(m => m.MonitorY + m.MonitorHeight);

            int offsetX = -minX;
            int offsetY = -minY;

            this.TotalCanvasWidth = maxX - minX;
            this.TotalCanvasHeight = maxY - minY;

            foreach (var monitor in monitors)
            {
                var resolvedTemplateName = selection.Selections.FirstOrDefault(s => s.MonitorID == monitor.DeviceID)?.TemplateName ?? null;
                var resolvedTemplateVm = this.TemplateLibrary.FirstOrDefault(vm => vm.Name == resolvedTemplateName);

                var vm = new MonitorViewModel(monitor, offsetX, offsetY)
                {
                    SelectedTemplate = resolvedTemplateVm == null ? this.TemplateLibrary.First() : resolvedTemplateVm,
                };

                vm.Clicked += this.OnMonitorClicked;
                this.Monitors.Add(vm);
            }
        }

        /// <summary>
        /// Initializes the selected monitor.
        /// </summary>
        private void InitializeMonitorSelection()
        {
            if (this.Monitors.Any())
            {
                this.selectedMonitor = this.Monitors.First();
                this.selectedMonitor.IsSelected = true;
            }

            this.editLayoutCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Initializes the template previews for the selected monitor.
        /// </summary>
        private void InitializeTemplatePreviews()
        {
            if (this.selectedMonitor is null || this.selectedMonitor.SelectedTemplate is null)
            {
                return;
            }

            IEnumerable<ZoneTemplateViewModel> others = this.TemplateLibrary
                .Where(t => t != this.selectedMonitor.SelectedTemplate);

            this.ClearTemplateLibrary();

            foreach (ZoneTemplateViewModel template in others)
            {
                var previewVm = new ZoneTemplatePreviewViewModel(template, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
                this.AttachTemplatePreviewEventHandlers(previewVm);
                this.TemplatePreviews.Add(previewVm);
            }

            this.editCurrentMonitorLayoutCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Handles monitor click events to update selection.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnMonitorClicked(object? sender, EventArgs e)
        {
            if (sender is not MonitorViewModel clicked)
            {
                return;
            }

            foreach (MonitorViewModel monitor in this.Monitors)
            {
                monitor.IsSelected = monitor == clicked;
            }

            this.selectedMonitor = clicked;

            this.InitializeTemplatePreviews();

            this.HighlightedPreview = null;
        }

        /// <summary>
        /// Attaches event handlers to a template preview view model.
        /// </summary>
        /// <param name="viewModel">The template preview view model.</param>
        private void AttachTemplatePreviewEventHandlers(ZoneTemplatePreviewViewModel viewModel)
        {
            viewModel.Clicked += this.OnTemplateClicked;
            viewModel.DoubleClicked += this.OnTemplateDoubleClicked;
            viewModel.CopyRequested += this.OnCopyRequested;
            viewModel.DeleteRequested += this.OnDeleteRequested;
        }

        /// <summary>
        /// Detaches event handlers from a template preview view model.
        /// </summary>
        /// <param name="viewModel">The template preview view model.</param>
        private void DetachTemplatePreviewEventHandlers(ZoneTemplatePreviewViewModel viewModel)
        {
            viewModel.Clicked -= this.OnTemplateClicked;
            viewModel.DoubleClicked -= this.OnTemplateDoubleClicked;
            viewModel.CopyRequested -= this.OnCopyRequested;
            viewModel.DeleteRequested -= this.OnDeleteRequested;
        }

        /// <summary>
        /// Clears the template library and detaches event handlers.
        /// </summary>
        private void ClearTemplateLibrary()
        {
            foreach (var preview in this.TemplatePreviews)
            {
                this.DetachTemplatePreviewEventHandlers(preview);
            }

            this.TemplatePreviews.Clear();
        }

        /// <summary>
        /// Handles template click events to update highlighting.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTemplateClicked(object? sender, EventArgs e)
        {
            if (sender is not ZoneTemplatePreviewViewModel clicked)
            {
                return;
            }

            // Check if clicked model is already highligted and clear the highlight if it is.
            if (this.HighlightedPreview == clicked)
            {
                this.HighlightedPreview.IsHighlighted = false;
                this.HighlightedPreview = null;
                return;
            }

            // Clean up the previous highlight.
            if (this.HighlightedPreview != null)
            {
                this.HighlightedPreview.IsHighlighted = false;
            }

            clicked.IsHighlighted = true;

            this.HighlightedPreview = clicked;
        }

        /// <summary>
        /// Handles template double-click events to select the template for the current monitor.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTemplateDoubleClicked(object? sender, EventArgs e)
        {
            if (sender is not ZoneTemplatePreviewViewModel clicked || this.selectedMonitor is null)
            {
                return;
            }

            this.selectedMonitor.SelectedTemplate = clicked.Template;

            IEnumerable<ZoneTemplateViewModel> others = this.TemplateLibrary
                .Where(t => t != clicked.Template);

            this.ClearTemplateLibrary();

            foreach (ZoneTemplateViewModel template in others)
            {
                var vm = new ZoneTemplatePreviewViewModel(template, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
                this.AttachTemplatePreviewEventHandlers(vm);
                this.TemplatePreviews.Add(vm);
            }

            this.HighlightedPreview = null;

            this.editCurrentMonitorLayoutCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Handles template copy requests to duplicate a template.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnCopyRequested(object? sender, EventArgs e)
        {
            if (sender is not ZoneTemplatePreviewViewModel vm || this.selectedMonitor == null)
            {
                return;
            }

            // Clone the template.
            ZoneTemplate templateCopy = (ZoneTemplate)vm.Template.Template.Clone();

            // Change the template name and default flag.
            templateCopy.Name += " - Copy";
            templateCopy.IsUserAdded = false;

            // Create a view model for the new template and add to the library.
            ZoneTemplateViewModel templateVm = new ZoneTemplateViewModel(templateCopy);
            this.TemplateLibrary.Add(templateVm);

            // Create a preview view model and add it to the previews.
            var previewVm = new ZoneTemplatePreviewViewModel(templateVm, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
            this.AttachTemplatePreviewEventHandlers(previewVm);

            this.TemplatePreviews.Add(previewVm);
            this.TemplateScrollVersion++; // Trigger template library to scroll to bottom, revealing the new template.
        }

        /// <summary>
        /// Handles template delete requests to remove a template.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private async void OnDeleteRequested(object? sender, EventArgs e)
        {
            if (sender is not ZoneTemplatePreviewViewModel vm)
            {
                return;
            }

            if (this.Monitors.Any(m => m.SelectedTemplate == vm.Template))
            {
                var dialog = new InformationDialogViewModel("Delete Warning", "This template cannot be deleted as it is used by other monitors.");
                await App.DialogService.ShowAsync(dialog);

                return;
            }

            // Ask for delete confirmation.

            var confirmDialogVm = new ConfirmationDialogViewModel(
                "Delete Layout",
                $"Are you sure you want to delete the layout '{vm.Template.Name}'?\n\nThis action cannot be undone.");

            var (result, _) = await App.DialogService.ShowAsync(confirmDialogVm);

            if (result == DialogDecision.Negative)
            {
                return;
            }

            // Remove from the template library.
            var libraryTemplate = this.TemplateLibrary.First(template => vm.Template == template);

            if (libraryTemplate != null)
            {
                this.DetachTemplatePreviewEventHandlers(vm);
                this.TemplateLibrary.Remove(libraryTemplate);
            }

            // Remove from the previews.
            this.TemplatePreviews.Remove(vm);
        }

        /// <summary>
        /// Opens the layout editor for the highlighted template on the selected monitor.
        /// </summary>
        private void EditLayout()
        {
            if (this.selectedMonitor == null || this.HighlightedPreview == null)
            {
                return;
            }

            var callback = (LayoutEditorViewModel editor) =>
            {
                if (editor.SaveChanges && this.HighlightedPreview != null)
                {
                    this.HighlightedPreview!.Template.UpdateFromTemplate(editor.ToZoneTemplate());
                }
            };

            LayoutEditorManager.OpenEditor(
                this.HighlightedPreview.Template.Template,
                this.selectedMonitor.MonitorInfo,
                onCloseCallback: callback);
        }

        /// <summary>
        /// Opens the layout editor for the template selected on the current monitor.
        /// </summary>
        private void EditSelectedLayout()
        {
            if (this.selectedMonitor == null || this.selectedMonitor.SelectedTemplate == null)
            {
                return;
            }

            var callback = (LayoutEditorViewModel editor) =>
            {
                if (editor.SaveChanges)
                {
                    this.selectedMonitor.SelectedTemplate.UpdateFromTemplate(editor.ToZoneTemplate());
                }
            };

            LayoutEditorManager.OpenEditor(
                this.selectedMonitor.SelectedTemplate.Template,
                this.selectedMonitor.MonitorInfo,
                onCloseCallback: callback);
        }

        /// <summary>
        /// Adds a new layout by prompting the user for a name and launching the layout editor.
        /// </summary>
        private async void AddNewLayout()
        {
            if (this.selectedMonitor == null)
            {
                return;
            }

            // Prompt the user to confirm creation and name the new layout.
            var newLayoutName = "New Layout";

            var vm = new CreateNewLayoutDialogViewModel(newLayoutName);
            var (result, updatedVm) = await App.DialogService.ShowAsync(vm);

            if (result == DialogDecision.Negative)
            {
                return;
            }

            if (updatedVm is CreateNewLayoutDialogViewModel createNewLayoutViewModel)
            {
                newLayoutName = createNewLayoutViewModel.Name;
            }

            // Initialize the new layout.
            var newLayout = new ZoneTemplate
            {
                Name = newLayoutName,
            };

            // Create the on close callback.
            var callback = (LayoutEditorViewModel editor) =>
            {
                if (editor.SaveChanges)
                {
                    // Extract the template model from the editor.
                    var templateModel = editor.ToZoneTemplate();

                    // Create and add a new template view model.
                    var templateViewModel = new ZoneTemplateViewModel(templateModel);
                    this.TemplateLibrary.Add(templateViewModel);

                    // Create and add a new template preview view model.
                    var previewViewModel = new ZoneTemplatePreviewViewModel(templateViewModel, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
                    this.AttachTemplatePreviewEventHandlers(previewViewModel);

                    this.TemplatePreviews.Add(previewViewModel);
                    this.TemplateScrollVersion++; // Trigger template library to scroll to bottom, revealing the new template.
                }
            };

            LayoutEditorManager.OpenEditor(
                newLayout,
                this.selectedMonitor.MonitorInfo,
                onCloseCallback: callback);
        }


        #endregion
    }
}

