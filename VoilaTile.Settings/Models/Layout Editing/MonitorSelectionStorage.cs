namespace VoilaTile.Settings.Models
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Settings.DTO;

    /// <summary>
    /// The storage handler for monitor template selection.
    /// </summary>
    public static class MonitorSelectionStorage
    {
        /// <summary>
        /// The json serializer options.
        /// </summary>
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Saves our updates the monitor template selection in a file.
        /// </summary>
        /// <param name="selectionCollection">The selection collection dto representing the template selection per monitor.</param>
        public static void SaveOrUpdateSelection(SelectionCollectionDTO selectionCollection)
        {
            var filePath = AppPaths.SelectionPath;

            // Ensure target directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Merge existing and updated monitor layouts.
            SelectionCollectionDTO existing = File.Exists(filePath)
                ? JsonSerializer.Deserialize<SelectionCollectionDTO>(File.ReadAllText(filePath), Options) ?? new SelectionCollectionDTO()
                : new SelectionCollectionDTO();

            // Build map of existing monitors.
            var existingById = existing.Selections.ToDictionary(m => m.MonitorID);

            foreach (var selection in selectionCollection.Selections)
            {
                existingById[selection.MonitorID] = selection; // Overwrite or add
            }

            var merged = new SelectionCollectionDTO
            {
                Selections = existingById.Values.ToList()
            };

            File.WriteAllText(filePath, JsonSerializer.Serialize(merged, Options));
        }

        /// <summary>
        /// Loads the monitor template selection for a file.
        /// </summary>
        /// <returns>A selection collection dto representing the template selection per montior.</returns>
        public static SelectionCollectionDTO LoadSelection()
        {
            var filePath = AppPaths.SelectionPath;

            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<SelectionCollectionDTO>(File.ReadAllText(filePath), Options) ?? new SelectionCollectionDTO()
                : new SelectionCollectionDTO();
        }
    }
}
