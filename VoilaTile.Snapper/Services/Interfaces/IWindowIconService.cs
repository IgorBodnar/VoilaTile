namespace VoilaTile.Snapper.Services
{
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Resolves and caches icons for windows and processes.
    /// </summary>
    public interface IWindowIconService
    {
        #region Methods

        /// <summary>
        /// Gets an icon for the specified window, if available.
        /// </summary>
        /// <param name="entry">The window entry.</param>
        /// <returns>An <see cref="IconSource"/> or <c>null</c> if none is available.</returns>
        IconSource? GetIcon(WindowEntry entry);

        /// <summary>
        /// Clears any internal caches.
        /// </summary>
        void ClearCache();

        #endregion
    }
}
