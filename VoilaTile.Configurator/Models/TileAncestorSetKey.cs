namespace VoilaTile.Configurator.Models
{
    public class TileAncestorSetKey : IEquatable<TileAncestorSetKey>
    {
        private readonly Guid[] sortedIds;
        private readonly int hashCode;

        public TileAncestorSetKey(IEnumerable<Guid> ids)
        {
            this.sortedIds = ids.OrderBy(id => id).ToArray();
            this.hashCode = ComputeHash(this.sortedIds);
        }

        public HashSet<Guid> ToHashSet() => new(this.sortedIds);

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

        public bool Equals(TileAncestorSetKey? other)
        {
            if (other is null || other.sortedIds.Length != this.sortedIds.Length)
                return false;

            for (int i = 0; i < this.sortedIds.Length; i++)
                if (this.sortedIds[i] != other.sortedIds[i])
                    return false;

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as TileAncestorSetKey);
        public override int GetHashCode() => this.hashCode;
    }

}
