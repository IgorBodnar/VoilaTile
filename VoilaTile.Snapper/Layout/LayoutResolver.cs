namespace VoilaTile.Snapper.Layout
{
    using System.IO;
    using System.Text.Json;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Models;

    /// <summary>
    /// Resolves and transforms the persisted layout file into usable runtime models.
    /// </summary>
    public static class LayoutResolver
    {
        /// <summary>
        /// Loads and resolves the layout file into monitor-specific zone layouts.
        /// </summary>
        /// <param name="layoutFilePath">Path to the layout JSON file.</param>
        /// <param name="activeMonitors">List of currently detected monitors.</param>
        /// <returns>A list of resolved zone layouts for connected monitors.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the layout file is missing.</exception>
        /// <exception cref="InvalidOperationException">Thrown if any monitor lacks a corresponding layout.</exception>
        public static List<ZoneLayoutModel> LoadAndResolveLayouts(string layoutFilePath, List<MonitorInfo> activeMonitors)
        {
            if (!File.Exists(layoutFilePath))
            {
                throw new FileNotFoundException("Layout file not found.", layoutFilePath);
            }

            string json = File.ReadAllText(layoutFilePath);
            LayoutCollectionDTO layoutCollection = JsonSerializer.Deserialize<LayoutCollectionDTO>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new LayoutCollectionDTO();

            var resolvedLayouts = new List<ZoneLayoutModel>();

            foreach (var monitor in activeMonitors)
            {
                var layoutDTO = layoutCollection.Monitors
                    .FirstOrDefault(l => string.Equals(l.MonitorID, monitor.DeviceID, StringComparison.OrdinalIgnoreCase));

                if (layoutDTO == null)
                {
                    throw new InvalidOperationException($"No layout found for connected monitor with DeviceID: {monitor.DeviceID}");
                }

                var resolvedTiles = layoutDTO.Tiles.Select(t => new ResolvedTileModel(
                    t.Hint,
                    t.X * monitor.WorkWidth,
                    t.Y * monitor.WorkHeight,
                    t.Width * monitor.WorkWidth,
                    t.Height * monitor.WorkHeight,
                    monitor.WorkX + t.X * monitor.WorkWidth,
                    monitor.WorkY + t.Y * monitor.WorkHeight,
                    monitor.DpiX,
                    monitor.DpiY
                )).ToList();

                resolvedLayouts.Add(new ZoneLayoutModel(monitor, layoutDTO, resolvedTiles));
            }

            return resolvedLayouts;
        }
    }
}


