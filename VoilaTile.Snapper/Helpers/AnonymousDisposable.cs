namespace VoilaTile.Snapper.Helpers
{
    using System;

    /// <summary>
    /// Represents a disposable object that executes a specified action upon disposal.
    /// </summary>
    internal sealed class AnonymousDisposable : IDisposable
    {
        private Action? disposeAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnonymousDisposable"/> class.
        /// </summary>
        /// <param name="disposeAction">The action to invoke upon disposal.</param>
        public AnonymousDisposable(Action disposeAction)
        {
            this.disposeAction = disposeAction ?? throw new ArgumentNullException(nameof(disposeAction));
        }

        /// <summary>
        /// Disposes the object and executes the specified action once.
        /// </summary>
        public void Dispose()
        {
            var action = Interlocked.Exchange(ref this.disposeAction, null);
            action?.Invoke();
        }
    }
}

