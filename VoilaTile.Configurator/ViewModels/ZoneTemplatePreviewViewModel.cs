namespace VoilaTile.Configurator.ViewModels
{
    using System;
    using System.Windows;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.Mvvm.Input;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Views;

    /// <summary>
    /// ViewModel for a zone template, enabling projection of normalized zones into specific pixel bounds.
    /// </summary>
    public partial class ZoneTemplatePreviewViewModel : ObservableObject
    {
        #region Fields

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isHighlighted;

        [ObservableProperty]
        private bool isDefault;

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
            this.IsDefault = template.Template.IsDefault;
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
        /// Gets the name of the template.
        /// </summary>
        public string Name
        {
            get => this.name;
            private set => this.SetProperty(ref this.name, value);
        }

        /// <summary>
        /// Gets the aspect ratio (width divided by height) of the monitor.
        /// </summary>
        public double AspectRatio { get; }

        #endregion Properties

        #region Methods

        public void UpdateFromTemplate(ZoneTemplateViewModel template)
        {
            this.Template = template;
            this.Name = template.Name;
            this.IsDefault = template.Template.IsDefault;
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
