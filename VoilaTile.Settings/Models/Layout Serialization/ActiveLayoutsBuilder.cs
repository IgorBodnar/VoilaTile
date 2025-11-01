namespace VoilaTile.Settings.Models
{
    using System.Linq;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Settings.Models;
    using VoilaTile.Settings.ViewModels.Panels;

    /// <summary>
    /// Builds the DTO required to persist active monitor layouts.
    /// </summary>
    public static class ActiveLayoutsBuilder
    {
        /// <summary>
        /// Builds the active layouts DTO from the user edited view model information.
        /// </summary>
        /// <param name="inputVm">The input settings view model.</param>
        /// <param name="layoutsVm">The layout settings view model.</param>
        /// <returns>The layout collection dto representing the active layouts.</returns>
        public static LayoutCollectionDTO Build(
            InputSettingsPanelViewModel inputVm,
            LayoutsSettingsPanelViewModel layoutsVm)
        {
            var monitorInfos = layoutsVm.Monitors.Select(m => m.MonitorInfo).ToList();
            var poolFactory = new CharacterPoolFactory(inputVm.Seed, monitorInfos);
            return MonitorLayoutMapper.MapLayouts(layoutsVm.Monitors.ToList(), poolFactory.Pool);
        }
    }
}

