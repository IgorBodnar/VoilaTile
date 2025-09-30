namespace VoilaTile.Snapper.Services
{
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Manages DWM thumbnails for multiple source windows.
    /// </summary>
    public interface IThumbnailSurface : IDisposable
    {
        #region Methods

        bool TryGetSourceSize(WindowId sourceId, out SizePx size);


        /// <summary>
        /// Ensures that a thumbnail for the specified source window is registered.
        /// </summary>
        /// <param name="sourceId">The source window identifier.</param>
        void EnsureRegistered(WindowId sourceId);

        /// <summary>
        /// Updates the destination rectangle for a source window thumbnail.
        /// </summary>
        /// <param name="sourceId">The source window identifier.</param>
        /// <param name="destinationPx">The destination rectangle in destination client pixels.</param>
        /// <param name="opacity">Optional opacity (0–255). Defaults to 255.</param>
        void UpdateRect(WindowId sourceId, RectPx destinationPx, byte opacity = 255);

        /// <summary>
        /// Removes the thumbnail for a source window if present.
        /// </summary>
        /// <param name="sourceId">The source window identifier.</param>
        void Remove(WindowId sourceId);

        #endregion
    }
}
