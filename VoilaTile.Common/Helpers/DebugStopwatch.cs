namespace VoilaTile.Common.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Records high-resolution timestamps keyed by a marker and logs the elapsed time
    /// (in milliseconds) between successive calls that use the same marker.
    /// Also logs a free-form message for traceability.
    /// </summary>
    public static class DebugStopwatch
    {
        #region Fields

        /// <summary>
        /// Stores the last recorded high-resolution timestamp per marker.
        /// </summary>
        private static readonly ConcurrentDictionary<string, long> lastTimestamps =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the stopwatch frequency (ticks per second) for converting raw timestamps.
        /// </summary>
        public static long Frequency => Stopwatch.Frequency;

        #endregion

        #region Methods

        /// <summary>
        /// Records a timestamp under the specified <paramref name="marker"/> and writes a debug line.
        /// If a previous timestamp exists for the same marker, logs the elapsed time in milliseconds
        /// between the two marks. The <paramref name="message"/> is appended for traceability.
        /// </summary>
        /// <param name="marker">A stable identifier to group related marks (e.g., "FetchProfiles").</param>
        /// <param name="message">Free-form metadata describing the context (e.g., "after DB call").</param>
        public static void Mark(string marker, string message)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                throw new ArgumentException("Marker must not be null or whitespace.", nameof(marker));
            }

            long now = Stopwatch.GetTimestamp();
            int threadId = Thread.CurrentThread.ManagedThreadId;

            if (lastTimestamps.TryGetValue(marker, out long previous))
            {
                double deltaMs = (now - previous) * 1000.0 / Frequency;

                Debug.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"[DebugStopwatch] marker=\"{marker}\" Δ={deltaMs:F3} ms — {message} (T{threadId})"));
            }
            else
            {
                Debug.WriteLine(
                    $"[DebugStopwatch] marker=\"{marker}\" first mark — {message} (T{threadId})");
            }

            lastTimestamps[marker] = now;
        }

        /// <summary>
        /// Removes any stored timestamp for the specified marker, so the next <see cref="Mark"/> with that
        /// marker will be treated as the first mark.
        /// </summary>
        /// <param name="marker">The marker to reset.</param>
        public static void Reset(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                throw new ArgumentException("Marker must not be null or whitespace.", nameof(marker));
            }

            lastTimestamps.TryRemove(marker, out _);
        }

        /// <summary>
        /// Clears all stored timestamps for all markers.
        /// </summary>
        public static void ClearAll()
        {
            lastTimestamps.Clear();
        }

        #endregion
    }
}
