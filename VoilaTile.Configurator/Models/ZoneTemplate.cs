using VoilaTile.Configurator.DTO;

namespace VoilaTile.Configurator.Models
{
    /// <summary>
    /// The data class encapsulating the zone template information using dividers.
    /// </summary>
    public class ZoneTemplate : ICloneable
    {
        public string Name { get; set; } = string.Empty;

        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// List of dividers that define the zone layout.
        /// </summary>
        public List<DividerModel> Dividers { get; set; } = new();

        public TemplateDTO ToDTO()
        {
            return new TemplateDTO
            {
                Name = this.Name,
                Dividers = this.Dividers.Select(x => x.ToDTO()).ToList(),
            };
        }

        public object Clone()
        {
            ZoneTemplate clone = new ZoneTemplate()
            {
                Name = this.Name,
                IsDefault = this.IsDefault,
            };

            foreach (var item in this.Dividers)
            {
                clone.Dividers.Add((DividerModel)item.Clone());
            }

            return clone;
        }
    }
}

