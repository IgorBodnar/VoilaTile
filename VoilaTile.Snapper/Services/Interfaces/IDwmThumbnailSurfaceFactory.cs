namespace VoilaTile.Snapper.Services
{
    using System;

    /// <summary>
    /// Creates DWM thumbnail surfaces bound to a destination child HWND.
    /// </summary>
    public interface IDwmThumbnailSurfaceFactory
    {
        #region Methods

        /// <summary>
        /// Creates a thumbnail surface that can host multiple source window thumbnails.
        /// </summary>
        /// <param name="destinationHwnd">The destination child window handle.</param>
        /// <returns>The created thumbnail surface.</returns>
        IThumbnailSurface Create(IntPtr destinationHwnd);

        #endregion
    }
}
