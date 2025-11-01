namespace VoilaTile.Settings.Models
{
    using VoilaTile.Common.DTO;
    using VoilaTile.Settings.DTO;
    using VoilaTile.Settings.Models;
    using VoilaTile.Settings.ViewModels;

    /// <summary>
    /// Helper for mapping template collections to and from template collection DTOs.
    /// </summary>
    public static class TemplatesMapper
    {
        /// <summary>
        /// Maps a template collection to a dto.
        /// </summary>
        /// <param name="templates">The template collection.</param>
        /// <returns>The template collection dto.</returns>
        public static TemplateCollectionDTO MapToDTO(List<ZoneTemplate> templates)
        {
            return new TemplateCollectionDTO()
            {
                Templates = templates.Select(t => t.ToDTO()).ToList(),
            };
        }

        /// <summary>
        /// Maps a template collection dto into a list of template models.
        /// </summary>
        /// <param name="templateCollection">The template collection dto.</param>
        /// <returns>A list of template models.</returns>
        public static List<ZoneTemplate> MapToModels(TemplateCollectionDTO templateCollection)
        {
            return templateCollection.Templates.Select(t => t.ToModel()).ToList();
        }
    }
}
