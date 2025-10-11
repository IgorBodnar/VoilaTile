namespace VoilaTile.Snapper.Input
{
    using System;

    /// <summary>
    /// Represents a feature/mode that wants to receive global keyboard input.
    /// Attaches event handlers and returns an <see cref="IDisposable"/> to unhook them.
    /// </summary>
    internal interface IKeyboardControllable
    {
        /// <summary>
        /// Attaches keyboard handlers to the given <paramref name="listener"/>.
        /// </summary>
        /// <param name="listener">Global input listener.</param>
        /// <returns>Disposable that unhooks all attached handlers.</returns>
        IDisposable Attach(GlobalInputListener listener);
    }
}
