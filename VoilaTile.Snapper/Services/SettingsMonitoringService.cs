namespace VoilaTile.Snapper.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Windows.Input;
    using VoilaTile.Common.DTO;

    /// <summary>
    /// Loads and watches the user settings JSON for Snapper, exposing the parsed shortcut key.
    /// </summary>
    public sealed class SettingsMonitoringService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly string filePath;
        private readonly FileSystemWatcher? watcher;
        private readonly object gate = new();
        private Timer? debounceTimer;
        private Key shortcutKey = Key.Space;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SnapperSettingsService"/> class.
        /// Parses the settings immediately and begins watching for changes.
        /// </summary>
        /// <param name="settingsFilePath">Absolute path to the settings JSON file.</param>
        public SettingsMonitoringService(string settingsFilePath)
        {
            if (string.IsNullOrWhiteSpace(settingsFilePath))
            {
                throw new ArgumentException("Settings file path must be provided.", nameof(settingsFilePath));
            }

            this.filePath = Path.GetFullPath(settingsFilePath);

            // Initial load (defaults to Space on any issue)
            this.LoadNowSafe();

            // Setup watcher if directory exists; otherwise there is nothing to watch yet.
            string? dir = Path.GetDirectoryName(this.filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir); // ensure exists so watcher can attach
                this.watcher = new FileSystemWatcher(dir)
                {
                    Filter = Path.GetFileName(this.filePath),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };

                this.watcher.Changed += this.OnSettingsFileChanged;
                this.watcher.Created += this.OnSettingsFileChanged;
                this.watcher.Renamed += this.OnSettingsFileChanged;
                this.watcher.Deleted += this.OnSettingsFileChanged;
            }
        }

        /// <summary>
        /// Gets the current shortcut key (combined with Win+Shift by the host).
        /// </summary>
        public Key ShortcutKey
        {
            get => this.shortcutKey;
            private set
            {
                if (this.shortcutKey != value)
                {
                    this.shortcutKey = value;
                }
            }
        }

        /// <summary>
        /// Disposes the watcher and timers.
        /// </summary>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            lock (this.gate)
            {
                this.debounceTimer?.Dispose();
                this.watcher?.Dispose();
            }
        }

        private void OnSettingsFileChanged(object? sender, FileSystemEventArgs e)
        {
            // Debounce rapid change events (save operations may fire multiple times).
            lock (this.gate)
            {
                this.debounceTimer?.Dispose();
                this.debounceTimer = new Timer(_ => this.LoadNowSafe(), null, dueTime: 150, period: Timeout.Infinite);
            }
        }

        private void LoadNowSafe()
        {
            try
            {
                this.ShortcutKey = this.LoadShortcutKeyWithRetry();
            }
            catch
            {
                // On any error, keep a safe default.
                this.ShortcutKey = Key.Space;
            }
        }

        private Key LoadShortcutKeyWithRetry()
        {
            // Retry a couple of times in case the file is temporarily locked by another writer.
            const int attempts = 3;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (!File.Exists(this.filePath))
                    {
                        return Key.Space; // default when settings file is absent
                    }

                    string json = File.ReadAllText(this.filePath);
                    SettingsDTO? dto = JsonSerializer.Deserialize<SettingsDTO>(json, JsonOptions) ?? new SettingsDTO();

                    // Parse the string into a Key; default to Space on failure or empty.
                    if (dto.SelectedShortcutKey is string s && Enum.TryParse<Key>(s, out var parsed))
                    {
                        return parsed;
                    }

                    return Key.Space;
                }
                catch (IOException)
                {
                    // brief backoff then retry
                    Thread.Sleep(40);
                    continue;
                }
                catch
                {
                    // Any other error: break and fall through to default.
                    break;
                }
            }

            return Key.Space;
        }
    }
}

