namespace VoilaTile.Snapper.Records
{
    /// <summary>
    /// Immutable snapshot of resource usage for a process group.
    /// </summary>
    internal sealed record ProcessResourceStats(
        /// <summary>
        /// Gets the CPU utilization percentage (0–100) normalized across all logical cores.
        /// </summary>
        double CpuPercent,

        /// <summary>
        /// Gets the number of processes in the group.
        /// </summary>
        int ProcessCount,

        /// <summary>
        /// Gets the total working set (private) memory in bytes for all processes in the group.
        /// </summary>
        long WorkingSetPrivateBytes,

        /// <summary>
        /// Gets the total private (committed) memory in bytes for all processes in the group.
        /// </summary>
        long PrivateBytes,

        /// <summary>
        /// Gets the percentage share of the group's working set relative to all processes in the system.
        /// </summary>
        double WsShareOfProcessesPercent,

        /// <summary>
        /// Gets the percentage share of the group's committed memory relative to the total system commit.
        /// </summary>
        double CommitShareOfSystemPercent,

        /// <summary>
        /// Gets the total number of threads across all processes in the group.
        /// </summary>
        int ThreadCount,

        /// <summary>
        /// Gets the total number of handles across all processes in the group.
        /// </summary>
        int HandleCount);
}

