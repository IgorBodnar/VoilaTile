namespace VoilaTile.Common.DTO
{
    /// <summary>
    /// The DTO representing application settings.
    /// </summary>
    public class SettingsDTO
    {
        /// <summary>
        /// Gets or sets the seed string used in hint label generation.
        /// </summary>
        public string Seed { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected shortcut key for snapping functionality.
        /// </summary>
        public string SelectedSnapShortcutKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the shortcut key selected for quick grab functionality.
        /// </summary>
        public string SelectedQuickGrabShortcutKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the shortcut key selected for the Power Grab feature.
        /// </summary>
        public string SelectedPowerGrabShortcutKey { get; set; } = string.Empty;
    }
}
