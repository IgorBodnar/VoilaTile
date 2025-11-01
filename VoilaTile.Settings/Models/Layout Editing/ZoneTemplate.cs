using VoilaTile.Settings.DTO;

namespace VoilaTile.Settings.Models
{
    /// <summary>
    /// The data class encapsulating the zone template information using dividers.
    /// </summary>
    public class ZoneTemplate : ICloneable
    {
        /// <summary>
        /// The name of the zone template.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The value indicating whether the template was added by the user.
        /// </summary>
        public bool IsUserAdded { get; set; } = true;

        /// <summary>
        /// List of dividers that define the zone layout.
        /// </summary>
        public List<DividerModel> Dividers { get; set; } = new();

        /// <summary>
        /// Converts the model to a DTO.
        /// </summary>
        /// <returns>The template DTO initialized from the model.</returns>
        public TemplateDTO ToDTO()
        {
            return new TemplateDTO
            {
                Name = this.Name,
                Dividers = this.Dividers.Select(x => x.ToDTO()).ToList(),
            };
        }

        /// <inheritdoc/>
        public object Clone()
        {
            ZoneTemplate clone = new ZoneTemplate()
            {
                Name = this.Name,
                IsUserAdded = this.IsUserAdded,
            };

            foreach (var item in this.Dividers)
            {
                clone.Dividers.Add((DividerModel)item.Clone());
            }

            return clone;
        }
    }
}

