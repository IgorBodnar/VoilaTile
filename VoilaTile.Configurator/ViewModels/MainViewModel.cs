namespace VoilaTile.Configurator.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Helpers;
    using VoilaTile.Configurator.Models;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;
    using VoilaTile.Configurator.DTO;
    using VoilaTile.Common.DTO;
    using System.Windows.Input;
    using System.Web;

    /// <summary>
    /// The main view model of the application.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private MonitorViewModel? selectedMonitor;

        private ZoneTemplatePreviewViewModel? highlightedPreview;

        private RelayCommand? editLayoutCommand;

        private RelayCommand? editCurrentMonitorLayoutCommand;

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
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel(List<ZoneTemplate> templates, SelectionCollectionDTO selection, SettingsDTO settings)
        {
            List<MonitorInfo> infos = MonitorManager.GetMonitors();

            this.InitializeSettings(settings);
            this.InitializeTemplates(templates);
            this.InitializeMonitorLayout(infos, selection);
            this.InitializeMonitorSelection();
            this.InitializeTemplatePreviews();
        }

        /// <summary>
        /// The value indicating whether to bootstrap snapper on exit.
        /// </summary>
        public bool BootstrapSnapperOnExit { get; private set; } = false;

        /// <summary>
        /// The action raised when the template library is to be scrolled all the way down.
        /// </summary>
        public event Action? RequestTemplateLibraryScroll;

        /// <summary>
        /// Gets the command to launch the layout editor for the selected monitor and template.
        /// </summary>
        public IRelayCommand EditLayoutCommand => this.editLayoutCommand ??= new RelayCommand(this.EditLayout, () => this.CanEditLayout);

        public IRelayCommand EditCurrentMonitorLayoutCommand => this.editCurrentMonitorLayoutCommand ??= new RelayCommand(this.EditSelectedLayout, () => this.CanEditCurrentMonitorLayout);

        public IRelayCommand AddNewLayoutCommand => new RelayCommand(this.AddNewLayout);

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
        /// Gets the preview for the currently selected template.
        /// </summary>
        public ObservableCollection<ZoneTemplatePreviewViewModel> SelectedTemplatePreview { get; } = new();

        public SettingsViewModel Settings { get; } = new();

        private ZoneTemplatePreviewViewModel? HighlightedPreview
        {
            get => this.highlightedPreview;
            set
            {
                this.highlightedPreview = value;
                this.editLayoutCommand?.NotifyCanExecuteChanged();
            }
        }

        private bool CanEditLayout => this.highlightedPreview != null && !this.highlightedPreview.IsDefault;

        private bool CanEditCurrentMonitorLayout => this.selectedMonitor != null && !this.selectedMonitor.SelectedTemplate.Template.IsDefault;

        private void InitializeTemplates(List<ZoneTemplate> templates)
        {
            // Add default templates.
            var fullScreen = new ZoneTemplate
            {
                Name = "Full Screen",
                IsDefault = true,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(fullScreen));

            var twoColumns = new ZoneTemplate
            {
                Name = "Two Columns",
                Dividers = new List<DividerModel>()
                {
                    new DividerModel(){IsVertical = true, Position = 0.5, BoundStart = 0, BoundEnd = 1},
                },
                IsDefault = true,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(twoColumns));

            var threeColumns = new ZoneTemplate
            {
                Name = "Three Columns",
                Dividers = new List<DividerModel>()
                {
                    new DividerModel(){IsVertical = true, Position = 1d/3, BoundStart = 0, BoundEnd = 1},
                    new DividerModel(){IsVertical = true, Position = 2d/3, BoundStart = 0, BoundEnd = 1},
                },
                IsDefault = true,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(threeColumns));

            var mainAndHelpers = new ZoneTemplate
            {
                Name = "Main And Helpers",
                Dividers = new List<DividerModel>()
                {
                    new DividerModel(){IsVertical = true, Position = 0.2, BoundStart = 0, BoundEnd = 1},
                    new DividerModel(){IsVertical = true, Position = 0.6, BoundStart = 0, BoundEnd = 1},
                    new DividerModel(){IsVertical = false, Position = 0.5, BoundStart = 0.2, BoundEnd = 0.6},
                    new DividerModel(){IsVertical = false, Position = 0.5, BoundStart = 0.6, BoundEnd = 1},
                },
                IsDefault = true,
            };

            this.TemplateLibrary.Add(new ZoneTemplateViewModel(mainAndHelpers));

            // Add imported templates.
            foreach (var template in templates)
            {
                this.TemplateLibrary.Add(new ZoneTemplateViewModel(template));
            }
        }

        private void InitializeSettings(SettingsDTO settingsDTO)
        {
            if (!settingsDTO.Seed.Equals(string.Empty))
            {
                this.Settings.Seed = settingsDTO.Seed;
            }
            
            if (Enum.TryParse<Key>(settingsDTO.SelectedShortcutKey, out var parsedKey))
            {
                this.Settings.SelectedShortcutKey = parsedKey;
            }
        }

        private void InitializeMonitorLayout(List<MonitorInfo> monitors, SelectionCollectionDTO selection)
        {
            int minX = monitors.Min(m => m.MonitorX);
            int maxX = monitors.Max(m => m.MonitorX + m.MonitorWidth);
            int minY = monitors.Min(m => m.MonitorY);
            int maxY = monitors.Max(m => m.MonitorY + m.MonitorHeight);

            int offsetX = -minX;
            int offsetY = -minY;

            this.TotalCanvasWidth = maxX - minX;
            this.TotalCanvasHeight = maxY - minY;

            foreach (MonitorInfo monitor in monitors)
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

        private void InitializeMonitorSelection()
        {
            if (this.Monitors.Any())
            {
                this.selectedMonitor = this.Monitors.First();
                this.selectedMonitor.IsSelected = true;
            }

            this.editLayoutCommand?.NotifyCanExecuteChanged();
        }

        private void InitializeTemplatePreviews()
        {
            if (this.selectedMonitor is null || this.selectedMonitor.SelectedTemplate is null)
            {
                return;
            }

            this.SelectedTemplatePreview.Clear();
            var selectedMonitorPreviewViewModel = new ZoneTemplatePreviewViewModel(
                    this.selectedMonitor.SelectedTemplate,
                    this.selectedMonitor.MonitorWidth,
                    this.selectedMonitor.MonitorHeight)
            {
                IsSelected = true,
            };

            this.SelectedTemplatePreview.Add(selectedMonitorPreviewViewModel);

            IEnumerable<ZoneTemplateViewModel> others = this.TemplateLibrary
                .Where(t => t != this.selectedMonitor.SelectedTemplate);

            this.TemplatePreviews.Clear();

            foreach (ZoneTemplateViewModel template in others)
            {
                var previewVm = new ZoneTemplatePreviewViewModel(template, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
                this.AttachTemplatePreviewEventHandlers(previewVm);
                this.TemplatePreviews.Add(previewVm);
            }

            this.editCurrentMonitorLayoutCommand?.NotifyCanExecuteChanged();
        }

        public void ShowSelectedMonitorOverlay()
        {
            if (this.selectedMonitor is not null)
            {
                OverlayManager.ShowOverlay(this.selectedMonitor);
            }
        }

        /// <summary>
        /// Detects if Snapper is running and, if not, asks the user whether to start it on exit.
        /// Sets <see cref="BootstrapSnapperOnExit"/> accordingly.
        /// </summary>
        /// <returns>A task that completes when the user has made a decision.</returns>
        public async Task CheckSnapperRunningAsync()
        {
            var isRunning = SnapperProcessHelper.IsSnapperRunning();

            if (isRunning)
            {
                this.BootstrapSnapperOnExit = false;
                return;
            }

            var vm = new ConfirmationDialogViewModel(
                title: "Start Snapper",
                text: "VoilaTile.Snapper doesn’t appear to be running.\nDo you want to start it on exit to try your new layouts?");

            var (result, _) = await App.DialogService.ShowAsync(vm);
            this.BootstrapSnapperOnExit = result == DialogDecision.Positive;
        }

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
            this.ShowSelectedMonitorOverlay();

            this.HighlightedPreview = null;
        }

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

        private void OnTemplateDoubleClicked(object? sender, EventArgs e)
        {
            if (sender is not ZoneTemplatePreviewViewModel clicked || this.selectedMonitor is null)
            {
                return;
            }

            this.selectedMonitor.SelectedTemplate = clicked.Template;

            this.SelectedTemplatePreview.Clear();
            this.SelectedTemplatePreview.Add(
                new ZoneTemplatePreviewViewModel(clicked.Template, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight) { IsSelected = true });

            IEnumerable<ZoneTemplateViewModel> others = this.TemplateLibrary
                .Where(t => t != clicked.Template);

            this.TemplatePreviews.Clear();

            foreach (ZoneTemplateViewModel template in others)
            {
                var vm = new ZoneTemplatePreviewViewModel(template, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
                this.AttachTemplatePreviewEventHandlers(vm);
                this.TemplatePreviews.Add(vm);
            }

            this.ShowSelectedMonitorOverlay();

            this.HighlightedPreview = null;

            this.editCurrentMonitorLayoutCommand?.NotifyCanExecuteChanged();
        }

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
            templateCopy.IsDefault = false;

            // Create a view model for the new template and add to the library.
            ZoneTemplateViewModel templateVm = new ZoneTemplateViewModel(templateCopy);
            this.TemplateLibrary.Add(templateVm);

            // Create a preview view model and add it to the previews.
            var previewVm = new ZoneTemplatePreviewViewModel(templateVm, this.selectedMonitor.MonitorWidth, this.selectedMonitor.MonitorHeight);
            this.AttachTemplatePreviewEventHandlers(previewVm);
            this.TemplatePreviews.Add(previewVm);

            // Scroll the template previews to reveal the newly added template.
            this.RequestTemplateLibraryScroll?.Invoke();
        }

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

            // Remove from the template library.
            var libraryTemplate = this.TemplateLibrary.First(template => vm.Template == template);
            if (libraryTemplate != null)
            {
                this.TemplateLibrary.Remove(libraryTemplate);
            }

            // Remove from the previews.
            this.TemplatePreviews.Remove(vm);
        }

        private void AttachTemplatePreviewEventHandlers(ZoneTemplatePreviewViewModel viewModel)
        {
            viewModel.Clicked += this.OnTemplateClicked;
            viewModel.DoubleClicked += this.OnTemplateDoubleClicked;
            viewModel.CopyRequested += this.OnCopyRequested;
            viewModel.DeleteRequested += this.OnDeleteRequested;
        }

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
                    this.HighlightedPreview.Template.UpdateFromTemplate(editor.ToZoneTemplate());
                }
            };

            OverlayManager.HideOverlay();

            LayoutEditorManager.OpenEditor(
                this.HighlightedPreview.Template.Template,
                this.selectedMonitor.MonitorInfo,
                onCloseCallback: callback);
        }

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

            OverlayManager.HideOverlay();

            LayoutEditorManager.OpenEditor(
                this.selectedMonitor.SelectedTemplate.Template,
                this.selectedMonitor.MonitorInfo,
                onCloseCallback: callback);
        }

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
                }
            };

            OverlayManager.HideOverlay();

            LayoutEditorManager.OpenEditor(
                newLayout,
                this.selectedMonitor.MonitorInfo,
                onCloseCallback: callback);
        }
    }
}
