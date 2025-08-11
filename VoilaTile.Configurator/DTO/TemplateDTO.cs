namespace VoilaTile.Configurator.DTO
{
    using VoilaTile.Configurator.Models;

    public class TemplateDTO
    {
        public string Name { get; set; }
        public List<DividerDTO> Dividers { get; set; } = new();

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
