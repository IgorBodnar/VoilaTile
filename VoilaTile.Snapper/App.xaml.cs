namespace VoilaTile.Snapper
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.EventArgs;
    using VoilaTile.Snapper.Input;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Layout;
    using VoilaTile.Snapper.Services;
    using VoilaTile.Snapper.Theming;
    using VoilaTile.Snapper.ViewModels;
    using VoilaTile.Snapper.Views;
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
        /// The input state manager.
        /// </summary>
        private InputStateManager? inputState;

        /// <summary>
        /// The global input listener.
        /// Handles hotkeys and user input.
        /// </summary>
        private GlobalInputListener? inputListener;

        /// <summary>
        /// Snap coordinator service.
        /// </summary>
        private SnapCoordinatorService? snappingCoordinator;

        /// <summary>
        /// Power grab coordinator service.
        /// </summary>
        private PowerGrabCoordinatorService? powerGrabCoordinator;

        /// <summary>
        /// Quick grab coordinator service.
        /// </summary>
        private QuickGrabCoordinatorService? quickGrabCoordinator;

        /// <summary>
        /// Tray icon service.
        /// </summary>
        private TrayIconService? trayIconService;

        /// <summary>
        /// The theme manager.
        /// </summary>
        private ThemeManager? themeManager;

        /// <summary>
        /// The theme file synchronization service.
        /// </summary>
        private ThemeFileSyncService? themeFileSyncService;

        /// <summary>
        /// The hidden host window used for message hooks.
        /// </summary>
        private Window? hostWindow;

        /// <summary>
        /// The current input binding.
        /// </summary>
        private IDisposable? currentInputBinding;

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

            this.InitializeThemeManagement();
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
                this.themeFileSyncService?.Dispose();
            }
            catch
            {
            }

            try
            {
                this.themeManager?.Dispose();
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
        /// Initializes theme management.
        /// </summary>
        private void InitializeThemeManagement()
        {
            this.themeManager = new ThemeManager();
            this.themeFileSyncService = new ThemeFileSyncService(this.themeManager);
        }

        /// <summary>
        /// Initializes application services.
        /// </summary>
        private void InitializeServices()
        {
            this.trayIconService = new TrayIconService();

            this.inputState = new InputStateManager();

            var overlayWindowFactory = new OverlayWindowFactory()
                .Register<SnapOverlayViewModel>(vm => new SnapOverlayWindow(vm))
                .Register<QuickGrabOverlayViewModel>(vm => new QuickGrabOverlayWindow(vm));

            var settingsMonitor = new SettingsMonitoringService(settingsFilePath);
            var overlayService = new OverlayDisplayService(overlayWindowFactory);
            var windowSnapper = new WindowSnappingService();
            var focusService = new WindowFocusService();
            var windowEnumerator = new WindowEnumerator();
            var windowIcons = new WindowIconService();
            var hintService = new HintService(settingsMonitor);
            var hintPlacementService = new HintPlacementService();
            var surfaceFactory = new DwmThumbnailSurfaceFactory();

            this.snappingCoordinator = new SnapCoordinatorService(overlayService, windowSnapper, this.inputState);

            Func<IntPtr> getHost = () => this.hostWindow?.GetHandleOrZero() ?? IntPtr.Zero;
            this.powerGrabCoordinator = new PowerGrabCoordinatorService(this.inputState, windowEnumerator, windowIcons, hintService, focusService, surfaceFactory, getHost);

            this.quickGrabCoordinator = new QuickGrabCoordinatorService(overlayService, windowEnumerator, hintPlacementService, hintService, this.inputState, focusService);

            this.inputListener = new GlobalInputListener(inputState, settingsMonitor);
            this.inputListener.OnHotKeyPressed += this.OnHotKeyPressed;
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
                string configuratorPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VoilaTile.Settings.exe");
                if (File.Exists(configuratorPath))
                {
                    System.Diagnostics.Process.Start(configuratorPath);
                }
                else
                {
                    MessageBox.Show("VoilaTile.Settings executable not found.", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch the Settings: {ex.Message}", "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles <see cref="GlobalInputListener.OnHotKeyPressed"/>.
        /// </summary>
        private void OnHotKeyPressed(HotKeyEventArgs e)
        {
            switch (e.InputFeature)
            {
                case InputFeature.Snap:
                    try
                    {
                        this.SwitchKeyboardTo(this.snappingCoordinator!);

                        // Begin snapping.
                        string layoutFilePath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "VoilaTile",
                            "active_layouts.json");

                        List<MonitorInfo> monitors = MonitorManager.GetMonitors();
                        List<ZoneLayoutModel> layouts = LayoutResolver.LoadAndResolveLayouts(layoutFilePath, monitors);

                        this.snappingCoordinator?.Begin(layouts);
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

                    break;

                case InputFeature.PowerGrab:
                    try
                    {
                        this.SwitchKeyboardTo(this.powerGrabCoordinator!);

                        // Launch power mode.
                        this.powerGrabCoordinator?.Begin();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to launch power grab: {ex.Message}", "Power Grab Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    break;

                case InputFeature.QuickGrab:
                    try
                    {
                        this.SwitchKeyboardTo(this.quickGrabCoordinator!);

                        // Launch quick grab mode.
                        this.quickGrabCoordinator?.Begin();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to launch quick grab: {ex.Message}", "Quick Grab Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    break;

            }
        }

        /// <summary>
        /// Switches keyboard control to the specified controller, disposing previous bindings.
        /// </summary>
        private void SwitchKeyboardTo(IKeyboardControllable controller)
        {
            // Dispose previous wiring
            try { this.currentInputBinding?.Dispose(); } catch { }
            this.currentInputBinding = null;

            // Attach new wiring
            if (this.inputListener is not null)
            {
                this.currentInputBinding = controller.Attach(this.inputListener);
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


