namespace VoilaTile.Snapper.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Windows.Input;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;

    /// <summary>
    /// Loads and watches the user settings JSON for Snapper, exposing the parsed shortcut keys and seed.
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

        private Key snapShortcutKey = Defaults.DefaultSnapShortcutKey;
        private Key quickGrabShortcutKey = Defaults.DefaultQuickGrabShortcutKey;
        private Key powerGrabShortcutKey = Defaults.DefaultPowerGrabShortcutKey;
        private string seed = Defaults.DefaultSeed;

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

            // Initial load (defaults on any issue)
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
        /// Gets the current snap shortcut key (combined with Win+Shift by the host).
        /// </summary>
        public Key SnapShortcutKey
        {
            get => this.snapShortcutKey;
            private set
            {
                if (this.snapShortcutKey != value)
                {
                    this.snapShortcutKey = value;
                }
            }
        }

        /// <summary>
        /// Gets the current quick grab shortcut key (combined with Win+Shift by the host).
        /// </summary>
        public Key QuickGrabShortcutKey
        {
            get => this.quickGrabShortcutKey;
            private set
            {
                if (this.quickGrabShortcutKey != value)
                {
                    this.quickGrabShortcutKey = value;
                }
            }
        }

        /// <summary>
        /// Gets the current power grab shortcut key (combined with Win+Shift by the host).
        /// </summary>
        public Key PowerGrabShortcutKey
        {
            get => this.powerGrabShortcutKey;
            private set
            {
                if (this.powerGrabShortcutKey != value)
                {
                    this.powerGrabShortcutKey = value;
                }
            }
        }

        /// <summary>
        /// Gets the current seed used for hint generation.
        /// </summary>
        public string Seed
        {
            get => this.seed;
            private set
            {
                if (!string.Equals(this.seed, value, StringComparison.Ordinal))
                {
                    this.seed = value;
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

        /// <summary>
        /// Loads settings immediately; safe defaults on any error.
        /// </summary>
        private void LoadNowSafe()
        {
            try
            {
                var (snap, quick, power, seedValue) = this.LoadSettingsWithRetry();
                this.SnapShortcutKey = snap;
                this.QuickGrabShortcutKey = quick;
                this.PowerGrabShortcutKey = power;
                this.Seed = seedValue;
            }
            catch
            {
                // Safe defaults if something unexpected happens.
                this.SnapShortcutKey = Defaults.DefaultSnapShortcutKey;
                this.QuickGrabShortcutKey = Defaults.DefaultQuickGrabShortcutKey;
                this.PowerGrabShortcutKey = Defaults.DefaultPowerGrabShortcutKey;
                this.Seed = Defaults.DefaultSeed;
            }
        }

        /// <summary>
        /// Reads and parses all shortcut keys and the seed from the JSON with brief retries
        /// (to tolerate temporary file locks during save).
        /// </summary>
        /// <returns>A tuple of (Snap, QuickGrab, PowerGrab, Seed).</returns>
        private (Key Snap, Key Quick, Key Power, string Seed) LoadSettingsWithRetry()
        {
            static Key ParseKey(string? s, Key fallback) =>
                !string.IsNullOrWhiteSpace(s) && Enum.TryParse<Key>(s, ignoreCase: true, out var parsed)
                    ? parsed
                    : fallback;

            const int attempts = 3;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    if (!File.Exists(this.filePath))
                    {
                        // File absent: return defaults.
                        return (Defaults.DefaultSnapShortcutKey,
                                Defaults.DefaultQuickGrabShortcutKey,
                                Defaults.DefaultPowerGrabShortcutKey,
                                Defaults.DefaultSeed);
                    }

                    string json = File.ReadAllText(this.filePath);
                    SettingsDTO? dto = JsonSerializer.Deserialize<SettingsDTO>(json, JsonOptions) ?? new SettingsDTO();

                    var snap = ParseKey(dto.SelectedSnapShortcutKey, Defaults.DefaultSnapShortcutKey);
                    var quick = ParseKey(dto.SelectedQuickGrabShortcutKey, Defaults.DefaultQuickGrabShortcutKey);
                    var power = ParseKey(dto.SelectedPowerGrabShortcutKey, Defaults.DefaultPowerGrabShortcutKey);
                    var seedValue = HintSeedHelper.CleanOrDefault(dto.Seed);

                    return (snap, quick, power, seedValue);
                }
                catch (IOException)
                {
                    Thread.Sleep(40); // brief backoff then retry
                    continue;
                }
                catch
                {
                    break; // fall through to defaults below
                }
            }

            return (Defaults.DefaultSnapShortcutKey,
                    Defaults.DefaultQuickGrabShortcutKey,
                    Defaults.DefaultPowerGrabShortcutKey,
                    Defaults.DefaultSeed);
        }
    }
}

