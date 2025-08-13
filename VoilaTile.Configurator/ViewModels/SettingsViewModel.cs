namespace VoilaTile.Configurator.ViewModels
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using CommunityToolkit.Mvvm.ComponentModel;

    /// <summary>
    /// View model for user-configurable application settings.
    /// </summary>
    public partial class SettingsViewModel : ObservableObject
    {
        private string seed = "asdfghjklqwertyuiop";

        /// <summary>
        /// Gets or sets the seed used for layout generation.
        /// Only lowercase letters (a–z) and numeric characters (0-9) are allowed, and characters must be unique.
        /// </summary>
        public string Seed
        {
            get => seed;
            set
            {
                string cleaned = new string(
                    value
                        .Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                        .Distinct()
                        .ToArray());

                if (seed != cleaned)
                {
                    SetProperty(ref seed, cleaned);
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected shortcut key.
        /// </summary>
        [ObservableProperty]
        private Key selectedShortcutKey = Key.Space;

        /// <summary>
        /// Gets the list of available keys.
        /// </summary>
        public List<Key> AvailableShortcutKeys { get; } =
            ((Key[])System.Enum.GetValues(typeof(Key)))
            .Where(k => IsKeyAllowed(k))
            .OrderBy(k => k.ToString())
            .ToList();

        private static bool IsKeyAllowed(Key key)
        {
            return
                key >= Key.A && key <= Key.Z || // Letters
                key >= Key.D0 && key <= Key.D9 || // Numbers
                key == Key.Space || key == Key.Tab; // Space or Tab
        }
    }
}

