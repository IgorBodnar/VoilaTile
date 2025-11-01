namespace VoilaTile.Settings.Theming
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Win32;

    /// <summary>
    /// Central runtime theme manager. Swaps light/dark resource dictionaries and
    /// regenerates the accent palette on demand or when Windows preferences change.
    /// </summary>
    public sealed class ThemeManager : IDisposable, INotifyPropertyChanged
    {
        #region Fields

        /// <summary>
        /// The light theme resource dictionary.
        /// </summary>
        private readonly ResourceDictionary lightDict = new()
        {
            Source = new Uri("/VoilaTile.Settings;component/Theming/Themes/Light.xaml", UriKind.Relative),
        };

        /// <summary>
        /// The dark theme resource dictionary.
        /// </summary>
        private readonly ResourceDictionary darkDict = new()
        {
            Source = new Uri("/VoilaTile.Settings;component/Theming/Themes/Dark.xaml", UriKind.Relative),
        };

        /// <summary>
        /// The current theme selection mode.
        /// </summary>
        private ThemeMode themeMode = ThemeMode.System;

        /// <summary>
        /// The current accent color source.
        /// </summary>
        private AccentMode accentMode = AccentMode.Windows;

        /// <summary>
        /// The custom accent color used when <see cref="AccentMode.Custom"/>.
        /// </summary>
        private Color customAccent = Color.FromRgb(0x4C, 0x8C, 0xFF);

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeManager"/> class.
        /// </summary>
        public ThemeManager()
        {
            SystemEvents.UserPreferenceChanged += this.OnUserPreferenceChanged;
        }

        #endregion Constructors

        #region Events

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        #endregion Events

        #region Properties

        /// <summary>
        /// Gets or sets how the application selects its base theme.
        /// </summary>
        public ThemeMode ThemeMode
        {
            get => this.themeMode;
            set
            {
                if (this.themeMode != value)
                {
                    this.themeMode = value;
                    this.ApplyTheme();
                    this.ApplyAccent(); // regenerate accent with new effective theme
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ThemeMode)));
                }
            }
        }

        /// <summary>
        /// Gets or sets how the application selects its accent color.
        /// </summary>
        public AccentMode AccentMode
        {
            get => this.accentMode;
            set
            {
                if (this.accentMode != value)
                {
                    this.accentMode = value;
                    this.ApplyAccent();
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.AccentMode)));
                }
            }
        }

        /// <summary>
        /// Gets or sets the custom accent color, used only when <see cref="AccentMode"/> is <see cref="Theming.AccentMode.Custom"/>.
        /// </summary>
        public Color CustomAccent
        {
            get => this.customAccent;
            set
            {
                if (this.customAccent != value)
                {
                    this.customAccent = value;
                    if (this.accentMode == AccentMode.Custom)
                    {
                        this.ApplyAccent();
                    }

                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.CustomAccent)));
                }
            }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Initializes the theme and accent according to current settings.
        /// Call once during application startup.
        /// </summary>
        public void Initialize()
        {
            this.ApplyTheme();
            this.ApplyAccent();
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Applies the currently selected theme by swapping merged dictionaries.
        /// </summary>
        private void ApplyTheme()
        {
            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            bool useLight = this.GetEffectiveTheme() == ThemeMode.Light;

            // Remove both if present, then add desired one to the end (highest precedence).
            app.Resources.MergedDictionaries.Remove(this.lightDict);
            app.Resources.MergedDictionaries.Remove(this.darkDict);
            app.Resources.MergedDictionaries.Add(useLight ? this.lightDict : this.darkDict);
        }

        /// <summary>
        /// Applies (regenerates) the accent palette based on the current accent mode and effective theme.
        /// </summary>
        private void ApplyAccent()
        {
            Color baseAccent = this.accentMode == AccentMode.Windows
                ? WindowsThemeInterop.GetWindowsAccentOrDefault()
                : this.customAccent;

            var effectiveTheme = this.GetEffectiveTheme();
            AccentPaletteGenerator.ApplyToResources(baseAccent, effectiveTheme);
        }

        /// <summary>
        /// Resolves the effective theme (Light or Dark), expanding <see cref="ThemeMode.System"/> to the current Windows preference.
        /// </summary>
        /// <returns>The effective theme (<see cref="ThemeMode.Light"/> or <see cref="ThemeMode.Dark"/>).</returns>
        private ThemeMode GetEffectiveTheme()
        {
            return this.themeMode switch
            {
                ThemeMode.Light => ThemeMode.Light,
                ThemeMode.Dark => ThemeMode.Dark,
                _ => WindowsThemeInterop.IsWindowsAppsLightMode() ? ThemeMode.Light : ThemeMode.Dark,
            };
        }

        /// <summary>
        /// Handles Windows user preference changes (theme/accent) and updates the app when following system.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event args.</param>
        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            if (this.themeMode == ThemeMode.System)
            {
                this.ApplyTheme();
            }

            if (this.accentMode == AccentMode.Windows)
            {
                this.ApplyAccent();
            }
        }

        #endregion Methods
    }
}

