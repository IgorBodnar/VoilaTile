namespace VoilaTile.Common.Helpers
{
    using System.Windows.Input;

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
    }
}

