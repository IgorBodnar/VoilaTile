namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// Type-based constructor map. Create windows for any overlay VM.
    /// </summary>
    public sealed class OverlayWindowFactory : IOverlayWindowFactory
    {
        private readonly Dictionary<Type, Func<IOverlayViewModel, Window>> map = new();

        /// <inheritdoc/>
        public IOverlayWindowFactory Register<TViewModel>(Func<TViewModel, Window> ctor)
            where TViewModel : IOverlayViewModel
        {
            if (ctor is null) throw new ArgumentNullException(nameof(ctor));
            map[typeof(TViewModel)] = vm => ctor((TViewModel)vm);
            return this;
        }

        /// <inheritdoc/>
        public Window Create(IOverlayViewModel viewModel)
        {
            if (viewModel is null) throw new ArgumentNullException(nameof(viewModel));
            var t = viewModel.GetType();
            if (!map.TryGetValue(t, out var ctor))
            {
                throw new InvalidOperationException(
                    $"No overlay window registration for VM type '{t.FullName}'.");
            }
            return ctor(viewModel);
        }
    }
}

