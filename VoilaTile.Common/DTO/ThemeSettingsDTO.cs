namespace VoilaTile.Common.DTO
{
    /// <summary>
    /// Serializable theme settings DTO used to persist and restore the application's theme selection.
    /// </summary>
    public sealed class ThemeSettingsDTO
    {
        /// <summary>
        /// Gets or sets the theme mode string ("Light", "Dark", "System").
        /// </summary>
        public string ThemeMode { get; set; } = "System";

        /// <summary>
        /// Gets or sets the accent mode string ("Windows", "Custom").
        /// </summary>
        public string AccentMode { get; set; } = "Windows";

        /// <summary>
        /// Gets or sets the custom accent color in #RRGGBB format (used only when AccentMode == "Custom").
        /// </summary>
        public string? CustomAccentHex { get; set; } = "#4C8CFF";
    }
}

