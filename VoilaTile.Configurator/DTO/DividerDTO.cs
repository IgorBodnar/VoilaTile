using VoilaTile.Configurator.Models;

namespace VoilaTile.Configurator.DTO
{
    public class DividerDTO
    {
        public bool IsVertical { get; set; }
        public double Position { get; set; }
        public double BoundStart { get; set; }
        public double BoundEnd { get; set; }

        public DividerModel ToModel()
        {
            return new DividerModel()
            {
                IsVertical = this.IsVertical,
                Position = this.Position,
                BoundStart = this.BoundStart,
                BoundEnd = this.BoundEnd,
            };
        }
    }
}
