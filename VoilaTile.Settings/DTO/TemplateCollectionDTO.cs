namespace VoilaTile.Settings.DTO
{
    /// <summary>
    /// The DTO representing a collection of templates.
    /// </summary>
    public class TemplateCollectionDTO
    {
        /// <summary>
        /// The collection of templates.
        /// </summary>
        public List<TemplateDTO> Templates { get; set; } = new();
    }
}
