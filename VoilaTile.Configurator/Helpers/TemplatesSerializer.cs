namespace VoilaTile.Configurator.Helpers
{
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Configurator.DTO;

    public static class TemplatesSerializer
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static void SaveTemplates(string filePath, TemplateCollectionDTO templateCollection)
        {
            // Ensure target directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to the json.
            File.WriteAllText(filePath, JsonSerializer.Serialize(templateCollection, Options));
        }

        public static TemplateCollectionDTO LoadTemplates(string filePath)
        {
            TemplateCollectionDTO templateCollection = File.Exists(filePath)
                ? JsonSerializer.Deserialize<TemplateCollectionDTO>(File.ReadAllText(filePath), Options) ?? new TemplateCollectionDTO()
                : new TemplateCollectionDTO();

            return templateCollection;
        }
    }
}
