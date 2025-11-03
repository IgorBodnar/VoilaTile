namespace VoilaTile.Settings.ViewModels.Panels
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Settings.Models;

    /// <summary>
    /// View model for user-configurable application settings.
    /// </summary>
    public partial class InputSettingsPanelViewModel : SettingsPanelViewModel
    {
        #region Fields

        /// <summary>
        /// The model for this panel.
        /// </summary>
        private readonly InputSettingsPanelModel model;

        /// <summary>
        /// Backing field for <see cref="Seed"/>.
        /// </summary>
        private string seed = Defaults.DefaultSeed;

        /// <summary>
        /// All allowed keys for assignment (pre-filtered and ordered).
        /// </summary>
        private static readonly List<Key> AllowedKeys =
            ((Key[])Enum.GetValues(typeof(Key)))
            .Where(IsKeyAllowed)
            .OrderBy(k => k.ToString(), StringComparer.Ordinal)
            .ToList();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="InputSettingsPanelViewModel"/> class.
        /// </summary>
        /// <param name="model">The input settings panel model.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided model is null.</exception>
        public InputSettingsPanelViewModel(InputSettingsPanelModel model)
        {
            this.Title = "Hints and Shortcuts";

            this.model = model ?? throw new ArgumentNullException(nameof(model));
            
            if (!model.InputSettings.Seed.Equals(string.Empty))
            {
                this.Seed = model.InputSettings.Seed;
            }

            if (Enum.TryParse<Key>(model.InputSettings.SelectedSnapShortcutKey, out var parsedSnapKey))
            {
                this.SelectedSnapShortcutKey = parsedSnapKey;
            }

            if (Enum.TryParse<Key>(model.InputSettings.SelectedQuickGrabShortcutKey, out var parsedQuickGrabKey))
            {
                this.SelectedQuickGrabShortcutKey = parsedQuickGrabKey;
            }

            if (Enum.TryParse<Key>(model.InputSettings.SelectedPowerGrabShortcutKey, out var parsedPowerGrabKey))
            {
                this.SelectedPowerGrabShortcutKey = parsedPowerGrabKey;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the seed used for layout generation.
        /// Only lowercase letters (a–z) and numeric characters (0-9) are allowed, and characters must be unique.
        /// </summary>
        public string Seed
        {
            get => this.seed;
            set
            {
                string cleaned = HintSeedHelper.CleanOrDefault(value);

                if (this.seed != cleaned)
                {
                    this.SetProperty(ref this.seed, cleaned);
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected Snap shortcut key.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvailableSnapShortcutKeys))]
        [NotifyPropertyChangedFor(nameof(AvailableQuickGrabShortcutKeys))]
        [NotifyPropertyChangedFor(nameof(AvailablePowerGrabShortcutKeys))]
        private Key selectedSnapShortcutKey = Defaults.DefaultSnapShortcutKey;

        /// <summary>
        /// Gets or sets the selected Quick Grab shortcut key.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvailableSnapShortcutKeys))]
        [NotifyPropertyChangedFor(nameof(AvailableQuickGrabShortcutKeys))]
        [NotifyPropertyChangedFor(nameof(AvailablePowerGrabShortcutKeys))]
        private Key selectedQuickGrabShortcutKey = Defaults.DefaultQuickGrabShortcutKey;

        /// <summary>
        /// Gets or sets the selected Power Grab shortcut key.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AvailableSnapShortcutKeys))]
        [NotifyPropertyChangedFor(nameof(AvailableQuickGrabShortcutKeys))]
        [NotifyPropertyChangedFor(nameof(AvailablePowerGrabShortcutKeys))]
        private Key selectedPowerGrabShortcutKey = Defaults.DefaultPowerGrabShortcutKey;

        /// <summary>
        /// Gets the list of keys available for Snap (excludes Quick/Power selections).
        /// </summary>
        public IEnumerable<Key> AvailableSnapShortcutKeys =>
            AllowedKeys.Except(new[] { this.SelectedQuickGrabShortcutKey, this.SelectedPowerGrabShortcutKey });

        /// <summary>
        /// Gets the list of keys available for Quick Grab (excludes Snap/Power selections).
        /// </summary>
        public IEnumerable<Key> AvailableQuickGrabShortcutKeys =>
            AllowedKeys.Except(new[] { this.SelectedSnapShortcutKey, this.SelectedPowerGrabShortcutKey });

        /// <summary>
        /// Gets the list of keys available for Power Grab (excludes Snap/Quick selections).
        /// </summary>
        public IEnumerable<Key> AvailablePowerGrabShortcutKeys =>
            AllowedKeys.Except(new[] { this.SelectedSnapShortcutKey, this.SelectedQuickGrabShortcutKey });

        #endregion

        #region Methods

        /// <summary>
        /// Saves the current settings asynchronously.
        /// </summary>
        /// <returns>An instance of <see cref="Task"/> representing an asynchronous operation.</returns>
        public async Task SaveSettingsAsync()
        {
            SettingsDTO settingsDTO = new SettingsDTO()
            {
                Seed = this.Seed,
                SelectedSnapShortcutKey = this.SelectedSnapShortcutKey.ToString(),
                SelectedQuickGrabShortcutKey = this.SelectedQuickGrabShortcutKey.ToString(),
                SelectedPowerGrabShortcutKey = this.SelectedPowerGrabShortcutKey.ToString(),
            };

            await this.model.SaveSettingsAsync(settingsDTO);
        }

        /// <summary>
        /// Determines whether a key is allowed to be assigned.
        /// </summary>
        /// <param name="key">The candidate key.</param>
        /// <returns><c>true</c> if allowed; otherwise, <c>false</c>.</returns>
        private static bool IsKeyAllowed(Key key)
        {
            return
                (key >= Key.A && key <= Key.Z) || // Letters
                (key >= Key.D0 && key <= Key.D9) || // Top-row digits
                key == Key.Space ||
                key == Key.Tab;
        }

        /// <summary>
        /// Picks the first available key from the allowed pool excluding the provided keys.
        /// </summary>
        /// <param name="excluded">Keys that must be excluded.</param>
        /// <returns>The first available key.</returns>
        private static Key FirstAvailableExcluding(params Key[] excluded)
        {
            var set = excluded?.ToHashSet() ?? new HashSet<Key>();
            return AllowedKeys.First(k => !set.Contains(k));
        }

        /// <summary>
        /// Resolves conflicts so that all three selected shortcut keys remain unique.
        /// The latest-changed property keeps its new value; conflicting earlier ones are reassigned.
        /// </summary>
        /// <param name="winner">The property name of the one that just changed.</param>
        private void ResolveConflictsKeeping(string winner)
        {
            // Ensure uniqueness: Snap, Quick, Power must all be different.
            // The "winner" (last changed) keeps its selection; others move if they conflict.

            if (winner == nameof(this.SelectedSnapShortcutKey))
            {
                if (this.SelectedSnapShortcutKey == this.SelectedQuickGrabShortcutKey)
                {
                    this.SelectedQuickGrabShortcutKey = FirstAvailableExcluding(this.SelectedSnapShortcutKey, this.SelectedPowerGrabShortcutKey);
                }

                if (this.SelectedSnapShortcutKey == this.SelectedPowerGrabShortcutKey)
                {
                    this.SelectedPowerGrabShortcutKey = FirstAvailableExcluding(this.SelectedSnapShortcutKey, this.SelectedQuickGrabShortcutKey);
                }
            }
            else if (winner == nameof(this.SelectedQuickGrabShortcutKey))
            {
                if (this.SelectedQuickGrabShortcutKey == this.SelectedSnapShortcutKey)
                {
                    this.SelectedSnapShortcutKey = FirstAvailableExcluding(this.SelectedQuickGrabShortcutKey, this.SelectedPowerGrabShortcutKey);
                }

                if (this.SelectedQuickGrabShortcutKey == this.SelectedPowerGrabShortcutKey)
                {
                    this.SelectedPowerGrabShortcutKey = FirstAvailableExcluding(this.SelectedQuickGrabShortcutKey, this.SelectedSnapShortcutKey);
                }
            }
            else if (winner == nameof(this.SelectedPowerGrabShortcutKey))
            {
                if (this.SelectedPowerGrabShortcutKey == this.SelectedSnapShortcutKey)
                {
                    this.SelectedSnapShortcutKey = FirstAvailableExcluding(this.SelectedPowerGrabShortcutKey, this.SelectedQuickGrabShortcutKey);
                }

                if (this.SelectedPowerGrabShortcutKey == this.SelectedQuickGrabShortcutKey)
                {
                    this.SelectedQuickGrabShortcutKey = FirstAvailableExcluding(this.SelectedPowerGrabShortcutKey, this.SelectedSnapShortcutKey);
                }
            }
        }

        #endregion

        #region Generated Callbacks

        /// <summary>
        /// Called after <see cref="SelectedSnapShortcutKey"/> changes.
        /// Preserves the new value and reassigns conflicting selections.
        /// </summary>
        /// <param name="oldValue">Old key.</param>
        /// <param name="newValue">New key.</param>
        partial void OnSelectedSnapShortcutKeyChanged(Key oldValue, Key newValue)
            => this.ResolveConflictsKeeping(nameof(this.SelectedSnapShortcutKey));

        /// <summary>
        /// Called after <see cref="SelectedQuickGrabShortcutKey"/> changes.
        /// Preserves the new value and reassigns conflicting selections.
        /// </summary>
        /// <param name="oldValue">Old key.</param>
        /// <param name="newValue">New key.</param>
        partial void OnSelectedQuickGrabShortcutKeyChanged(Key oldValue, Key newValue)
            => this.ResolveConflictsKeeping(nameof(this.SelectedQuickGrabShortcutKey));

        /// <summary>
        /// Called after <see cref="SelectedPowerGrabShortcutKey"/> changes.
        /// Preserves the new value and reassigns conflicting selections.
        /// </summary>
        /// <param name="oldValue">Old key.</param>
        /// <param name="newValue">New key.</param>
        partial void OnSelectedPowerGrabShortcutKeyChanged(Key oldValue, Key newValue)
            => this.ResolveConflictsKeeping(nameof(this.SelectedPowerGrabShortcutKey));

        #endregion
    }
}

