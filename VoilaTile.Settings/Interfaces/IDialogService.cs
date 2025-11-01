namespace VoilaTile.Settings.Interfaces
{
    using VoilaTile.Settings.Enumerations;
    using VoilaTile.Settings.ViewModels;

    /// <summary>
    /// The dialog service interface.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a dialog asynchronously.
        /// </summary>
        /// <param name="viewModel">The dialog view model inheriting from <see cref="DialogViewModelBase"/>.</param>
        /// <returns>The dialog result and the view model updated in the process.</returns>
        Task<(DialogDecision Result, DialogViewModelBase ViewModel)> ShowAsync(DialogViewModelBase viewModel);
    }

}
