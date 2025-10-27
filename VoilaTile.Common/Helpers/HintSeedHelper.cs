namespace VoilaTile.Common.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Provides utilities for validating and normalizing hint seeds.
    /// </summary>
    public static class HintSeedHelper
    {
        #region Methods

        /// <summary>
        /// Cleans a seed by keeping only lowercase letters <c>a–z</c> and digits <c>0–9</c>,
        /// removing duplicates while preserving the first occurrence order.
        /// Returns <see cref="Defaults.DefaultSeed"/> if the result is empty.
        /// </summary>
        /// <param name="value">The raw seed text (may be <c>null</c>).</param>
        /// <returns>A normalized, non-empty seed string.</returns>
        public static string CleanOrDefault(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Defaults.DefaultSeed;
            }

            var seen = new HashSet<char>();
            var sb = new StringBuilder(value.Length);

            foreach (var c in value)
            {
                bool isAllowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                if (isAllowed && seen.Add(c))
                {
                    sb.Append(c);
                }
            }

            return sb.Length == 0 ? Defaults.DefaultSeed : sb.ToString();
        }

        #endregion
    }
}
