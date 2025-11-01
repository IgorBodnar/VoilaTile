namespace VoilaTile.Settings.Models
{
    using System.IO;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;
    using VoilaTile.Settings.DTO;

    /// <summary>
    /// The backing model of the layouts settings panel.
    /// Used on intialization and closing (for persistance).
    /// </summary>
    public class LayoutsSettingsPanelModel
    {
        #region Fields

        /// <summary>
        /// The list of monitor information representing all connected monitors.
        /// </summary>
        private readonly List<MonitorInfo> monitorInfos;

        /// <summary>
        /// The list of user created templates initialized from a file.
        /// </summary>
        private List<ZoneTemplate> templates;
            
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of <see cref="LayoutsSettingsPanelModel"/> class.   
        /// </summary>
        public LayoutsSettingsPanelModel()
        {
            this.monitorInfos = MonitorManager.GetMonitors();
            this.templates = new List<ZoneTemplate>();

            this.InitializeTemplates();
            this.InitializeMonitorTemplateSelection();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the enumerable collection of monitor infos for all connected monitors.
        /// </summary>
        /// <remarks>
        /// Used once in view model initialization.
        /// </remarks>
        public IEnumerable<MonitorInfo> Monitors => this.monitorInfos;

        /// <summary>
        /// Gets the enumerable collection of all user created templates.
        /// </summary>
        /// <remarks>
        /// Used once in view model initialization.
        /// </remarks>
        public IEnumerable<ZoneTemplate> Templates => this.templates;

        /// <summary>
        /// Gets the template selection collection.
        /// </summary>
        /// <remarks>
        /// Used once in view model initialization.
        /// </remarks>
        public SelectionCollectionDTO MonitorTemplateSelection { get; private set; }

        #endregion 

        #region Methods

        /// <summary>
        /// Saves the template selection per monitor into a file.
        /// </summary>
        /// <param name="selectionCollectionDTO">The selection collection dto representing the template selection per monitor.</param>
        /// <returns>An instance of <see cref="Task"/> representing an asynchronous operation.</returns>
        public async Task SaveMonitorTemplateSelectionAsync(SelectionCollectionDTO selectionCollectionDTO)
        {
            await Task.Run(() => MonitorSelectionStorage.SaveOrUpdateSelection(selectionCollectionDTO)).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves templates into a file.
        /// </summary>
        /// <param name="templateCollectionDTO">The template collection dto representing the templates library.</param>
        /// <returns>An instance of <see cref="Task"/> representing an asynchronous operation.</returns>
        public async Task SaveTemplatesAsync(TemplateCollectionDTO templateCollectionDTO)
        {
            await Task.Run(() => TemplatesStorage.SaveTemplates(templateCollectionDTO)).ConfigureAwait(false);
        }

        /// <summary>
        /// Initializes the templates library.
        /// </summary>
        private void InitializeTemplates()
        {
            this.templates = TemplatesMapper.MapToModels(TemplatesStorage.LoadTemplates());
        }

        /// <summary>
        /// Initializes the monitor template selection.
        /// </summary>
        private void InitializeMonitorTemplateSelection()
        {
            this.MonitorTemplateSelection = MonitorSelectionStorage.LoadSelection();
        }

        #endregion
    }
}
