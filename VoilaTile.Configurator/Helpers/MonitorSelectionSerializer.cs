namespace VoilaTile.Configurator.Helpers
{
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Configurator.DTO;

    public static class MonitorSelectionSerializer
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void SaveOrUpdateSelection(string filePath, SelectionCollectionDTO selectionCollection)
        {
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

        public static SelectionCollectionDTO LoadSelection(string filePath)
        {
            SelectionCollectionDTO dto = File.Exists(filePath)
                ? JsonSerializer.Deserialize<SelectionCollectionDTO>(File.ReadAllText(filePath), Options) ?? new SelectionCollectionDTO()
                : new SelectionCollectionDTO();

            return dto;
        }
    }
}
