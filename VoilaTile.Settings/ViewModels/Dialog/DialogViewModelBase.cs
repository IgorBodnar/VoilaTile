namespace VoilaTile.Settings.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// The base view model for dialog windows.
    /// </summary>
    public abstract partial class DialogViewModelBase : ObservableObject
    {
        /// <summary>
        /// The dialog title.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private string title = string.Empty;

        /// <summary>
        /// The value indicating whether to show the positive response button.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private bool showPositiveResponse = true;

        /// <summary>
        /// The value indicating whether to show the negative response button.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private bool showNegativeResponse = true;

        /// <summary>
        /// The positive response button text.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private string positiveResponseText = "OK";

        /// <summary>
        /// The negative response button text.
        /// </summary>
        /// <remarks>
        /// Generates an observable property on build.
        /// </remarks>
        [ObservableProperty]
        private string negativeResponseText = "Cancel";
    }
}

