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
        #region Fields

        /// <summary>
        /// Application settings file path.
        /// </summary>
        private static string settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VoilaTile",
                "settings.json");

        /// <summary>
        /// Single-instance mutex to prevent multiple instances of the process.
        /// </summary>
        private static Mutex? singleInstanceMutex;

        /// <summary>
        /// Guard to ensure dispose logic runs only once.
        /// </summary>
        private static int shuttingDown;

        /// <summary>
        /// The global input listener.
        /// Handles hotkeys and user input.
        /// </summary>
        private GlobalInputListener? inputListener;

        /// <summary>
        /// Snap coordinator service.
        /// </summary>
        private SnapCoordinatorService? coordinator;

        /// <summary>
        /// Tray icon service.
        /// </summary>
        private TrayIconService? trayIconService;

        /// <summary>
        /// The hidden host window used for message hooks.
        /// </summary>
        private Window? hostWindow;

        #endregion

        #region Events

        /// <summary>
        /// Handles application startup and delegates initialization to helper methods.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check that there is not another instance of VoilaTile.Snapper running.
            if (!this.InitializeSingleInstanceGuard())
            {
                this.Shutdown(0);
                return;
            }

            this.AttachShutdownHooks();
            this.CreateHiddenHostWindow();
            this.InitializeServices();
        }

        /// <summary>
        /// Handles application exit and funnels disposal through a helper method.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnExit(ExitEventArgs e)
        {
            this.Dispose();
            base.OnExit(e);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Attaches shutdown hooks that funnel down into <see cref="TryShutdown"/> or <see cref="Dispose"/>.
        /// </summary>
        private void AttachShutdownHooks()
        {
            this.DispatcherUnhandledException += (s, args) =>
            {
                args.Handled = true;
                this.TryShutdown(-1);
            };

            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                args.SetObserved();
                this.TryShutdown(-1);
            };

            AppDomain.CurrentDomain.ProcessExit += (s, args) =>
            {
                this.Dispose();
            };

            this.SessionEnding += (s, args) =>
            {
                this.TryShutdown(0);
            };
        }

        /// <summary>
        /// Creates a hidden host window (useful for message hooks, etc.).
        /// </summary>
        private void CreateHiddenHostWindow()
        {
            this.hostWindow = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Opacity = 0,
                Top = -10000,
                Left = -10000,
            };

            this.hostWindow.Show();
        }

        /// <summary>
        /// Disposes managed resources.
        /// Called from <see cref="OnExit"/> and also from non-WPF exit paths.
        /// </summary>
        private void Dispose()
        {
            try
            {
                this.inputListener?.Dispose();
            }
            catch
            {
            }

            try
            {
                this.trayIconService?.Dispose();
            }
            catch
            {
            }

            try
            {
                if (this.hostWindow is not null)
                {
                    if (this.hostWindow.Dispatcher.CheckAccess())
                    {
                        this.hostWindow.Close();
                    }
                    else
                    {
                        this.hostWindow.Dispatcher.Invoke(this.hostWindow.Close);
                    }
                }
            }
            catch
            {
            }

            try
            {
                singleInstanceMutex?.ReleaseMutex();
                singleInstanceMutex?.Dispose();
                singleInstanceMutex = null;
            }
            catch
            {
            }
        }

        /// <summary>
        /// Forwards the user input to the snap coordinator.
        /// </summary>
        /// <param name="state">The input state manager.</param>
        /// <param name="c">The input character.</param>
        private void ForwardInput(InputStateManager state, char c)
        {
            if (state.CurrentMode == VoilaTile.Snapper.Input.InputMode.Input)
            {
                this.coordinator?.ForwardCharacter(c);
            }
        }

        /// <summary>
        /// Initializes application services.
        /// </summary>
        private void InitializeServices()
        {
            this.trayIconService = new TrayIconService();

            var inputState = new InputStateManager();
            var overlayService = new OverlayDisplayService();
            var windowSnapper = new WindowSnappingService();
            var settingsMonitor = new SettingsMonitoringService(settingsFilePath);

            this.coordinator = new SnapCoordinatorService(overlayService, windowSnapper, inputState);

            this.inputListener = new GlobalInputListener(inputState, settingsMonitor);
            this.inputListener.OnCharacterTyped += c => this.ForwardInput(inputState, c);
            this.inputListener.OnBackspacePressed += () => this.coordinator?.Backspace();
            this.inputListener.OnEscapePressed += () => this.coordinator?.Cancel();
            this.inputListener.OnEnterPressed += () => this.coordinator?.CommitSnap();
            this.inputListener.OnSpacePressed += () => this.coordinator?.CommitSnap();
            this.inputListener.OnManualHotKeyPressed += this.OnHotKeyPressed;
        }

        /// <summary>
        /// Initializes the single-instance mutex.
        /// </summary>
        /// <returns>True if this process is first instance; false otherwise.</returns>
        private bool InitializeSingleInstanceGuard()
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, "VoilaTile.Snapper.SingleInstance", out createdNew);

            return createdNew;
        }

        /// <summary>
        /// Launches VoilaTile.Configurator.
        /// </summary>
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

        /// <summary>
        /// Handles <see cref="GlobalInputListener.OnManualHotKeyPressed"/>.
        /// </summary>
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

        /// <summary>
        /// Attempts to shutdown the application once, marshalled to the UI thread.
        /// </summary>
        /// <param name="code">Process exit code.</param>
        private void TryShutdown(int code)
        {
            // Check that not already shutting down.
            if (Interlocked.Exchange(ref shuttingDown, 1) == 1)
            {
                return;
            }

            if (this.Dispatcher.CheckAccess())
            {
                this.Shutdown(code);
            }
            else
            {
                this.Dispatcher.Invoke(() => this.Shutdown(code));
            }
        }

        #endregion
    }
}


