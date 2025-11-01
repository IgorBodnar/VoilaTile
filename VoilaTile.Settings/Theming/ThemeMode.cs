namespace VoilaTile.Settings.Theming
{
    /// <summary>
    /// Specifies how the application selects its base theme.
    /// </summary>
    public enum ThemeMode
    {
        /// <summary>
        /// Follows the Windows Apps theme preference (light or dark).
        /// </summary>
        System,

        /// <summary>
        /// Forces the application to use the light theme.
        /// </summary>
        Light,

        /// <summary>
        /// Forces the application to use the dark theme.
        /// </summary>
        Dark,
    }
}

