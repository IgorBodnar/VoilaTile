namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Models;

    public class CharacterPoolFactory
    {
        private string seed;

        public CharacterPoolFactory(string seedCharacters, List<MonitorInfo> monitors, string layoutFilePath)
        {
            this.SanitizeSeed(seedCharacters, monitors, layoutFilePath);
        }

        public CharacterPool Pool => new CharacterPool(this.seed);

        private void SanitizeSeed(string seedCharacters, List<MonitorInfo> monitors, string layoutFilePath)
        {
            // Load the existing monitor layout information.
            LayoutCollectionDTO existing = File.Exists(layoutFilePath)
            ? JsonSerializer.Deserialize<LayoutCollectionDTO>(File.ReadAllText(layoutFilePath), MonitorLayoutSerializer.Options) ?? new LayoutCollectionDTO()
                : new LayoutCollectionDTO();

            // Filter to find inactive monitors.
            List<MonitorLayoutDTO> inactiveMonitorLayouts = new List<MonitorLayoutDTO>();

            foreach (var monitorDTO in existing.Monitors)
            {
                if (monitors.Any(m => m.DeviceID == monitorDTO.MonitorID))
                {
                    continue;
                }

                inactiveMonitorLayouts.Add(monitorDTO);
            }

            StringBuilder stringBuilder = new StringBuilder();

            foreach (var monitor in inactiveMonitorLayouts)
            {
                foreach (var tile in monitor.Tiles)
                {
                    if (tile.Hint.Length == 1)
                    {
                        stringBuilder.Append(tile.Hint);
                    }
                }
            }

            var invalidCharacters = stringBuilder.ToString();

            this.seed = new string(seedCharacters.Distinct().Where(c => !invalidCharacters.Contains(c)).ToArray());
        }
    }
}
