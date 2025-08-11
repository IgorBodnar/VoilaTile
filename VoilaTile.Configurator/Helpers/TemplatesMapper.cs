namespace VoilaTile.Configurator.Helpers
{
    using VoilaTile.Common.DTO;
    using VoilaTile.Configurator.DTO;
    using VoilaTile.Configurator.Models;
    using VoilaTile.Configurator.Records;
    using VoilaTile.Configurator.ViewModels;

    public static class TemplatesMapper
    {
        public static TemplateCollectionDTO MapToDTO(List<ZoneTemplate> templates)
        {
            return new TemplateCollectionDTO()
            {
                Templates = templates.Select(t => t.ToDTO()).ToList(),
            };
        }

        public static List<ZoneTemplate> MapToModels(TemplateCollectionDTO templateCollection)
        {
            return templateCollection.Templates.Select(t => t.ToModel()).ToList();
        }
    }
}
