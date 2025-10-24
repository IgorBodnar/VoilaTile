namespace VoilaTile.Snapper.Records
{
    /// <summary>
    /// Snapshot of process resource usage.
    /// </summary>
    internal sealed class ProcessResourceStats
    {
        /// <summary>
        /// CPU percent (0..100) normalized across logical cores.
        /// </summary>
        public double CpuPercent { get; set; }

        /// <summary>
        /// Working set in bytes.
        /// </summary>
        public long WorkingSetBytes { get; set; }

        /// s<summary>
        /// RAM percent (0..100), WorkingSet / TotalPhysicalMemory.
        /// </summary>
        public double RamPercent { get; set; }
    }
}

