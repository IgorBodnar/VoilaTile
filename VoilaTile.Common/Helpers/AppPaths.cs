namespace VoilaTile.Common.Helpers
{
    using System;
    using System.IO;

    /// <summary>
    /// Provides default file and directory paths for the application.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>
        /// Gets the application data root directory.
        /// </summary>
        public static string AppDataRoot { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoilaTile");

        /// <summary>
        /// Gets the input settings file path.
        /// </summary>
        public static string InputSettingsPath => Path.Combine(AppDataRoot, "settings.json");

        /// <summary>
        /// Gets the templates file path.
        /// </summary>
        public static string TemplatesPath => Path.Combine(AppDataRoot, "templates.json");

        /// <summary>
        /// Gets the selection file path.
        /// </summary>
        public static string SelectionPath => Path.Combine(AppDataRoot, "selection.json");

        /// <summary>
        /// Gets the active layouts file path.
        /// </summary>
        public static string ActiveLayoutsPath => Path.Combine(AppDataRoot, "active_layouts.json");

        /// <summary>
        /// Gets the theme settings file path.
        /// </summary>
        public static string ThemeSettingsPath => Path.Combine(AppDataRoot, "theme.json");
    }
}

