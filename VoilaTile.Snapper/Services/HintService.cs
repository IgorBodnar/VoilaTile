namespace VoilaTile.Snapper.Services
{
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Provides sequential assignment of keyboard hints using a custom alphabet.
    /// </summary>
    internal sealed class HintService : IHintService
    {
        /// <summary>
        /// The alphabet used to generate hint strings, ordered by preference.
        /// </summary>
        private static readonly char[] Alphabet = "asdfghjklqwertyuiopzxcvbnm".ToCharArray();

        /// <inheritdoc />
        public IReadOnlyList<string> AssignHints(int count)
        {
            var list = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                list.Add(Encode(i));
            }

            return list;
        }

        /// <summary>
        /// Encodes an integer index into a hint string using the custom alphabet.
        /// Works like base-N numbering but with no zero digit (like Excel columns).
        /// </summary>
        private static string Encode(int index)
        {
            var sb = new StringBuilder();

            index++; // shift so first hint = 1 → "a"

            while (index > 0)
            {
                index--; // adjust for 1-based alphabet
                sb.Insert(0, Alphabet[index % Alphabet.Length]);
                index /= Alphabet.Length;
            }

            return sb.ToString();
        }
    }
}

