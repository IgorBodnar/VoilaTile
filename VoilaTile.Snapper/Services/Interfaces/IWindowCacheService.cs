namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Warm cache for window metadata, icons, and source sizes with a sliding TTL.
    /// </summary>
    public interface IWindowCacheService : IDisposable
    {
        #region Methods

        /// <summary>
        /// Gets a snapshot of windows and a version token representing the cache state.
        /// </summary>
        /// <param name="options">The query options.</param>
        /// <returns>A tuple containing the window list and a version.</returns>
        (IReadOnlyList<WindowEntry> entries, long version) GetSnapshot(WindowQueryOptions options);

        /// <summary>
        /// Pins the cache for an active session (prevents eviction and hooks live events).
        /// </summary>
        /// <returns>A disposable token that unpins on dispose.</returns>
        IDisposable PinForSession();

        /// <summary>
        /// Attempts to get a single window entry by id.
        /// </summary>
        /// <param name="id">The window identifier.</param>
        /// <param name="entry">The resulting entry if found.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        bool TryGet(WindowId id, out WindowEntry entry);

        /// <summary>
        /// Forces invalidation of the cache.
        /// </summary>
        void InvalidateAll();

        #endregion
    }
}
