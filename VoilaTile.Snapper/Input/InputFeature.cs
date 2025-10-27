namespace VoilaTile.Snapper.Input
{
    /// <summary>
    /// Represents the current input feature of the application.
    /// </summary>
    public enum InputFeature
    {
        /// <summary>
        /// The zone selection an snapping of the focused window.
        /// </summary>
        Snap,

        /// <summary>
        /// The global window switcher with thumbnails.
        /// </summary>
        PowerGrab,

        /// <summary>
        /// The hint based top level window switcher.
        /// </summary>
        QuickGrab,
    }
}
