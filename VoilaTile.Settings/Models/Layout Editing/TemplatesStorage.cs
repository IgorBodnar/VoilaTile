namespace VoilaTile.Settings.Models
{
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Settings.DTO;

    /// <summary>
    /// The storage handler for template collection.
    /// </summary>
    public static class TemplatesStorage
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
        /// Saves the templates to the specified file path.
        /// </summary>
        /// <param name="templateCollection">The template collection DTO.</param>
        public static void SaveTemplates(TemplateCollectionDTO templateCollection)
        {
            var filePath = AppPaths.TemplatesPath;

            // Ensure target directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to the json.
            File.WriteAllText(filePath, JsonSerializer.Serialize(templateCollection, Options));
        }

        /// <summary>
        /// Loads the template collection from a file.
        /// </summary>
        /// <returns>A template collection dto.</returns>
        public static TemplateCollectionDTO LoadTemplates()
        {
            var filePath = AppPaths.TemplatesPath;

            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<TemplateCollectionDTO>(File.ReadAllText(filePath), Options) ?? new TemplateCollectionDTO()
                : new TemplateCollectionDTO();
        }
    }
}
