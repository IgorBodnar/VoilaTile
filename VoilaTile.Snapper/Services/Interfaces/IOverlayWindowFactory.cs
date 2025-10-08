namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Windows;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// A factory for creting overlay windows from their respective view models.
    /// </summary>
    public interface IOverlayWindowFactory
    {
        Window Create(IOverlayViewModel viewModel);
        IOverlayWindowFactory Register<TViewModel>(Func<TViewModel, Window> ctor)
            where TViewModel : IOverlayViewModel;
    }
}
