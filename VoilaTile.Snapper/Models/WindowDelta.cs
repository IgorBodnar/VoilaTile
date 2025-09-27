namespace VoilaTile.Snapper.Models
{
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Describes a change to a window observed via system events.
    /// </summary>
    public sealed class WindowDelta
    {
        #region Properties

        /// <summary>
        /// Gets or sets the window identifier.
        /// </summary>
        public WindowId Id { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the window became visible (true) or hidden (false).
        /// Null means no visibility change.
        /// </summary>
        public bool? BecameVisible { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the window was destroyed.
        /// </summary>
        public bool? Destroyed { get; set; }

        /// <summary>
        /// Gets or sets the new bounds in pixels, if changed.
        /// </summary>
        public RectPx? NewBoundsPx { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the window is minimized, if changed.
        /// </summary>
        public bool? Minimized { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the window is cloaked, if changed.
        /// </summary>
        public bool? Cloaked { get; set; }

        /// <summary>
        /// Gets or sets the new window title, if changed.
        /// </summary>
        public string? NewTitle { get; set; }

        #endregion
    }
}
