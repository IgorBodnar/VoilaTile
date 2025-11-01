namespace VoilaTile.Settings.DTO
{
    /// <summary>
    /// The DTO for a collection of monitor template selections.
    /// </summary>
    public class SelectionCollectionDTO
    {
        /// <summary>
        /// The collection of monitor template selections.
        /// </summary>
        public List<MonitorTemplateSelectionDTO> Selections { get; set; } = new();
    }
}
