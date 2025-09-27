namespace VoilaTile.Snapper.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Generates deterministic hint labels for a set of items.
    /// </summary>
    public interface IHintService
    {
        #region Methods

        /// <summary>
        /// Assigns hints for a collection of items.
        /// </summary>
        /// <param name="count">The number of hints to generate.</param>
        /// <returns>A list of hint strings with <paramref name="count"/> elements.</returns>
        IReadOnlyList<string> AssignHints(int count);

        #endregion
    }
}
