namespace VoilaTile.Snapper.Helpers
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a group of <see cref="IDisposable"/> objects that are disposed together.
    /// </summary>
    internal sealed class CompositeDisposable : IDisposable
    {
        private readonly List<IDisposable> disposables = new();
        private bool disposed;

        /// <summary>
        /// Adds a disposable object to the collection.
        /// </summary>
        /// <param name="disposable">The disposable object to add.</param>
        public void Add(IDisposable disposable)
        {
            if (this.disposed)
            {
                disposable.Dispose();
                return;
            }

            this.disposables.Add(disposable);
        }

        /// <summary>
        /// Disposes all contained disposables in reverse order of addition.
        /// </summary>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            for (int i = this.disposables.Count - 1; i >= 0; i--)
            {
                try
                {
                    this.disposables[i].Dispose();
                }
                catch
                {
                    // Intentionally swallow exceptions to avoid breaking composite disposal.
                }
            }

            this.disposables.Clear();
        }
    }
}

