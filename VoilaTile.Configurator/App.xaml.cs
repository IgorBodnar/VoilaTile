namespace VoilaTile.Configurator
{
    using System.IO;
    using System.Windows;
    using VoilaTile.Common.DTO;
    using VoilaTile.Configurator.DTO;
    using VoilaTile.Configurator.Enumerations;
    using VoilaTile.Configurator.Helpers;
    using VoilaTile.Configurator.Interfaces;
    using VoilaTile.Configurator.Services;
    using VoilaTile.Configurator.ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App : Application
    {
        private static string layoutFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoilaTile",
                "active_layouts.json");

        private static string templatesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoilaTile",
                "templates.json");

        private static string selectionFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoilaTile",
                "selection.json");

        private static string settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoilaTile",
                "settings.json");

        private MainViewModel mainViewModel;

        public static IDialogService DialogService { get; } = new DialogService();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var templates = TemplatesMapper.MapToModels(TemplatesSerializer.LoadTemplates(templatesFilePath));
            var selection = MonitorSelectionSerializer.LoadSelection(selectionFilePath);
            var settings = SettingsSerializer.LoadSettings(settingsFilePath);

            this.mainViewModel = new MainViewModel(templates, selection, settings);
            this.MainWindow = new MainWindow(this.mainViewModel);
            this.MainWindow.Show();

            this.mainViewModel.ShowSelectedMonitorOverlay();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            // Serialize settings.
            SettingsDTO settingsDTO = new SettingsDTO()
            {
                Seed = this.mainViewModel.Settings.Seed,
                SelectedShortcutKey = this.mainViewModel.Settings.SelectedShortcutKey.ToString(),
            };

            SettingsSerializer.SaveSettings(settingsFilePath, settingsDTO);

            // Serialize the current selection.
            SelectionCollectionDTO selectionCollectionDTO = new SelectionCollectionDTO()
            {
                Selections = this.mainViewModel.Monitors.Select(m =>
                    new MonitorTemplateSelectionDTO()
                    {
                        MonitorID = m.MonitorInfo.DeviceID,
                        TemplateName = m.SelectedTemplate.Name,
                    }).ToList(),
            };

            MonitorSelectionSerializer.SaveOrUpdateSelection(selectionFilePath, selectionCollectionDTO);

            // Serialize the template library.
            var templatesToPersist = this.mainViewModel.TemplateLibrary.Select(vm => vm.Template).Where(m => !m.IsDefault).ToList();
            TemplatesSerializer.SaveTemplates(templatesFilePath, TemplatesMapper.MapToDTO(templatesToPersist));

            // Serialize the selected templates.
            var monitorInfos = this.mainViewModel.Monitors.Select(m => m.MonitorInfo).ToList();
            var characterPoolFactory = new CharacterPoolFactory(this.mainViewModel.Settings.Seed, monitorInfos, layoutFilePath);

            var layoutDTO = MonitorLayoutMapper.MapLayouts(this.mainViewModel.Monitors.ToList(), characterPoolFactory.Pool);

            MonitorLayoutSerializer.SaveOrUpdateLayouts(layoutFilePath, layoutDTO);

            // Bootstrap snapper if needed.
            if (this.mainViewModel.BootstrapSnapperOnExit && !SnapperProcessHelper.IsSnapperRunning())
            {
                SnapperProcessHelper.TryStartSnapper();
            }
        }
    }
}
