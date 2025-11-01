namespace VoilaTile.Settings.Services
{
    using System.Threading.Tasks;
    using System.Windows;
    using VoilaTile.Settings.Enumerations;
    using VoilaTile.Settings.Interfaces;
    using VoilaTile.Settings.ViewModels;
    using VoilaTile.Settings.Views;

    /// <summary>
    /// Shows dialogs using a common shell. The shell auto-sizes to the provided body.
    /// The body is provided as a ViewModel and is mapped to a View via DataTemplates.
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <inheritdoc/>
        public async Task<(DialogDecision Result, DialogViewModelBase ViewModel)> ShowAsync(DialogViewModelBase viewModel)
        {
            var window = new BaseDialogView
            {
                DataContext = viewModel,
                Owner = Application.Current.MainWindow,
                Body = viewModel,
            };

            // Display modally.
            window.ShowDialog();

            // ShowDialog blocks until closed; the TCS will already be completed.
            var result = await window.CompletionSource.Task;
            return (result, viewModel);
        }
    }
}

