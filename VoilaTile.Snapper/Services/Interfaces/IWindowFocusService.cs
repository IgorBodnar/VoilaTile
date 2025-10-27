namespace VoilaTile.Snapper.Services
{
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Provides robust foreground activation for windows.
    /// </summary>
    public interface IWindowFocusService
    {
        #region Methods

        /// <summary>
        /// Attempts to restore and focus the specified window.
        /// </summary>
        /// <param name="id">The window identifier.</param>
        /// <returns><c>true</c> if focus succeeded; otherwise <c>false</c>.</returns>
        bool TryFocus(WindowId id);

        #endregion
    }
}
