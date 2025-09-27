namespace VoilaTile.Snapper.Services
{
    /// <summary>
    /// Options controlling window enumeration behavior.
    /// </summary>
    public sealed class WindowQueryOptions
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether tool windows should be included.
        /// </summary>
        public bool IncludeToolWindows { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether minimized windows should be included.
        /// </summary>
        public bool IncludeMinimized { get; init; } = false;

        /// <summary>
        /// Gets a value indicating whether cloaked windows should be excluded.
        /// </summary>
        public bool ExcludeCloaked { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether only windows on the current virtual desktop should be returned.
        /// </summary>
        public bool CurrentDesktopOnly { get; init; } = true;

        /// <summary>
        /// Gets the monitor handle filter (optional; null means all monitors).
        /// </summary>
        public nint? MonitorHandle { get; init; }

        #endregion
    }
}
