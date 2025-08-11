namespace VoilaTile.Snapper
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.Input;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.Services;

    using Application = System.Windows.Application;
    using MessageBox = System.Windows.MessageBox;

    /// <summary>
    /// Interaction logic for the Snapper application.
    /// </summary>
    public partial class App : Application
    {
        private static string settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoilaTile",
                "settings.json");

        private GlobalInputListener? inputListener;
        private SnapCoordinatorService? coordinator;
        private TrayIconService? trayIconService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.trayIconService = new TrayIconService();

            // Create a hidden host window for WndProc
            var hostWindow = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Opacity = 0,
                Top = -10000,
                Left = -10000
            };
            hostWindow.Show();

            var inputState = new InputStateManager();
            var overlayService = new OverlayDisplayService();
            var windowSnapper = new WindowSnappingService();
            var settingsMonitor = new SettingsMonitoringService(settingsFilePath);
            this.coordinator = new SnapCoordinatorService(overlayService, windowSnapper, inputState);

            this.inputListener = new GlobalInputListener(inputState, settingsMonitor);
            this.inputListener.OnCharacterTyped += c => ForwardInput(inputState, c);
            this.inputListener.OnBackspacePressed += () => this.coordinator?.Backspace();
            this.inputListener.OnEscapePressed += () => this.coordinator?.Cancel();
            this.inputListener.OnEnterPressed += () => this.coordinator?.CommitSnap();
            this.inputListener.OnSpacePressed += () => this.coordinator?.CommitSnap();
            this.inputListener.OnManualHotKeyPressed += OnHotKeyPressed;
        }

        private void OnHotKeyPressed()
        {
            try
            {
                string layoutFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VoilaTile",
                    "active_layouts.json");

                List<MonitorInfo> monitors = MonitorManager.GetMonitors();
                List<ZoneLayoutModel> layouts = LayoutResolver.LoadAndResolveLayouts(layoutFilePath, monitors);

                this.coordinator?.BeginSnapping(layouts);
            }
            catch (FileNotFoundException ex)
            {
                var result = MessageBox.Show(
                    "The layout file could not be found. Would you like to open the Configurator to create or select a layout?",
                    "Missing Layout File",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LaunchConfigurator();
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("No layout found"))
            {
                var result = MessageBox.Show(
                    "Some connected monitors do not have a layout defined. Would you like to open the Configurator to fix this?",
                    "Unmatched Monitor Layout",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LaunchConfigurator();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch overlays: {ex.Message}", "Snapper Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ForwardInput(InputStateManager state, char c)
        {
            if (state.CurrentMode == VoilaTile.Snapper.Input.InputMode.Input)
            {
                this.coordinator?.ForwardCharacter(c);
            }
        }

        private void LaunchConfigurator()
        {
            try
            {
                string configuratorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoilaTile.Configurator.exe");
                if (File.Exists(configuratorPath))
                {
                    System.Diagnostics.Process.Start(configuratorPath);
                }
                else
                {
                    MessageBox.Show("Configurator executable not found.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch Configurator: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            this.inputListener?.Dispose();
            this.trayIconService?.Dispose();
            base.OnExit(e);
        }
    }
}


