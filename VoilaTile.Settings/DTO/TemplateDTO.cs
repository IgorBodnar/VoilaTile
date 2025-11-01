namespace VoilaTile.Settings.DTO
{
    using VoilaTile.Settings.Models;

    /// <summary>
    /// The DTO for a template.
    /// </summary>
    public class TemplateDTO
    {
        /// <summary>
        /// Gets or sets the name of the template.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// The collection of dividers in the template.
        /// </summary>
        public List<DividerDTO> Dividers { get; set; } = new();

        /// <summary>
        /// Transforms the DTO into a model.
        /// </summary>
        /// <returns>The zome template model intialized from the DTO.</returns>
        public ZoneTemplate ToModel()
        {
            return new ZoneTemplate()
            {
                Name = this.Name,
                Dividers = this.Dividers.Select(d => d.ToModel()).ToList(),
            };
        }
    }
}
