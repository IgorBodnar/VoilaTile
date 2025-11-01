namespace VoilaTile.Settings.Models
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
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;

    /// <summary>
    /// A factory responsible for creating a <see cref="CharacterPool"/> while ensuring that certain characters are excluded based on inactive monitor layouts.
    /// </summary>
    public class CharacterPoolFactory
    {
        #region Fields

        /// <summary>
        /// The characters to be used in the character pool after sanitization.
        /// </summary>
        private string seed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterPoolFactory"/> class.
        /// </summary>
        /// <param name="seedCharacters">The string representing the seed characters.</param>
        /// <param name="monitors">The list of currently connected monitors.</param>
        public CharacterPoolFactory(string seedCharacters, List<MonitorInfo> monitors)
        {
            this.seed = Defaults.DefaultSeed;

            this.SanitizeSeed(seedCharacters, monitors);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the character pool created from the sanitized seed.
        /// </summary>
        public CharacterPool Pool => new CharacterPool(this.seed);

        #endregion

        #region Methods

        /// <summary>
        /// Sanitizes the seed by removing characters that are already in use by inactive monitor layouts.
        /// </summary>
        /// <param name="seedCharacters">The string representing the seed characters.</param>
        /// <param name="monitors">The list of currently active monitors.</param>
        private void SanitizeSeed(string seedCharacters, List<MonitorInfo> monitors)
        {
            var layoutFilePath = AppPaths.ActiveLayoutsPath;

            // Load the existing monitor layout information.
            LayoutCollectionDTO existing = File.Exists(layoutFilePath)
            ? JsonSerializer.Deserialize<LayoutCollectionDTO>(File.ReadAllText(layoutFilePath), MonitorLayoutStorage.Options) ?? new LayoutCollectionDTO()
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

        #endregion
    }
}
