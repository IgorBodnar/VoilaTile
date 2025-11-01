using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoilaTile.Settings.Theming;

namespace VoilaTile.Settings.ViewModels.Panels
{
    /// <summary>
    /// Appearance/theme settings panel.
    /// </summary>
    public sealed partial class AppearanceSettingsPanelViewModel : SettingsPanelViewModel
    {
        #region Fields

        /// <summary>
        /// The theme manager used to apply theme and accent changes.
        /// </summary>
        private readonly ThemeManager themeManager;

        /// <summary>
        /// Backing field for <see cref="SelectedThemeMode"/>.
        /// </summary>
        [ObservableProperty]
        private ThemeMode selectedThemeMode;

        /// <summary>
        /// Backing field for <see cref="SelectedAccentMode"/>.
        /// </summary>
        [ObservableProperty]
        private AccentMode selectedAccentMode;

        /// <summary>
        /// Backing field for <see cref="CustomAccent"/>.
        /// </summary>
        [ObservableProperty]
        private Color customAccent;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AppearanceSettingsPanelViewModel"/> class.
        /// </summary>
        /// <param name="themeManager">The application's theme manager.</param>
        public AppearanceSettingsPanelViewModel(ThemeManager themeManager)
        {
            this.themeManager = themeManager;

            this.selectedThemeMode = this.themeManager.ThemeMode;
            this.selectedAccentMode = this.themeManager.AccentMode;

            // If we're following Windows at startup, preview should show Windows accent, not the default blue.
            this.customAccent = this.selectedAccentMode == AccentMode.Windows
                ? WindowsThemeInterop.GetWindowsAccentOrDefault()
                : this.themeManager.CustomAccent;

            this.Title = "Appearance";
        }

        #endregion Constructors

        #region Events
        // No public events.
        #endregion Events

        #region Properties

        /// <summary>
        /// Gets the list of available theme modes for binding.
        /// </summary>
        public ReadOnlyCollection<ThemeMode> ThemeModeOptions { get; }
            = Array.AsReadOnly(Enum.GetValues(typeof(ThemeMode)).Cast<ThemeMode>().ToArray());

        /// <summary>
        /// Gets the list of available accent modes for binding.
        /// </summary>
        public ReadOnlyCollection<AccentMode> AccentModeOptions { get; }
            = Array.AsReadOnly(Enum.GetValues(typeof(AccentMode)).Cast<AccentMode>().ToArray());

        /// <summary>
        /// Gets or sets the selected theme mode (System/Light/Dark).
        /// Two-way bound to the UI; applies immediately.
        /// </summary>
        partial void OnSelectedThemeModeChanged(ThemeMode value)
        {
            this.themeManager.ThemeMode = value;
        }

        /// <summary>
        /// Gets or sets the selected accent mode (Windows/Custom).
        /// Two-way bound to the UI; applies immediately.
        /// </summary>
        partial void OnSelectedAccentModeChanged(AccentMode value)
        {
            this.themeManager.AccentMode = value;

            if (value == AccentMode.Custom)
            {
                // Apply the current custom color immediately.
                this.themeManager.CustomAccent = this.customAccent;
            }
            else if (value == AccentMode.Windows)
            {
                // Keep the preview in sync with the effective color users will see.
                // (Safe: OnCustomAccentChanged only pushes to ThemeManager when in Custom mode.)
                this.CustomAccent = WindowsThemeInterop.GetWindowsAccentOrDefault();
            }
        }

        /// <summary>
        /// Gets or sets the custom accent color (used when <see cref="SelectedAccentMode"/> is Custom).
        /// Two-way bound; applies on change when in Custom mode.
        /// </summary>
        partial void OnCustomAccentChanged(Color value)
        {
            if (this.selectedAccentMode == AccentMode.Custom)
            {
                this.themeManager.CustomAccent = value;
            }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Command to adopt the current Windows accent color as the custom accent.
        /// Useful to start from system color and then tweak.
        /// </summary>
        [RelayCommand]
        private void UseWindowsAccentAsCustom()
        {
            var win = WindowsThemeInterop.GetWindowsAccentOrDefault();
            this.CustomAccent = win; // triggers OnCustomAccentChanged
        }

        #endregion Methods
    }
}

