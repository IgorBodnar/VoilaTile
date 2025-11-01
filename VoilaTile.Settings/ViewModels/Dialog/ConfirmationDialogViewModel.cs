namespace VoilaTile.Settings.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// The view model for a confirmation dialog.
    /// </summary>
    public partial class ConfirmationDialogViewModel : DialogViewModelBase
    {
        /// <summary>
        /// The text of the confirmation dialog.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private string text;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfirmationDialogViewModel"/> class.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="text">The confirmation text.</param>
        public ConfirmationDialogViewModel(string title, string text)
        {
            this.Title = title;
            this.Text = text;
            this.PositiveResponseText = "Yes";
            this.NegativeResponseText = "No";
        }
    }

}
