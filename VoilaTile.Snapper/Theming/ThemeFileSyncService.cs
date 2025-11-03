namespace VoilaTile.Snapper.Theming
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Theming;
    using VoilaTile.Snapper.Theming;

    /// <summary>
    /// Reads Snapper's theme settings from the Settings JSON file and keeps the runtime theme in sync.
    /// This service is read-only; it never writes the theme file.
    /// </summary>
    public sealed class ThemeFileSyncService : IDisposable
    {
        #region Fields

        /// <summary>
        /// JSON options for deserialization.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        /// <summary>
        /// The ThemeManager that applies dictionaries and accent resources.
        /// </summary>
        private readonly ThemeManager themeManager;

        /// <summary>
        /// Optional filesystem watcher for the theme file. May be null if directory is unavailable.
        /// </summary>
        private readonly FileSystemWatcher? watcher;

        /// <summary>
        /// Gate for debounce coordination.
        /// </summary>
        private readonly object debounceGate = new();

        /// <summary>
        /// CTS for the active debounce task.
        /// </summary>
        private CancellationTokenSource? debounceCts;

        /// <summary>
        /// Indicates whether this instance has been disposed.
        /// </summary>
        private bool isDisposed;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeFileSyncService"/> class.
        /// Performs an initial load and (if possible) starts watching the theme file for changes.
        /// </summary>
        /// <param name="themeManager">The theme manager to apply settings to.</param>
        public ThemeFileSyncService(ThemeManager themeManager)
        {
            this.themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

            // Initial load (safe if file missing or corrupt).
            this.LoadAndApply();

            // Set up file watching.
            var settingsPath = AppPaths.ThemeSettingsPath;
            var dir = Path.GetDirectoryName(settingsPath);
            var file = Path.GetFileName(settingsPath);

            if (!string.IsNullOrWhiteSpace(dir) && !string.IsNullOrWhiteSpace(file))
            {
                this.watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };

                this.watcher.Changed += this.OnSettingsChanged;
                this.watcher.Created += this.OnSettingsChanged;
                this.watcher.Renamed += this.OnSettingsChanged;
                this.watcher.Deleted += this.OnSettingsChanged;
            }
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Releases resources and stops file watching.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.watcher is not null)
            {
                this.watcher.Changed -= this.OnSettingsChanged;
                this.watcher.Created -= this.OnSettingsChanged;
                this.watcher.Renamed -= this.OnSettingsChanged;
                this.watcher.Deleted -= this.OnSettingsChanged;
                this.watcher.Dispose();
            }

            this.debounceCts?.Cancel();
            this.debounceCts?.Dispose();

            this.isDisposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles file watcher events with debounce to avoid partial reads and event bursts.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The file system event args.</param>
        private void OnSettingsChanged(object sender, FileSystemEventArgs e)
        {
            lock (this.debounceGate)
            {
                this.debounceCts?.Cancel();
                this.debounceCts?.Dispose();
                this.debounceCts = new CancellationTokenSource();
                _ = this.DebouncedReloadAsync(this.debounceCts.Token);
            }
        }

        /// <summary>
        /// Debounces reload to coalesce rapid file change events.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        private async Task DebouncedReloadAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token).ConfigureAwait(false);
                this.LoadAndApply();
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer event; ignore.
            }
        }

        /// <summary>
        /// Loads the DTO from disk and applies it to the ThemeManager on the UI thread.
        /// </summary>
        private void LoadAndApply()
        {
            var dto = TryLoadDtoOrDefault();

            void Apply()
            {
                this.themeManager.ThemeMode = ParseThemeMode(dto.ThemeMode);
                this.themeManager.AccentMode = ParseAccentMode(dto.AccentMode);

                if (this.themeManager.AccentMode == AccentMode.Custom)
                {
                    if (!TryParseHex(dto.CustomAccentHex, out var color))
                    {
                        color = ColorFromRgb(0x4C, 0x8C, 0xFF);
                    }

                    this.themeManager.CustomAccent = color;
                }

                // Ensure dictionaries + accent are present the first time.
                this.themeManager.Initialize();
            }

            var app = Application.Current;
            if (app is not null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
        }

        /// <summary>
        /// Attempts to read and deserialize the theme DTO from disk; returns defaults on failure.
        /// </summary>
        /// <returns>A valid <see cref="ThemeSettingsDTO"/>.</returns>
        private static ThemeSettingsDTO TryLoadDtoOrDefault()
        {
            try
            {
                var path = AppPaths.ThemeSettingsPath;

                if (!File.Exists(path))
                {
                    return new ThemeSettingsDTO();
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Thread.Sleep(50);
                    json = File.ReadAllText(path);
                }

                var dto = JsonSerializer.Deserialize<ThemeSettingsDTO>(json, JsonOpts);
                return dto ?? new ThemeSettingsDTO();
            }
            catch
            {
                return new ThemeSettingsDTO();
            }
        }

        /// <summary>
        /// Parses theme mode, defaulting to <see cref="ThemeMode.System"/> on invalid input.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <returns>The parsed <see cref="ThemeMode"/>.</returns>
        private static ThemeMode ParseThemeMode(string? s)
        {
            return Enum.TryParse<ThemeMode>(s, true, out var parsed) ? parsed : ThemeMode.System;
        }

        /// <summary>
        /// Parses accent mode, defaulting to <see cref="AccentMode.Windows"/> on invalid input.
        /// </summary>
        /// <param name="s">The input string.</param>
        /// <returns>The parsed <see cref="AccentMode"/>.</returns>
        private static AccentMode ParseAccentMode(string? s)
        {
            return Enum.TryParse<AccentMode>(s, true, out var parsed) ? parsed : AccentMode.Windows;
        }

        /// <summary>
        /// Parses a #RRGGBB string into a WPF <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">Hex string with or without leading '#'.</param>
        /// <param name="color">The parsed color.</param>
        /// <returns>True on success; otherwise false.</returns>
        private static bool TryParseHex(string? hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var m = Regex.Match(hex.Trim(), "^#?(?<r>[0-9A-Fa-f]{2})(?<g>[0-9A-Fa-f]{2})(?<b>[0-9A-Fa-f]{2})$");
            if (!m.Success)
            {
                return false;
            }

            byte r = Convert.ToByte(m.Groups["r"].Value, 16);
            byte g = Convert.ToByte(m.Groups["g"].Value, 16);
            byte b = Convert.ToByte(m.Groups["b"].Value, 16);
            color = Color.FromRgb(r, g, b);
            return true;
        }

        /// <summary>
        /// Creates a color from 8-bit RGB components.
        /// </summary>
        /// <param name="r">Red.</param>
        /// <param name="g">Green.</param>
        /// <param name="b">Blue.</param>
        /// <returns>A WPF color.</returns>
        private static Color ColorFromRgb(byte r, byte g, byte b)
        {
            return Color.FromRgb(r, g, b);
        }

        #endregion Methods
    }
}

