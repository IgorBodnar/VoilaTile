namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Helper that generates unique character combinations from a seed.
    /// Used to assign hint labels (e.g. "A", "AS", "DF") to zones.
    /// </summary>
    public class CharacterPool
    {
        private readonly char[] seed;
        private readonly Queue<string> queue = new();

        /// <summary>
        /// Initializes a new instance with the given character seed (e.g. "ASDFGHJKL").
        /// </summary>
        /// <param name="seedCharacters">The characters to use for combinations.</param>
        public CharacterPool(string seedCharacters)
        {
            if (string.IsNullOrWhiteSpace(seedCharacters))
                throw new ArgumentException("Seed characters must not be empty.");

            this.seed = seedCharacters.Distinct().ToArray();
        }

        /// <summary>
        /// Clears and refills the queue with up to <paramref name="size"/> unique combinations.
        /// </summary>
        public void RefillPool(int size)
        {
            queue.Clear();
            var result = new List<string>();
            var bfs = new Queue<string>();

            foreach (var ch in seed)
                bfs.Enqueue(ch.ToString());

            while (result.Count < size && bfs.Count > 0)
            {
                var current = bfs.Dequeue();
                result.Add(current);

                if (result.Count >= size)
                    break;

                foreach (var ch in seed)
                    bfs.Enqueue(current + ch);
            }

            foreach (var combo in result)
                queue.Enqueue(combo);
        }

        /// <summary>
        /// Dequeues the next available combination.
        /// </summary>
        public string Dequeue()
        {
            if (queue.Count == 0)
                throw new InvalidOperationException("Character pool is empty. Call RefillPool first.");

            return queue.Dequeue();
        }

        /// <summary>
        /// Dequeues multiple character combinations from the pool.
        /// </summary>
        /// <param name="count">Number of combinations to dequeue.</param>
        /// <returns>An array of unique character combinations.</returns>
        public Queue<string> DequeueMany(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");

            if (count > queue.Count)
                throw new InvalidOperationException($"Character pool has only {queue.Count} remaining.");

            var result = new Queue<string>();
            for (int i = 0; i < count; i++)
                result.Enqueue(queue.Dequeue());

            return result;
        }

        /// <summary>
        /// Returns the number of remaining combinations.
        /// </summary>
        public int Count => queue.Count;
    }
}
