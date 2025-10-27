namespace VoilaTile.Snapper.Services
{
    using System.Collections.Generic;
    using System.Text;
    using VoilaTile.Common.Helpers;

    /// <summary>
    /// Provides sequential assignment of keyboard hints using a custom alphabet.
    /// </summary>
    internal sealed class HintService : IHintService
    {
        #region Fields

        /// <summary>
        /// The alphabet used to generate hint strings, ordered by preference.
        /// </summary>
        private char[] seed;

        /// <summary>
        /// The reference to the settings monitoring service.
        /// </summary>
        private readonly SettingsMonitoringService settings;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the hint service.
        /// </summary>
        /// <param name="settings"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public HintService(SettingsMonitoringService settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.seed = Array.Empty<char>();
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public IReadOnlyList<string> AssignHints(int count)
        {
            // Load the seed from the settings.
            this.seed = HintSeedHelper.CleanOrDefault(this.settings.Seed).ToCharArray();

            var list = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(this.Encode(i));
            }

            return list;
        }

        /// <summary>
        /// Encodes an integer index into a hint string using the custom alphabet.
        /// Works like base-N numbering but with no zero digit (like Excel columns).
        /// </summary>
        private string Encode(int index)
        {
            var sb = new StringBuilder();

            index++; // shift so first hint = 1 → "a"

            while (index > 0)
            {
                index--; // adjust for 1-based alphabet
                sb.Insert(0, seed[index % seed.Length]);
                index /= seed.Length;
            }

            return sb.ToString();
        }

        #endregion
    }
}

