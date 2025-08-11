using VoilaTile.Configurator.Enumerations;

namespace VoilaTile.Configurator.Interfaces
{
    public interface IDialogService
    {
        Task<(DialogDecision Result, DialogViewModelBase ViewModel)> ShowAsync(DialogViewModelBase viewModel);
    }

}
