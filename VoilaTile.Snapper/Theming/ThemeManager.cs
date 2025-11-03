namespace VoilaTile.Snapper.Theming
{
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Win32;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Theming;

    /// <summary>
    /// Central runtime theme manager for Snapper.
    /// Swaps light/dark resource dictionaries and regenerates the accent palette
    /// when the source settings or Windows preferences change.
    /// </summary>
    public sealed class ThemeManager : IDisposable, INotifyPropertyChanged
    {
        #region Fields

        /// <summary>
        /// The light theme resource dictionary for Snapper.
        /// </summary>
        private readonly ResourceDictionary lightDict = new()
        {
            Source = new Uri("/VoilaTile.Snapper;component/Theming/Themes/Light.xaml", UriKind.Relative),
        };

        /// <summary>
        /// The dark theme resource dictionary for Snapper.
        /// </summary>
        private readonly ResourceDictionary darkDict = new()
        {
            Source = new Uri("/VoilaTile.Snapper;component/Theming/Themes/Dark.xaml", UriKind.Relative),
        };

        /// <summary>
        /// The currently selected base theme mode.
        /// </summary>
        private ThemeMode themeMode = ThemeMode.System;

        /// <summary>
        /// The currently selected accent color mode.
        /// </summary>
        private AccentMode accentMode = AccentMode.Windows;

        /// <summary>
        /// The custom accent color used when <see cref="AccentMode.Custom"/>.
        /// </summary>
        private Color customAccent = Defaults.DefaultAccentColor;

        /// <summary>
        /// Indicates whether this instance has been disposed.
        /// </summary>
        private bool isDisposed;

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
        /// Gets or sets how Snapper selects its base theme.
        /// </summary>
        public ThemeMode ThemeMode
        {
            get => this.themeMode;
            set
            {
                if (this.themeMode != value)
                {
                    this.themeMode = value;
                    this.ApplyThemeOnUI();
                    this.ApplyAccentOnUI(); // regenerate accent using new effective theme
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ThemeMode)));
                }
            }
        }

        /// <summary>
        /// Gets or sets how Snapper selects its accent color.
        /// </summary>
        public AccentMode AccentMode
        {
            get => this.accentMode;
            set
            {
                if (this.accentMode != value)
                {
                    this.accentMode = value;
                    this.ApplyAccentOnUI();
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
                        this.ApplyAccentOnUI();
                    }

                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.CustomAccent)));
                }
            }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Applies the current base theme and accent once. Call during application startup after construction.
        /// </summary>
        public void Initialize()
        {
            this.ApplyThemeOnUI();
            this.ApplyAccentOnUI();
        }

        /// <summary>
        /// Releases unmanaged resources and detaches system event handlers.
        /// </summary>
        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
            this.isDisposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Applies the currently selected theme by swapping merged dictionaries.
        /// Always executes on the UI thread.
        /// </summary>
        private void ApplyThemeOnUI()
        {
            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            void Swap()
            {
                bool useLight = this.GetEffectiveTheme() == ThemeMode.Light;

                // Remove both, then append the one we need (highest precedence at the end).
                app.Resources.MergedDictionaries.Remove(this.lightDict);
                app.Resources.MergedDictionaries.Remove(this.darkDict);
                app.Resources.MergedDictionaries.Add(useLight ? this.lightDict : this.darkDict);
            }

            if (app.Dispatcher.CheckAccess())
            {
                Swap();
            }
            else
            {
                app.Dispatcher.Invoke(Swap);
            }
        }

        /// <summary>
        /// Applies (regenerates) the accent palette and <c>Brush.OnAccent</c>.
        /// Always executes on the UI thread.
        /// </summary>
        private void ApplyAccentOnUI()
        {
            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            void Regenerate()
            {
                Color baseAccent = this.accentMode == AccentMode.Windows
                    ? WindowsThemeInterop.GetWindowsAccentOrDefault()
                    : this.customAccent;

                var effectiveTheme = this.GetEffectiveTheme();
                AccentPaletteGenerator.ApplyToResources(baseAccent, effectiveTheme);
            }

            if (app.Dispatcher.CheckAccess())
            {
                Regenerate();
            }
            else
            {
                app.Dispatcher.Invoke(Regenerate);
            }
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
        /// Handles Windows user preference changes. Responds only when following system/accent.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="e">Event args.</param>
        private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
        {
            // When following system theme, switch immediately.
            if (this.themeMode == ThemeMode.System)
            {
                this.ApplyThemeOnUI();
                this.ApplyAccentOnUI(); // accent nudges depend on effective theme
            }

            // When following Windows accent, regenerate on change.
            if (this.accentMode == AccentMode.Windows)
            {
                this.ApplyAccentOnUI();
            }
        }

        #endregion Methods
    }
}

