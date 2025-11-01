namespace VoilaTile.Common.Helpers
{
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Centralized defaults for system and application settings.
    /// </summary>
    public static class Defaults
    {
        /// <summary>
        /// The standard DPI used by Windows for 1:1 scaling.
        /// </summary>
        public const uint StandardDpi = 96;

        /// <summary>
        /// Default seed for hint generation (lowercase, unique chars).
        /// </summary>
        public const string DefaultSeed = "asdfghjklqwertyuiop";

        /// <summary>
        /// Default Snap shortcut key (combined with Win+Shift).
        /// </summary>
        public const Key DefaultSnapShortcutKey = Key.Space;

        /// <summary>
        /// Default Quick Grab shortcut key (combined with Win+Shift).
        /// </summary>
        public const Key DefaultQuickGrabShortcutKey = Key.J;

        /// <summary>
        /// Default Power Grab shortcut key (combined with Win+Shift).
        /// </summary>
        public const Key DefaultPowerGrabShortcutKey = Key.L;

        /// <summary>
        /// Default custom accent color used when theme accent mode is set to custom.
        /// </summary>
        public static readonly Color DefaultAccentColor = Color.FromRgb(0xDC, 0x14, 0x3C);

        /// <summary>
        /// Gets the default accent color in #RRGGBB format for serialization.
        /// </summary>
        public static string DefaultAccentHex => "#DC143C";
    }
}

