namespace VoilaTile.Snapper.Layout
{
    using System.Collections.Generic;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Models;

    /// <summary>
    /// Represents a resolved layout for a single monitor including its tiles in absolute screen coordinates.
    /// </summary>
    public class ZoneLayoutModel
    {
        public MonitorInfo Monitor { get; }

        public MonitorLayoutDTO LayoutDTO { get; }

        public List<ResolvedTileModel> Tiles { get; }

        public ZoneLayoutModel(MonitorInfo monitor, MonitorLayoutDTO layoutDTO, List<ResolvedTileModel> tiles)
        {
            Monitor = monitor;
            LayoutDTO = layoutDTO;
            Tiles = tiles;
        }
    }
}

