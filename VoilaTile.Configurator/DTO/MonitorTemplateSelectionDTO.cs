namespace VoilaTile.Configurator.DTO
{
    /// <summary>
    /// The DTO representing the monitor template selection.
    /// </summary>
    public class MonitorTemplateSelectionDTO
    {
        /// <summary>
        /// Gets or sets the monitor id.
        /// </summary>
        public required string MonitorID { get; set; }

        /// <summary>
        /// Gets or sets the template name.
        /// </summary>
        public required string TemplateName { get; set; }
    }
}
