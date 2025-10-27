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
        #region Fields

        /// <summary>
        /// The character array representing the seed for the pool.
        /// </summary>
        private readonly char[] seed;

        /// <summary>
        /// The queue used to sequentially assign hints.
        /// </summary>
        private readonly Queue<string> queue = new();

        #endregion

        #region Constructors

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

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of remaining combinations.
        /// </summary>
        public int Count => this.queue.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Clears and refills the queue with up to <paramref name="size"/> unique combinations.
        /// </summary>
        /// <param name="size">The size of the pool required.</param>
        public void RefillPool(int size)
        {
            this.queue.Clear();
            var result = new List<string>();
            var bfs = new Queue<string>();

            foreach (var ch in this.seed)
                bfs.Enqueue(ch.ToString());

            while (result.Count < size && bfs.Count > 0)
            {
                var current = bfs.Dequeue();
                result.Add(current);

                if (result.Count >= size)
                    break;

                foreach (var ch in this.seed)
                    bfs.Enqueue(current + ch);
            }

            foreach (var combo in result)
                this.queue.Enqueue(combo);
        }

        /// <summary>
        /// Dequeues the next available combination.
        /// </summary>
        /// <returns></returns>
        public string Dequeue()
        {
            if (this.queue.Count == 0)
                throw new InvalidOperationException("Character pool is empty. Call RefillPool first.");

            return this.queue.Dequeue();
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

            if (count > this.queue.Count)
                throw new InvalidOperationException($"Character pool has only {this.queue.Count} remaining.");

            var result = new Queue<string>();
            for (int i = 0; i < count; i++)
                result.Enqueue(this.queue.Dequeue());

            return result;
        }

        #endregion
    }
}
