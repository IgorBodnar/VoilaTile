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
        public bool IncludeToolWindows { get; init; } = false;

        /// <summary>
        /// Gets a value indicating whether minimized windows should be included.
        /// </summary>
        public bool IncludeMinimized { get; init; } = true;

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

        /// <summary>
        /// Apply Alt-Tab-like filtering (owned-window suppression, no tool windows, etc.).
        /// When true, it overrides conflicting flags (e.g., forces IncludeToolWindows=false).
        /// </summary>
        public bool AltTabOnly { get; init; } = true;

        /// <summary>
        /// Minimum visible size in pixels (guards against 0x0 or tiny helper windows).
        /// </summary>
        public int MinWidth { get; init; } = 10;
        public int MinHeight { get; init; } = 10;

        #endregion
    }
}
