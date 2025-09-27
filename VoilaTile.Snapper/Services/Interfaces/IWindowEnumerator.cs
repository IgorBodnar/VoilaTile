namespace VoilaTile.Snapper.Services
{
    using System.Collections.Generic;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Provides one-shot snapshots of top-level windows.
    /// </summary>
    public interface IWindowEnumerator
    {
        #region Methods

        /// <summary>
        /// Enumerates windows according to the provided options.
        /// </summary>
        /// <param name="options">The query options.</param>
        /// <returns>A read-only list of window entries.</returns>
        IReadOnlyList<WindowEntry> Snapshot(WindowQueryOptions options);

        #endregion
    }
}
