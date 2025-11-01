namespace VoilaTile.Settings.Models
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;

    /// <summary>
    /// The monitor layout storage helper.
    /// </summary>
    public static class MonitorLayoutStorage
    {
        /// <summary>
        /// The JSON serializer options.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Saves or updates the active monitor layouts.
        /// </summary>
        /// <param name="layoutCollection"></param>
        public static void SaveOrUpdateLayouts(LayoutCollectionDTO layoutCollection)
        {
            var filePath = AppPaths.ActiveLayoutsPath;

            // Ensure target directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Merge existing and updated monitor layouts.
            LayoutCollectionDTO existing = File.Exists(filePath)
                ? JsonSerializer.Deserialize<LayoutCollectionDTO>(File.ReadAllText(filePath), Options) ?? new LayoutCollectionDTO()
                : new LayoutCollectionDTO();

            // Build map of existing monitors.
            var existingById = existing.Monitors.ToDictionary(m => m.MonitorID);

            foreach (var layout in layoutCollection.Monitors)
            {
                existingById[layout.MonitorID] = layout; // Overwrite or add
            }

            var merged = new LayoutCollectionDTO
            {
                Monitors = existingById.Values.ToList()
            };

            File.WriteAllText(filePath, JsonSerializer.Serialize(merged, Options));
        }
    }
}
