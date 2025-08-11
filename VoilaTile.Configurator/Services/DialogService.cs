namespace VoilaTile.Configurator.Services
{
    using System.Threading.Tasks;
    using System.Windows;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Interfaces;
    using VoilaTile.Configurator.ViewModels;
    using VoilaTile.Configurator.Views;

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

