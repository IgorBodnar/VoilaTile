namespace VoilaTile.Settings.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// The view model for the rename dialog.
    /// </summary>
    public partial class RenameDialogViewModel : DialogViewModelBase
    {
        /// <summary>
        /// The name to rename to.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="RenameDialogViewModel"/> class.
        /// </summary>
        /// <param name="currentName">The current name to be renamed.</param>
        public RenameDialogViewModel(string currentName)
        {
            Title = "Rename Template";
            Name = currentName;
            PositiveResponseText = "Save";
            NegativeResponseText = "Cancel";
        }
    }

}
