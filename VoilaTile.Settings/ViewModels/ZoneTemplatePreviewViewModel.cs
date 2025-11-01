namespace VoilaTile.Settings.ViewModels
{
    using System;
    using System.Windows;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using VoilaTile.Settings.Enumerations;

    /// <summary>
    /// ViewModel for a zone template, enabling projection of normalized zones into specific pixel bounds.
    /// </summary>
    public partial class ZoneTemplatePreviewViewModel : ObservableObject
    {
        #region Fields

        /// <summary>
        /// The backing field for the IsSelected property generated on build.
        /// Represents whether the template is currently selected.
        /// </summary>
        [ObservableProperty]
        private bool isSelected;

        /// <summary>
        /// The backing field for the IsHighlighted property generated on build.
        /// Represents whether the template is currently highlighted.
        /// </summary>
        [ObservableProperty]
        private bool isHighlighted;

        /// <summary>
        /// The backing field for the IsUserAdded property generated on build.
        /// Represents whether the template was added by the user.
        /// </summary>
        [ObservableProperty]
        private bool isUserAdded;

        /// <summary>
        /// The backing field for the Name property generated on build.
        /// Represents the name of the template.
        /// </summary>
        [ObservableProperty]
        private string name;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ZoneTemplatePreviewViewModel"/> class.
        /// </summary>
        /// <param name="template">The associated <see cref="ZoneTemplateViewModel"/>.</param>
        /// <param name="monitorWidth">The width of the monitor in pixels.</param>
        /// <param name="monitorHeight">The height of the monitor in pixels.</param>
        public ZoneTemplatePreviewViewModel(ZoneTemplateViewModel template, double monitorWidth, double monitorHeight)
        {
            this.Template = template;
            this.Name = template.Name;
            this.AspectRatio = monitorWidth / monitorHeight;
            this.IsUserAdded = template.Template.IsUserAdded;
        }

        #endregion Fields

        #region Events

        /// <summary>
        /// Occurs when the template is clicked.
        /// </summary>
        public event EventHandler? Clicked;

        /// <summary>
        /// Occurs when the template is double clicked.
        /// </summary>
        public event EventHandler? DoubleClicked;

        /// <summary>
        /// Occurs when the presses on the copy button.
        /// </summary>
        public event EventHandler? CopyRequested;

        /// <summary>
        /// Occurs when the presses on the delete button.
        /// </summary>
        public event EventHandler? DeleteRequested;

        #endregion Events

        #region Properties

        /// <summary>
        /// Gets the associated <see cref="ZoneTemplateViewModel"/>.
        /// </summary>
        public ZoneTemplateViewModel Template { get; private set; }

        /// <summary>
        /// Gets the aspect ratio (width divided by height) of the monitor.
        /// </summary>
        public double AspectRatio { get; }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Updates the preview from the given template view model.
        /// </summary>
        /// <param name="template"></param>
        public void UpdateFromTemplate(ZoneTemplateViewModel template)
        {
            this.Template = template;
            this.Name = template.Name;
            this.IsUserAdded = template.Template.IsUserAdded;
        }

        /// <summary>
        /// Raises the <see cref="Clicked"/> event.
        /// </summary>
        public void RaiseClicked()
        {
            this.Clicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="DoubleClicked"/> event.
        /// </summary>
        public void RaiseDoubleClicked()
        {
            this.DoubleClicked?.Invoke(this, EventArgs.Empty);
        }

        #endregion Methods

        #region Commands

        /// <summary>
        /// Renames the zone template.
        /// </summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        [RelayCommand]
        public async Task RenameAsync()
        {
            var vm = new RenameDialogViewModel(this.Name);
            var (result, updatedVm) = await App.DialogService.ShowAsync(vm);

            if (result == DialogDecision.Positive && updatedVm is RenameDialogViewModel renameVm)
            {
                this.Name = renameVm.Name;
                this.Template.Name = renameVm.Name;
            }
        }

        /// <summary>
        /// Deletes the zone template.
        /// </summary>
        [RelayCommand]
        private void Delete()
        {
            this.DeleteRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Copies the zone template.
        /// </summary>
        [RelayCommand]
        private void Copy()
        {
            this.CopyRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion Commands
    }
}
