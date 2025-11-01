namespace VoilaTile.Settings.Models
{
    /// <summary>
    /// The tile ancestor set key used to uniquely identify a tile based on the set of its ancestors.
    /// </summary>
    public class TileAncestorSetKey : IEquatable<TileAncestorSetKey>
    {
        /// <summary>
        /// The sorted array of ancestor IDs.
        /// </summary>
        private readonly Guid[] sortedIds;

        /// <summary>
        /// The hash code.
        /// </summary>
        private readonly int hashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="TileAncestorSetKey"/> class.
        /// </summary>
        /// <param name="ids">The enumerable collection of tile ancestor GUIDs.</param>
        public TileAncestorSetKey(IEnumerable<Guid> ids)
        {
            this.sortedIds = ids.OrderBy(id => id).ToArray();
            this.hashCode = ComputeHash(this.sortedIds);
        }

        /// <summary>
        /// Gets the hash set of ancestor IDs.
        /// </summary>
        /// <returns>A hash set of ancestor IDs.</returns>
        public HashSet<Guid> ToHashSet() => new(this.sortedIds);

        /// <summary>
        /// Computes the hash code for the given array of GUIDs.
        /// </summary>
        /// <param name="ids">The array of GUIDs.</param>
        /// <returns>A hash for the specific tile.</returns>
        private static int ComputeHash(Guid[] ids)
        {
            unchecked
            {
                int hash = 17;
                foreach (var id in ids)
                    hash = hash * 31 + id.GetHashCode();
                return hash;
            }
        }

        /// <inheritdoc/>
        public bool Equals(TileAncestorSetKey? other)
        {
            if (other is null || other.sortedIds.Length != this.sortedIds.Length)
                return false;

            for (int i = 0; i < this.sortedIds.Length; i++)
                if (this.sortedIds[i] != other.sortedIds[i])
                    return false;

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as TileAncestorSetKey);

        /// <inheritdoc/>
        public override int GetHashCode() => this.hashCode;
    }

}
