namespace VoilaTile.Settings
{
    using System.ComponentModel;
    using System.IO;
    using System.Windows;
    using VoilaTile.Settings.Enumerations;
    using VoilaTile.Settings.Helpers;
    using VoilaTile.Settings.Interfaces;
    using VoilaTile.Settings.Models;
    using VoilaTile.Settings.Services;
    using VoilaTile.Settings.Theming;
    using VoilaTile.Settings.ViewModels;
    using VoilaTile.Settings.ViewModels.Panels;
    using VoilaTile.Settings.Views;

    /// <summary>
    /// Application entry point. Composes view models and initializes theming.
    /// </summary>
    public partial class App : Application
    {
        #region Fields

        /// <summary>
        /// The application's theme manager.
        /// </summary>
        private ThemeManager? themeManager;

        /// <summary>
        /// The root shell view model, kept for shutdown save.
        /// </summary>
        private ShellViewModel? shellViewModel;

        /// <summary>
        /// The value indicating whether the user has already confirmed exit.
        /// </summary>
        private bool isConfirmedExit = false;

        /// <summary>
        /// The reentry guard for closing work.
        /// </summary>
        private bool isClosingWorkRunning = false;

        /// <summary>
        /// The value indicating whether to bootstrap Snapper on exit.
        /// </summary>
        private bool bootstapSnapperOnExit = false;

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the dialog service.
        /// </summary>
        public static IDialogService DialogService { get; } = new DialogService();

        #endregion Properties

        #region Methods

        /// <summary>
        /// Handles application startup.
        /// </summary>
        /// <param name="e">The startup arguments.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create and configure theme manager from persisted settings, then initialize.
            this.themeManager = new ThemeManager();
            var themeDto = ThemeSettingsStorage.LoadOrDefault();
            ThemeSettingsStorage.ApplyToManager(this.themeManager, themeDto);
            this.themeManager.Initialize();

            // Compose panel view models.
            var layoutsPanel = new LayoutsSettingsPanelViewModel(
                new LayoutsSettingsPanelModel());
            var inputPanel = new InputSettingsPanelViewModel(new InputSettingsPanelModel());
            var appearancePanel = new AppearanceSettingsPanelViewModel(this.themeManager);

            // Compose shell and show.
            this.shellViewModel = new ShellViewModel(layoutsPanel, inputPanel, appearancePanel);
            var shellView = new ShellView
            {
                DataContext = this.shellViewModel,
            };

            this.MainWindow = shellView;
            shellView.Closing += this.OnMainWindowClosing;
            shellView.Show();
        }

        /// <summary>
        /// Handles application exit.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            this.themeManager?.Dispose();
            base.OnExit(e);
        }

        /// <summary>
        /// Intercepts window close to ask for shutdown confirmation, optionally bootstrap Snapper,
        /// and persist settings asynchronously without blocking the UI thread.
        /// </summary>
        private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
        {
            // If we're already in the middle of the async shutdown work, let it proceed.
            if (this.isClosingWorkRunning)
            {
                return;
            }

            // If we already confirmed and re-invoked Close(), allow it to pass through.
            if (this.isConfirmedExit)
            {
                return;
            }

            await this.OrchestrateShutdownAsync(e).ConfigureAwait(true);
        }

        /// <summary>
        /// Handles the full shutdown flow: confirm, optional Snapper prompt, async persist, then re-close.
        /// </summary>
        private async Task OrchestrateShutdownAsync(CancelEventArgs e)
        {
            // 1) Confirm
            bool proceed = await this.ConfirmCloseAsync().ConfigureAwait(true);
            if (!proceed)
            {
                e.Cancel = true;
                return;
            }

            // 2) Snapper bootstrap prompt
            await this.AskToBootstrapSnapperAsync().ConfigureAwait(true);

            // 3) Cancel this close, run async saves, then re-invoke Close()
            e.Cancel = true;
            this.isClosingWorkRunning = true;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                await this.PersistAllSettingsAsync(cts.Token).ConfigureAwait(true);
            }
            catch
            {
                // Swallow; shutdown path should not crash.
            }
            finally
            {
                this.TryStartSnapperIfRequested();
                this.isClosingWorkRunning = false;
                this.isConfirmedExit = true;
                this.MainWindow?.Close(); // re-invoke; will pass through next time
            }
        }

        /// <summary>
        /// Shows the "Confirm Close" dialog. Returns true if user wants to exit.
        /// </summary>
        private async Task<bool> ConfirmCloseAsync()
        {
            var vm = new ConfirmationDialogViewModel(
                "Confirm Close",
                "You are about to close the settings.\n\nClosing the settings will persist all changes.\n\nAre you sure you want to continue?");

            var (decision, _) = await App.DialogService.ShowAsync(vm).ConfigureAwait(true);
            return decision != DialogDecision.Negative;
        }

        /// <summary>
        /// If Snapper is not running, asks the user whether to start it on exit, and records the choice.
        /// </summary>
        private async Task AskToBootstrapSnapperAsync()
        {
            if (SnapperProcessHelper.IsSnapperRunning())
            {
                this.bootstapSnapperOnExit = false;
                return;
            }

            var vm = new ConfirmationDialogViewModel(
                "Start Snapper",
                "VoilaTile.Snapper doesn’t appear to be running.\nDo you want to start it on exit to try your new layouts?");

            var (decision, _) = await App.DialogService.ShowAsync(vm).ConfigureAwait(true);
            this.bootstapSnapperOnExit = decision == DialogDecision.Positive;
        }

        /// <summary>
        /// Persists all settings in parallel without blocking the UI thread.
        /// Includes input settings, templates, selections, active layouts, and theme.
        /// </summary>
        /// <param name="ct">Cancellation token with a short timeout.</param>
        private async Task PersistAllSettingsAsync(CancellationToken ct)
        {
            if (this.shellViewModel is null)
            {
                return;
            }

            // Build DTOs
            var shell = this.shellViewModel;
            var activeLayoutsDto = ActiveLayoutsBuilder.Build(shell.InputPanel, shell.LayoutsPanel);

            // Kick off persistence in parallel.
            var t1 = shell.InputPanel.SaveSettingsAsync();
            var t2 = shell.LayoutsPanel.SaveTemplatesAsync();
            var t3 = shell.LayoutsPanel.SaveMonitorTemplateSelectionAsync();

            var t4 = Task.Run(() => MonitorLayoutStorage.SaveOrUpdateLayouts(activeLayoutsDto), ct);

            var t5 = Task.Run(() =>
            {
                if (this.themeManager is not null)
                {
                    var dto = ThemeSettingsStorage.FromManager(this.themeManager);
                    ThemeSettingsStorage.Save(dto);
                }
            }, ct);

            var all = Task.WhenAll(t1, t2, t3, t4, t5);

            // Timeout guard.
            var finished = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(6), ct)).ConfigureAwait(true);
        }

        /// <summary>
        /// Starts Snapper if the user opted in during the shutdown flow.
        /// </summary>
        private void TryStartSnapperIfRequested()
        {
            if (!this.bootstapSnapperOnExit)
            {
                return;
            }

            SnapperProcessHelper.TryStartSnapper();
        }


        #endregion Methods
    }
}

