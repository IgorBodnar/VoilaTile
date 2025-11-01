namespace VoilaTile.Settings.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// The view model for an information dialog.
    /// </summary>
    public partial class InformationDialogViewModel : DialogViewModelBase
    {
        /// <summary>
        /// The information text.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks> 
        [ObservableProperty]
        private string info;

        /// <summary>
        /// Initializes a new instance of the <see cref="InformationDialogViewModel"/> class.
        /// </summary>
        /// <param name="title">The dialog title.</param>
        /// <param name="info">The information text.</param>
        public InformationDialogViewModel(string title, string info)
        {
            this.Title = title;
            this.Info = info;
            this.PositiveResponseText = "OK";
            this.ShowNegativeResponse = false;
        }
    }
}
