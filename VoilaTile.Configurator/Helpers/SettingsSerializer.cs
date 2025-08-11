namespace VoilaTile.Configurator.Helpers
{
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Common.DTO;

    public static class SettingsSerializer
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static void SaveSettings(string filePath, SettingsDTO settings)
        {
            // Ensure target directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to the json.
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings, Options));
        }

        public static SettingsDTO LoadSettings(string filePath)
        {
            SettingsDTO settings = File.Exists(filePath)
                ? JsonSerializer.Deserialize<SettingsDTO>(File.ReadAllText(filePath), Options) ?? new SettingsDTO()
                : new SettingsDTO();

            return settings;
        }
    }
}
