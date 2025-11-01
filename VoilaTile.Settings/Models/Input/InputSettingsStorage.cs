namespace VoilaTile.Settings.Models
{
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;

    /// <summary>
    /// The storage handler for input settings.
    /// </summary>
    public static class InputSettingsStorage
    {
        /// <summary>
        /// The JSON serializer options.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Saves the settings to the specified file path.
        /// </summary>
        /// <param name="settings">The input settings DTO.</param>
        public static void SaveSettings(SettingsDTO settings)
        {
            var filePath = AppPaths.InputSettingsPath;

            // Ensure target directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to the json.
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
        }

        /// <summary>
        /// Loads the input settings from a file.
        /// </summary>
        /// <returns>A settings dto.</returns>
        public static SettingsDTO LoadSettings()
        {
            var filePath = AppPaths.InputSettingsPath;

            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<SettingsDTO>(File.ReadAllText(filePath), Options) ?? new SettingsDTO()
                : new SettingsDTO();
        }
    }
}
