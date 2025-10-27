// -------------------------------------------------------------------------------------
// <copyright file="ProcessUsageSamplerByHwnd.cs">
//   Copyright © VoilaTile.
// </copyright>
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Selects how per-process memory is measured and aggregated.
    /// </summary>
    internal enum MemoryMetric
    {
        /// <summary>
        /// Uses <b>Private Bytes</b> (Commit size).
        /// </summary>
        CommitPrivateBytes,

        /// <summary>
        /// Uses <b>Working Set - Private</b> (resident private pages).
        /// </summary>
        PrivateWorkingSet,
    }

    /// <summary>
    /// Long-lived sampler that can be retargeted to different <c>HWND</c>s without reinitializing PDH.
    /// PDH counters are wildcarded (<c>\Process(*)</c>) and remain open; retargeting only refreshes the PID group.
    /// </summary>
    internal sealed class ProcessUsageSamplerByHwnd : IDisposable
    {
        #region Fields

        /// <summary>
        /// The interval between samples in milliseconds.
        /// </summary>
        private const int SampleIntervalMs = 1000;

        /// <summary>
        /// The initial warmup gap in milliseconds.
        /// </summary>
        private const int WarmupGapMs = 250;

        /// <summary>
        /// The initial delay before sampling in milliseconds.
        /// </summary>
        private const int InitialDelayMs = 60;

        /// <summary>
        /// The number of samples between group refreshes.
        /// </summary>
        private const int GroupRefreshEveryNSamples = 5;

        /// <summary>
        /// PDH format constant for double values.
        /// </summary>
        private const uint PdhFmtDouble = 0x00000200;

        /// <summary>
        /// PDH format constant for large integer values.
        /// </summary>
        private const uint PdhFmtLarge = 0x00000400;

        /// <summary>
        /// PDH error code indicating more data is available.
        /// </summary>
        private const int PdhMoreData = unchecked((int)0x800007D2);

        /// <summary>
        /// Window ancestor constant for root owner.
        /// </summary>
        private const uint GaRootOwner = 3;

        /// <summary>
        /// Process access rights for limited information.
        /// </summary>
        private const uint ProcessQueryLimitedInformation = 0x1000;

        /// <summary>
        /// Alpha value for CPU exponential moving average.
        /// </summary>
        private const double CpuEmaAlpha = 0.40;

        /// <summary>
        /// Gate for synchronizing start/stop operations.
        /// </summary>
        private readonly object startStopGate = new();

        /// <summary>
        /// Gate for synchronizing target window operations.
        /// </summary>
        private readonly object targetGate = new();

        /// <summary>
        /// The selected memory metric for measurements.
        /// </summary>
        private readonly MemoryMetric memoryMetric;

        /// <summary>
        /// The cancellation token source for the background sampling loop.
        /// </summary>
        private CancellationTokenSource? cts;

        /// <summary>
        /// The target window handle being monitored.
        /// </summary>
        private IntPtr targetHwnd = IntPtr.Zero;

        /// <summary>
        /// The set of process IDs in the current monitoring group.
        /// </summary>
        private HashSet<int> groupPids = new();

        /// <summary>
        /// The base process name of the anchor process.
        /// </summary>
        private string anchorBaseName = string.Empty;

        /// <summary>
        /// The executable name of the anchor process.
        /// </summary>
        private string anchorExeName = string.Empty;

        /// <summary>
        /// The directory containing the anchor process executable.
        /// </summary>
        private string anchorExeDir = string.Empty;

        /// <summary>
        /// The version counter for target updates.
        /// </summary>
        private int targetVersion;

        /// <summary>
        /// The PDH query handle.
        /// </summary>
        private IntPtr hQuery = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for process IDs.
        /// </summary>
        private IntPtr hPid = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for parent process IDs.
        /// </summary>
        private IntPtr hPpid = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for CPU usage.
        /// </summary>
        private IntPtr hCpu = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for private working set.
        /// </summary>
        private IntPtr hPrivWs = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for private bytes.
        /// </summary>
        private IntPtr hPrivateBytes = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for thread count.
        /// </summary>
        private IntPtr hThreadCount = IntPtr.Zero;

        /// <summary>
        /// The PDH counter handle for handle count.
        /// </summary>
        private IntPtr hHandleCount = IntPtr.Zero;

        /// <summary>
        /// Flag indicating if PDH counters are ready.
        /// </summary>
        private bool pdhReady;

        /// <summary>
        /// Buffer for PDH counter data.
        /// </summary>
        private byte[] scratch = Array.Empty<byte>();

        /// <summary>
        /// Counter for tracking sample iterations.
        /// </summary>
        private int sampleCounter;

        /// <summary>
        /// The most recently sampled resource statistics.
        /// </summary>
        private volatile ProcessResourceStats? lastStats;

        /// <summary>
        /// Flag indicating if debug logging is enabled.
        /// </summary>
        private volatile bool debugEnabled;

        /// <summary>
        /// The exponential moving average of CPU usage.
        /// </summary>
        private double? cpuEma;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the ProcessUsageSamplerByHwnd class.
        /// </summary>
        /// <param name="memoryMetric">The memory metric to use for measurements.</param>
        public ProcessUsageSamplerByHwnd(MemoryMetric memoryMetric = MemoryMetric.PrivateWorkingSet)
        {
            this.memoryMetric = memoryMetric;
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a new sample is taken.
        /// </summary>
        public event Action<ProcessResourceStats>? OnSample;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the selected memory metric for measurements.
        /// </summary>
        public MemoryMetric SelectedMemoryMetric => this.memoryMetric;

        #endregion

        #region Methods

        /// <summary>
        /// Enables or disables debug logging.
        /// </summary>
        /// <param name="enable">True to enable debug logging, false to disable.</param>
        public void SetDebugLogging(bool enable)
        {
            this.debugEnabled = enable;
            if (enable)
            {
                this.LogDebug("Debug logging ENABLED");
            }
        }

        /// <summary>
        /// Starts the background sampling loop.
        /// </summary>
        public void Start()
        {
            lock (this.startStopGate)
            {
                if (this.cts is not null)
                {
                    return;
                }

                var localCts = new CancellationTokenSource();
                this.cts = localCts;
                CancellationToken token = localCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(InitialDelayMs, token).ConfigureAwait(false);

                        this.EnsurePdhQuery();

                        PdhCollectQueryData(this.hQuery);
                        await Task.Delay(WarmupGapMs, token).ConfigureAwait(false);

                        this.sampleCounter = 0;

                        while (!token.IsCancellationRequested)
                        {
                            IntPtr hwnd;
                            HashSet<int> group;
                            int version;
                            string family;
                            string exeName;
                            string exeDir;

                            lock (this.targetGate)
                            {
                                hwnd = this.targetHwnd;
                                group = this.groupPids;
                                version = this.targetVersion;
                                family = this.anchorBaseName;
                                exeName = this.anchorExeName;
                                exeDir = this.anchorExeDir;
                            }

                            if (hwnd == IntPtr.Zero || group.Count == 0)
                            {
                                await Task.Delay(SampleIntervalMs, token).ConfigureAwait(false);
                                continue;
                            }

                            PdhCollectQueryData(this.hQuery);

                            // Snapshot all counters we need for this tick.
                            var pidByInstance = this.ReadPidByInstance();
                            var ppidByInstance = this.ReadParentPidByInstance();
                            var cpuByInstance = this.ReadDoubleByInstance(this.hCpu);
                            var wsPrivByInstance = this.ReadLongByInstance(this.hPrivWs);
                            var privBytesByInst = this.ReadLongByInstance(this.hPrivateBytes);
                            var threadsByInstance = this.ReadLongByInstance(this.hThreadCount);
                            var handlesByInstance = this.ReadLongByInstance(this.hHandleCount);

                            this.sampleCounter++;
                            if ((this.sampleCounter % GroupRefreshEveryNSamples) == 0)
                            {
                                var refreshedSeed = this.BuildSeedFromPdh(pidByInstance, family, exeName, exeDir);
                                refreshedSeed.UnionWith(this.EnumerateFamilyPids(family, exeName, exeDir));
                                var refreshed = this.AugmentWithPdhDescendants(refreshedSeed, pidByInstance, ppidByInstance);
                                if (refreshed.Count > 0)
                                {
                                    lock (this.targetGate)
                                    {
                                        this.groupPids = refreshed;
                                    }

                                    this.LogDebug($"[v{version}] PDH refresh => groupSize={refreshed.Count}");
                                }
                            }

                            // ---- Per-PID aggregation (avoid double count with MAX across instances for point-in-time values) ----
                            var perPid = new Dictionary<int, (double cpu, long wsPriv, long priv, int threads, int handles, int instCount)>(group.Count);

                            foreach (var (inst, pid) in pidByInstance)
                            {
                                bool inGroup = group.Contains(pid);
                                if (!inGroup)
                                {
                                    continue;
                                }

                                // CPU is additive
                                double c = 0;
                                if (cpuByInstance.TryGetValue(inst, out var cv)) c = cv;

                                long ws = 0;
                                if (wsPrivByInstance.TryGetValue(inst, out var wsv)) ws = wsv;

                                long pv = 0;
                                if (privBytesByInst.TryGetValue(inst, out var pvx)) pv = pvx;

                                int th = 0;
                                if (threadsByInstance.TryGetValue(inst, out var thx)) th = unchecked((int)Math.Max(0, thx));

                                int hd = 0;
                                if (handlesByInstance.TryGetValue(inst, out var hdx)) hd = unchecked((int)Math.Max(0, hdx));

                                if (perPid.TryGetValue(pid, out var agg))
                                {
                                    long wsMax = ws > agg.wsPriv ? ws : agg.wsPriv;
                                    long pvMax = pv > agg.priv ? pv : agg.priv;
                                    int thMax = th > agg.threads ? th : agg.threads;
                                    int hdMax = hd > agg.handles ? hd : agg.handles;

                                    perPid[pid] = (agg.cpu + c, wsMax, pvMax, thMax, hdMax, agg.instCount + 1);
                                }
                                else
                                {
                                    perPid[pid] = (c, ws, pv, th, hd, 1);
                                }
                            }

                            // Optional: fill any missing PIDs with psapi as a last resort (rare).
                            if (group.Count > 0)
                            {
                                foreach (var pid in group)
                                {
                                    if (!perPid.TryGetValue(pid, out var agg) || (agg.wsPriv <= 0 && agg.priv <= 0))
                                    {
                                        if (this.TryGetProcessMemoryMetric(pid, out long fallback))
                                        {
                                            // Fallback only for memory; threads/handles we skip if missing.
                                            if (perPid.TryGetValue(pid, out var ex))
                                            {
                                                long wsMax = fallback > ex.wsPriv ? fallback : ex.wsPriv;
                                                perPid[pid] = (ex.cpu, wsMax, ex.priv, ex.threads, ex.handles, ex.instCount);
                                            }
                                            else
                                            {
                                                perPid[pid] = (0, fallback, 0, 0, 0, 0);
                                            }
                                        }
                                    }
                                }
                            }

                            // ---- Group totals ----
                            double cpuSum = 0;
                            long wsPrivSum = 0;
                            long privBytesSum = 0;
                            int threadsSum = 0;
                            int handlesSum = 0;

                            foreach (var v in perPid.Values)
                            {
                                cpuSum += v.cpu;
                                wsPrivSum += v.wsPriv;
                                privBytesSum += v.priv;
                                threadsSum += v.threads;
                                handlesSum += v.handles;
                            }

                            // CPU %
                            int cores = Math.Max(1, Environment.ProcessorCount);
                            double cpuPct = Math.Min(100.0, cpuSum / cores);
                            double cpuPctSmoothed = this.UpdateCpuEma(cpuPct);

                            // ---- Denominators for shares ----

                            // Sum WS-Private for ALL processes (aggregate by PID with MAX across instances).
                            ulong wsAllProcs = 0;
                            var wsByPidAll = new Dictionary<int, ulong>(256);
                            foreach (var (inst, pid) in pidByInstance)
                            {
                                if (pid == 0) continue; // skip Idle
                                if (wsPrivByInstance.TryGetValue(inst, out var ws))
                                {
                                    ulong val = (ulong)Math.Max(0, ws);
                                    if (wsByPidAll.TryGetValue(pid, out var cur))
                                    {
                                        if (val > cur) wsByPidAll[pid] = val; // MAX
                                    }
                                    else
                                    {
                                        wsByPidAll[pid] = val;
                                    }
                                }
                            }
                            foreach (var b in wsByPidAll.Values) wsAllProcs += b;

                            // System commit total (bytes)
                            ulong commitTotalBytes = 0;
                            if (GetPerformanceInfo(out PERFORMANCE_INFORMATION pi, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
                            {
                                commitTotalBytes = (ulong)pi.CommitTotal * (ulong)pi.PageSize;
                            }

                            double wsSharePercent = (wsAllProcs > 0) ? ((double)wsPrivSum / wsAllProcs * 100.0) : 0.0;
                            double commitSharePercent = (commitTotalBytes > 0) ? ((double)privBytesSum / commitTotalBytes * 100.0) : 0.0;

                            var stats = new ProcessResourceStats(
                                CpuPercent: cpuPctSmoothed,
                                ProcessCount: group.Count,
                                WorkingSetPrivateBytes: wsPrivSum,
                                PrivateBytes: privBytesSum,
                                WsShareOfProcessesPercent: Math.Min(100.0, wsSharePercent),
                                CommitShareOfSystemPercent: Math.Min(100.0, commitSharePercent),
                                ThreadCount: threadsSum,
                                HandleCount: handlesSum);

                            this.lastStats = stats;
                            this.OnSample?.Invoke(stats);

                            if (this.debugEnabled)
                            {
                                this.LogDebug($"[tick v{version}] group:{group.Count} pidItems:{pidByInstance.Count} cpuItems:{cpuByInstance.Count} wsPrivItems:{wsPrivByInstance.Count} privItems:{privBytesByInst.Count} thrItems:{threadsByInstance.Count} hdlItems:{handlesByInstance.Count}");
                                foreach (var pid in perPid.Keys.OrderBy(p => p))
                                {
                                    var (cpu, ws, pv, th, hd, inst) = perPid[pid];
                                    string name = this.SafeProcName(pid);
                                    this.LogDebug($"    PID {pid} ({name}) inst:{inst} cpuRaw:{cpu:F2} WS-Priv:{this.FormatBytes(ws)} Private:{this.FormatBytes(pv)} Threads:{th} Handles:{hd}");
                                }

                                this.LogDebug($"    GROUP WS-Priv:{this.FormatBytes(wsPrivSum)}  Private:{this.FormatBytes(privBytesSum)}  WS share:{stats.WsShareOfProcessesPercent:F2}%  Commit share:{stats.CommitShareOfSystemPercent:F2}%");
                                this.LogDebug($"    SUM cpuRaw:{cpuSum:F2} / cores:{cores} => cpu%:{cpuPct:F1} (ema:{cpuPctSmoothed:F1})");
                            }

                            await Task.Delay(SampleIntervalMs, token).ConfigureAwait(false);
                        }
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        this.LogDebug($"Background loop error: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Stops the background sampling loop and releases PDH resources.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource? toCancel = null;

            lock (this.startStopGate)
            {
                if (this.cts is null)
                {
                    return;
                }

                toCancel = this.cts;
                this.cts = null;
            }

            try { toCancel?.Cancel(); } catch { }
            try { toCancel?.Dispose(); } catch { }

            if (this.hPid != IntPtr.Zero) { PdhRemoveCounter(this.hPid); this.hPid = IntPtr.Zero; }
            if (this.hPpid != IntPtr.Zero) { PdhRemoveCounter(this.hPpid); this.hPpid = IntPtr.Zero; }
            if (this.hCpu != IntPtr.Zero) { PdhRemoveCounter(this.hCpu); this.hCpu = IntPtr.Zero; }
            if (this.hPrivWs != IntPtr.Zero) { PdhRemoveCounter(this.hPrivWs); this.hPrivWs = IntPtr.Zero; }
            if (this.hPrivateBytes != IntPtr.Zero) { PdhRemoveCounter(this.hPrivateBytes); this.hPrivateBytes = IntPtr.Zero; }
            if (this.hThreadCount != IntPtr.Zero) { PdhRemoveCounter(this.hThreadCount); this.hThreadCount = IntPtr.Zero; }
            if (this.hHandleCount != IntPtr.Zero) { PdhRemoveCounter(this.hHandleCount); this.hHandleCount = IntPtr.Zero; }

            if (this.hQuery != IntPtr.Zero) { PdhCloseQuery(this.hQuery); this.hQuery = IntPtr.Zero; }

            this.pdhReady = false;

            lock (this.targetGate)
            {
                this.groupPids = new HashSet<int>();
                this.anchorBaseName = string.Empty;
                this.anchorExeName = string.Empty;
                this.anchorExeDir = string.Empty;
                this.targetHwnd = IntPtr.Zero;
                this.targetVersion++;
            }

            this.lastStats = null;
            this.cpuEma = null;

            this.LogDebug("Stopped and PDH released.");
        }

        /// <summary>
        /// Retargets the sampler to a specific window handle. Optionally triggers an immediate sample pulse.
        /// </summary>
        /// <param name="hwnd">The window handle to target.</param>
        /// <param name="immediate">Whether to take an immediate sample.</param>
        public void SetTarget(IntPtr hwnd, bool immediate = true)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            var root = GetAncestor(hwnd, GaRootOwner);
            if (root == IntPtr.Zero)
            {
                root = hwnd;
            }

            _ = GetWindowThreadProcessId(root, out uint pidU);
            int rootPid = unchecked((int)pidU);

            string baseName = string.Empty;
            string exePath = string.Empty;

            try
            {
                using var p = Process.GetProcessById(rootPid);
                baseName = p.ProcessName;
            }
            catch { }

            if (!this.TryGetProcessImagePath(rootPid, out exePath))
            {
                try
                {
                    using var p = Process.GetProcessById(rootPid);
                    exePath = p.MainModule?.FileName ?? string.Empty;
                }
                catch { }
            }

            var exeNameLc = this.SafeFileNameLower(exePath);
            var exeDirLc = this.SafeDirectoryLowerWithSlash(exePath);

            try
            {
                this.EnsurePdhQuery();
                PdhCollectQueryData(this.hQuery);

                var pidByInstance = this.ReadPidByInstance();
                var ppidByInstance = this.ReadParentPidByInstance();

                var seed = this.BuildSeedFromPdh(pidByInstance, baseName, exeNameLc, exeDirLc);
                seed.Add(rootPid);
                seed.UnionWith(this.EnumerateFamilyPids(baseName, exeNameLc, exeDirLc));

                var augmented = this.AugmentWithPdhDescendants(seed, pidByInstance, ppidByInstance);

                lock (this.targetGate)
                {
                    this.targetHwnd = hwnd;
                    this.groupPids = augmented;
                    this.anchorBaseName = baseName;
                    this.anchorExeName = exeNameLc;
                    this.anchorExeDir = exeDirLc;
                    this.targetVersion++;
                }

                this.LogDebug($"SetTarget hwnd=0x{hwnd.ToInt64():X} rootPid={rootPid} baseName='{baseName}' exeName='{exeNameLc}' exeDir='{exeDirLc}' groupSize={augmented.Count} group=[{string.Join(",", augmented.OrderBy(p => p))}]");

                if (immediate && this.pdhReady)
                {
                    // Quick pulse using current snapshot.
                    PdhCollectQueryData(this.hQuery);

                    var cpuByInstance = this.ReadDoubleByInstance(this.hCpu);
                    var wsPrivByInstance = this.ReadLongByInstance(this.hPrivWs);
                    var privBytesByInst = this.ReadLongByInstance(this.hPrivateBytes);
                    var threadsByInstance = this.ReadLongByInstance(this.hThreadCount);
                    var handlesByInstance = this.ReadLongByInstance(this.hHandleCount);

                    this.ImmediatePulse(augmented, pidByInstance, cpuByInstance, wsPrivByInstance, privBytesByInst, threadsByInstance, handlesByInstance);
                }
            }
            catch (Exception ex)
            {
                this.LogDebug($"SetTarget error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the latest sampled statistics, if any.
        /// </summary>
        /// <returns>The latest process resource statistics, or null if not available.</returns>
        public ProcessResourceStats? TrySnapshotCurrent() => this.lastStats;

        /// <summary>
        /// Releases all resources used by the sampler.
        /// </summary>
        public void Dispose()
        {
            this.Stop();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Builds initial set of process IDs from PDH data matching the target process family.
        /// </summary>
        /// <param name="pidByInstance">Mapping of PDH instance names to process IDs.</param>
        /// <param name="baseName">Base process name to match.</param>
        /// <param name="exeNameLc">Lowercase executable name to match.</param>
        /// <param name="exeDirLc">Lowercase directory path to match.</param>
        /// <returns>Set of matching process IDs.</returns>
        private HashSet<int> BuildSeedFromPdh(
            Dictionary<string, int> pidByInstance,
            string baseName,
            string exeNameLc,
            string exeDirLc)
        {
            var set = new HashSet<int>();
            int added = 0;

            foreach (var (inst, pid) in pidByInstance)
            {
                bool include = false;
                string reason = string.Empty;

                var instBase = this.GetBaseInstanceName(inst);
                if (!string.IsNullOrEmpty(baseName) &&
                    (instBase.Contains(baseName, StringComparison.OrdinalIgnoreCase)))
                {
                    include = true;
                    reason = $"name:{instBase}";
                }
                else
                {
                    if (!string.IsNullOrEmpty(exeNameLc) && !string.IsNullOrEmpty(exeDirLc))
                    {
                        if (this.TryGetProcessImagePath(pid, out string pPath))
                        {
                            string pName = this.SafeFileNameLower(pPath);
                            string pDir = this.SafeDirectoryLowerWithSlash(pPath);
                            if (pName == exeNameLc && pDir == exeDirLc)
                            {
                                include = true;
                                reason = $"path:{pName}@{pDir}";
                            }
                        }
                    }
                }

                if (include && set.Add(pid))
                {
                    added++;
                    if (this.debugEnabled)
                    {
                        this.LogDebug($"    + grouped PID {pid} ({this.SafeProcName(pid)}) via {reason}");
                    }
                }
            }

            if (this.debugEnabled)
            {
                this.LogDebug($"BuildSeedFromPdh base='{baseName}' exeName='{exeNameLc}' exeDir='{exeDirLc}' => added:{added} size:{set.Count}");
            }

            return set;
        }

        /// <summary>
        /// Enumerates all processes belonging to the same process family using Process.GetProcesses.
        /// </summary>
        /// <param name="baseName">Base process name to match.</param>
        /// <param name="exeNameLc">Lowercase executable name to match.</param>
        /// <param name="exeDirLc">Lowercase directory path to match.</param>
        /// <returns>Set of process IDs belonging to the family.</returns>
        private HashSet<int> EnumerateFamilyPids(string baseName, string exeNameLc, string exeDirLc)
        {
            var result = new HashSet<int>();

            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    int pid = -1;
                    try
                    {
                        pid = p.Id;

                        bool include = false;

                        if (!string.IsNullOrEmpty(baseName))
                        {
                            string pname = p.ProcessName;
                            if (!string.IsNullOrEmpty(pname) &&
                                (pname.Contains(baseName, StringComparison.OrdinalIgnoreCase)))
                            {
                                include = true;
                            }
                        }

                        if (!include && !string.IsNullOrEmpty(exeNameLc) && !string.IsNullOrEmpty(exeDirLc))
                        {
                            if (this.TryGetProcessImagePath(pid, out string full))
                            {
                                string pName = this.SafeFileNameLower(full);
                                string pDir = this.SafeDirectoryLowerWithSlash(full);
                                if (pName == exeNameLc && pDir == exeDirLc)
                                {
                                    include = true;
                                }
                            }
                        }

                        if (include)
                        {
                            result.Add(pid);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }
            }
            catch { }

            if (this.debugEnabled)
            {
                this.LogDebug($"EnumerateFamilyPids base='{baseName}' exe='{exeNameLc}' dir='{exeDirLc}' => size:{result.Count}");
            }

            return result;
        }

        /// <summary>
        /// Adds child processes to the seed set by following parent-child relationships from PDH data.
        /// </summary>
        /// <param name="seed">Initial set of process IDs.</param>
        /// <param name="pidByInstance">Mapping of PDH instance names to process IDs.</param>
        /// <param name="ppidByInstance">Mapping of PDH instance names to parent process IDs.</param>
        /// <returns>Augmented set including child processes.</returns>
        private HashSet<int> AugmentWithPdhDescendants(
            HashSet<int> seed,
            Dictionary<string, int> pidByInstance,
            Dictionary<string, int> ppidByInstance)
        {
            if (seed.Count == 0 || ppidByInstance.Count == 0)
            {
                return seed;
            }

            var children = new Dictionary<int, List<int>>(256);
            foreach (var inst in pidByInstance.Keys)
            {
                int pid = pidByInstance[inst];
                if (!ppidByInstance.TryGetValue(inst, out int parent))
                {
                    continue;
                }

                if (!children.TryGetValue(parent, out var list))
                {
                    children[parent] = list = new List<int>(2);
                }

                list.Add(pid);
            }

            var set = new HashSet<int>(seed);
            var q = new Queue<int>(seed);

            while (q.Count > 0)
            {
                int p = q.Dequeue();
                if (!children.TryGetValue(p, out var kids))
                {
                    continue;
                }

                for (int i = 0; i < kids.Count; i++)
                {
                    int k = kids[i];
                    if (set.Add(k))
                    {
                        q.Enqueue(k);
                    }
                }
            }

            if (this.debugEnabled)
            {
                this.LogDebug($"AugmentWithPdhDescendants seed:{seed.Count} => final:{set.Count}");
            }

            return set;
        }

        /// <summary>
        /// Extracts the base process name from a PDH instance name by removing the instance number suffix.
        /// </summary>
        /// <param name="instanceName">The PDH instance name.</param>
        /// <returns>The base process name without instance number.</returns>
        private string GetBaseInstanceName(string instanceName)
        {
            int hash = instanceName.IndexOf('#');
            return hash >= 0 ? instanceName.Substring(0, hash) : instanceName;
        }

        /// <summary>
        /// Initializes PDH query and adds required performance counters.
        /// </summary>
        private void EnsurePdhQuery()
        {
            if (this.pdhReady)
            {
                return;
            }

            this.Check(PdhOpenQuery(IntPtr.Zero, IntPtr.Zero, out this.hQuery));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\ID Process", IntPtr.Zero, out this.hPid));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Creating Process ID", IntPtr.Zero, out this.hPpid));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\% Processor Time", IntPtr.Zero, out this.hCpu));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Working Set - Private", IntPtr.Zero, out this.hPrivWs));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Private Bytes", IntPtr.Zero, out this.hPrivateBytes));

            // NEW: thread/handle counts
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Thread Count", IntPtr.Zero, out this.hThreadCount));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Handle Count", IntPtr.Zero, out this.hHandleCount));

            this.pdhReady = true;
            this.LogDebug("PDH query initialized (ID, Creating PID, %CPU, WS-Private, Private Bytes, Thread Count, Handle Count).");
        }

        /// <summary>
        /// Reads the process IDs for all process instances from PDH.
        /// </summary>
        /// <returns>A dictionary mapping PDH instance names to process IDs.</returns>
        private Dictionary<string, int> ReadPidByInstance()
        {
            var result = new Dictionary<string, int>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsLarge(this.hPid, PdhFmtLarge, (name, value) => result[name] = unchecked((int)value));
            return result;
        }

        /// <summary>
        /// Reads the parent process IDs for all process instances from PDH.
        /// </summary>
        /// <returns>A dictionary mapping PDH instance names to parent process IDs.</returns>
        private Dictionary<string, int> ReadParentPidByInstance()
        {
            var result = new Dictionary<string, int>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsLarge(this.hPpid, PdhFmtLarge, (name, value) => result[name] = unchecked((int)value));
            return result;
        }

        /// <summary>
        /// Reads double-precision counter values for all instances of a specified PDH counter.
        /// </summary>
        /// <param name="hCounter">Handle to the PDH counter.</param>
        /// <returns>A dictionary mapping PDH instance names to counter values.</returns>
        private Dictionary<string, double> ReadDoubleByInstance(IntPtr hCounter)
        {
            var result = new Dictionary<string, double>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsDouble(hCounter, PdhFmtDouble, (name, value) => result[name] = value);
            return result;
        }

        /// <summary>
        /// Reads 64-bit integer counter values for all instances of a specified PDH counter.
        /// </summary>
        /// <param name="hCounter">Handle to the PDH counter.</param>
        /// <returns>A dictionary mapping PDH instance names to counter values.</returns>
        private Dictionary<string, long> ReadLongByInstance(IntPtr hCounter)
        {
            var result = new Dictionary<string, long>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsLarge(hCounter, PdhFmtLarge, (name, value) => result[name] = value);
            return result;
        }

        /// <summary>
        /// Reads formatted double-precision counter values from PDH.
        /// </summary>
        /// <param name="hCounter">Handle to the PDH counter.</param>
        /// <param name="fmt">The PDH data format to use.</param>
        /// <param name="onItem">Callback invoked for each counter instance value.</param>
        private void ReadCounterArrayItemsDouble(IntPtr hCounter, uint fmt, Action<string, double> onItem)
        {
            uint bufBytes = 0;
            uint num = 0;
            int status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, IntPtr.Zero);
            if (status != PdhMoreData) return;

            this.EnsureScratch((int)bufBytes);

            var handle = GCHandle.Alloc(this.scratch, GCHandleType.Pinned);
            try
            {
                IntPtr bufPinned = handle.AddrOfPinnedObject();

                status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, bufPinned);
                if (status != 0 && status != 1) return;

                int itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM_DOUBLE>();
                IntPtr cur = bufPinned;

                for (int i = 0; i < num; i++)
                {
                    var it = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM_DOUBLE>(cur);
                    string? name = Marshal.PtrToStringUni(it.szName);
                    if (!string.IsNullOrEmpty(name))
                    {
                        onItem(name, it.FmtValue.doubleValue);
                    }

                    cur = IntPtr.Add(cur, itemSize);
                }
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }

        private void ReadCounterArrayItemsLarge(IntPtr hCounter, uint fmt, Action<string, long> onItem)
        {
            uint bufBytes = 0;
            uint num = 0;
            int status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, IntPtr.Zero);
            if (status != PdhMoreData) return;

            this.EnsureScratch((int)bufBytes);

            var handle = GCHandle.Alloc(this.scratch, GCHandleType.Pinned);
            try
            {
                IntPtr bufPinned = handle.AddrOfPinnedObject();

                status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, bufPinned);
                if (status != 0 && status != 1) return;

                int itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM_LARGE>();
                IntPtr cur = bufPinned;

                for (int i = 0; i < num; i++)
                {
                    var it = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM_LARGE>(cur);
                    string? name = Marshal.PtrToStringUni(it.szName);
                    if (!string.IsNullOrEmpty(name))
                    {
                        onItem(name, it.FmtValue.largeValue);
                    }

                    cur = IntPtr.Add(cur, itemSize);
                }
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }

        private void EnsureScratch(int bytes)
        {
            if (this.scratch.Length < bytes)
            {
                Array.Resize(ref this.scratch, Math.Max(bytes, this.scratch.Length == 0 ? 4096 : this.scratch.Length * 2));
            }
        }

        // Immediate pulse updated to emit the new record shape
        private void ImmediatePulse(
            HashSet<int> group,
            Dictionary<string, int> pidByInstance,
            Dictionary<string, double> cpuByInstance,
            Dictionary<string, long> wsPrivByInstance,
            Dictionary<string, long> privBytesByInst,
            Dictionary<string, long> threadsByInstance,
            Dictionary<string, long> handlesByInstance)
        {
            try
            {
                var perPid = new Dictionary<int, (double cpu, long wsPriv, long priv, int th, int hd, int inst)>(group.Count);

                foreach (var (inst, pid) in pidByInstance)
                {
                    if (!group.Contains(pid)) continue;

                    double c = 0; if (cpuByInstance.TryGetValue(inst, out var cv)) c = cv;
                    long ws = 0; if (wsPrivByInstance.TryGetValue(inst, out var wsv)) ws = wsv;
                    long pv = 0; if (privBytesByInst.TryGetValue(inst, out var pvx)) pv = pvx;
                    int th = 0; if (threadsByInstance.TryGetValue(inst, out var thx)) th = unchecked((int)Math.Max(0, thx));
                    int hd = 0; if (handlesByInstance.TryGetValue(inst, out var hdx)) hd = unchecked((int)Math.Max(0, hdx));

                    if (perPid.TryGetValue(pid, out var agg))
                    {
                        long wsMax = ws > agg.wsPriv ? ws : agg.wsPriv;
                        long pvMax = pv > agg.priv ? pv : agg.priv;
                        int thMax = th > agg.th ? th : agg.th;
                        int hdMax = hd > agg.hd ? hd : agg.hd;

                        perPid[pid] = (agg.cpu + c, wsMax, pvMax, thMax, hdMax, agg.inst + 1);
                    }
                    else
                    {
                        perPid[pid] = (c, ws, pv, th, hd, 1);
                    }
                }

                double cpuSum = 0;
                long wsPrivSum = 0;
                long privBytesSum = 0;
                int threadsSum = 0;
                int handlesSum = 0;

                foreach (var v in perPid.Values)
                {
                    cpuSum += v.cpu;
                    wsPrivSum += v.wsPriv;
                    privBytesSum += v.priv;
                    threadsSum += v.th;
                    handlesSum += v.hd;
                }

                // Denominators
                ulong wsAllProcs = 0;
                var wsByPidAll = new Dictionary<int, ulong>(256);
                foreach (var (inst, pid) in pidByInstance)
                {
                    if (pid == 0) continue;
                    if (wsPrivByInstance.TryGetValue(inst, out var ws))
                    {
                        ulong val = (ulong)Math.Max(0, ws);
                        if (wsByPidAll.TryGetValue(pid, out var cur))
                        {
                            if (val > cur) wsByPidAll[pid] = val;
                        }
                        else
                        {
                            wsByPidAll[pid] = val;
                        }
                    }
                }
                foreach (var b in wsByPidAll.Values) wsAllProcs += b;

                ulong commitTotalBytes = 0;
                if (GetPerformanceInfo(out PERFORMANCE_INFORMATION pi, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
                {
                    commitTotalBytes = (ulong)pi.CommitTotal * (ulong)pi.PageSize;
                }

                double wsSharePercent = (wsAllProcs > 0) ? ((double)wsPrivSum / wsAllProcs * 100.0) : 0.0;
                double commitSharePercent = (commitTotalBytes > 0) ? ((double)privBytesSum / commitTotalBytes * 100.0) : 0.0;

                int cores = Math.Max(1, Environment.ProcessorCount);
                double cpuPct = Math.Min(100.0, cpuSum / cores);
                double cpuPctSmoothed = this.UpdateCpuEma(cpuPct);

                var stats = new ProcessResourceStats(
                    CpuPercent: cpuPctSmoothed,
                    ProcessCount: group.Count,
                    WorkingSetPrivateBytes: wsPrivSum,
                    PrivateBytes: privBytesSum,
                    WsShareOfProcessesPercent: Math.Min(100.0, wsSharePercent),
                    CommitShareOfSystemPercent: Math.Min(100.0, commitSharePercent),
                    ThreadCount: threadsSum,
                    HandleCount: handlesSum);

                this.lastStats = stats;
                this.OnSample?.Invoke(stats);

                if (this.debugEnabled)
                {
                    this.LogDebug($"[immediate] group:{group.Count} WS-Priv:{this.FormatBytes(wsPrivSum)} Private:{this.FormatBytes(privBytesSum)} WSshare:{wsSharePercent:F2}% CommShare:{commitSharePercent:F2}% CPUema:{cpuPctSmoothed:F1}");
                }
            }
            catch (Exception ex)
            {
                this.LogDebug($"Immediate pulse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the exponential moving average for CPU usage.
        /// </summary>
        /// <param name="latest">The latest CPU usage value.</param>
        /// <returns>The updated EMA value.</returns>
        private double UpdateCpuEma(double latest)
        {
            if (!this.cpuEma.HasValue)
            {
                this.cpuEma = latest;
                return latest;
            }

            this.cpuEma = CpuEmaAlpha * latest + (1.0 - CpuEmaAlpha) * this.cpuEma.Value;
            return this.cpuEma.Value;
        }

        /// <summary>
        /// Queries the total physical memory installed in the system.
        /// </summary>
        /// <returns>Total physical memory in bytes, or 0 if query fails.</returns>
        private ulong QueryTotalPhysicalMemory()
        {
            try
            {
                MEMORYSTATUSEX s = default;
                s.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (GlobalMemoryStatusEx(ref s))
                {
                    return s.ullTotalPhys;
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// Formats a byte count into a human-readable string with appropriate size units.
        /// </summary>
        /// <param name="bytes">The number of bytes to format.</param>
        /// <returns>A formatted string with size units (B, KB, MB, or GB).</returns>
        private string FormatBytes(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB) return (bytes / (double)GB).ToString("F2", CultureInfo.InvariantCulture) + " GB";
            if (bytes >= MB) return (bytes / (double)MB).ToString("F2", CultureInfo.InvariantCulture) + " MB";
            if (bytes >= KB) return (bytes / (double)KB).ToString("F2", CultureInfo.InvariantCulture) + " KB";
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        private void LogDebug(string message)
        {
            if (!this.debugEnabled) return;
            Debug.WriteLine($"[Sampler] {message}");
        }

        /// <summary>
        /// Safely retrieves the process name for a given process ID.
        /// </summary>
        /// <param name="pid">The process ID.</param>
        /// <returns>The process name, or "?" if the process cannot be accessed.</returns>
        private string SafeProcName(int pid)
        {
            try { using var p = Process.GetProcessById(pid); return p.ProcessName; }
            catch { return "?"; }
        }

        /// <summary>
        /// Safely extracts the lowercase filename from a path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The lowercase filename, or empty string if the path is invalid.</returns>
        private string SafeFileNameLower(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return string.Empty;
                return System.IO.Path.GetFileName(path).ToLowerInvariant();
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Safely extracts the lowercase directory path from a file path, ensuring it ends with a backslash.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The lowercase directory path with trailing backslash, or empty string if invalid.</returns>
        private string SafeDirectoryLowerWithSlash(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return string.Empty;

                string? dir = System.IO.Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) return string.Empty;

                if (!dir.EndsWith("\\", StringComparison.Ordinal)) dir += "\\";
                return dir.ToLowerInvariant();
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Attempts to get the full executable path of a process using native Windows APIs.
        /// </summary>
        /// <param name="pid">The process ID.</param>
        /// <param name="fullPath">When successful, contains the full path to the process executable.</param>
        /// <returns>True if the path was successfully retrieved, false otherwise.</returns>
        private bool TryGetProcessImagePath(int pid, out string fullPath)
        {
            fullPath = string.Empty;
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
                if (h == IntPtr.Zero) return false;

                var sb = new StringBuilder(32768);
                int size = sb.Capacity;
                if (!QueryFullProcessImageName(h, 0, sb, ref size)) return false;

                fullPath = sb.ToString(0, size);
                return !string.IsNullOrEmpty(fullPath);
            }
            catch { return false; }
            finally
            {
                if (h != IntPtr.Zero)
                {
                    try { CloseHandle(h); } catch { }
                }
            }
        }

        /// <summary>
        /// Attempts to retrieve the selected memory metric value for a process using native Windows APIs.
        /// </summary>
        /// <param name="pid">The process ID.</param>
        /// <param name="value">When successful, contains the memory metric value in bytes.</param>
        /// <returns>True if the memory metric was successfully retrieved, false otherwise.</returns>
        private bool TryGetProcessMemoryMetric(int pid, out long value)
        {
            value = 0;
            IntPtr h = IntPtr.Zero;

            try
            {
                h = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
                if (h == IntPtr.Zero) return false;

                PROCESS_MEMORY_COUNTERS_EX pmc = default;
                pmc.cb = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX>();

                if (!GetProcessMemoryInfo(h, out pmc, pmc.cb)) return false;

                value = this.memoryMetric == MemoryMetric.CommitPrivateBytes
                    ? (long)pmc.PrivateUsage
                    : (long)pmc.WorkingSetSize;

                return true;
            }
            catch { return false; }
            finally
            {
                if (h != IntPtr.Zero)
                {
                    try { CloseHandle(h); } catch { }
                }
            }
        }

        /// <summary>
        /// Checks a PDH status code and throws an exception if it indicates an error.
        /// </summary>
        /// <param name="status">The PDH status code to check.</param>
        /// <exception cref="InvalidOperationException">Thrown when the status indicates an error.</exception>
        private void Check(int status)
        {
            if (status != 0 && status != 1)
            {
                throw new InvalidOperationException("PDH error: 0x" + status.ToString("X8", CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region Interop

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern int PdhOpenQuery(IntPtr dataSource, IntPtr userData, out IntPtr phQuery);

        [DllImport("pdh.dll")]
        private static extern int PdhCloseQuery(IntPtr hQuery);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern int PdhAddCounter(IntPtr hQuery, string counterPath, IntPtr userData, out IntPtr phCounter);

        [DllImport("pdh.dll")]
        private static extern int PdhRemoveCounter(IntPtr hCounter);

        [DllImport("pdh.dll")]
        private static extern int PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode, EntryPoint = "PdhGetFormattedCounterArrayW")]
        private static extern int PdhGetFormattedCounterArray(
            IntPtr hCounter,
            uint dwFormat,
            ref uint lpdwBufferSize,
            ref uint lpdwNumItems,
            IntPtr itemBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_DOUBLE
        {
            public uint CStatus;
            public double doubleValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_LARGE
        {
            public uint CStatus;
            public long largeValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_ITEM_DOUBLE
        {
            public IntPtr szName;
            public PDH_FMT_COUNTERVALUE_DOUBLE FmtValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_ITEM_LARGE
        {
            public IntPtr szName;
            public PDH_FMT_COUNTERVALUE_LARGE FmtValue;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint Size;
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonpaged;
            public IntPtr PageSize;
            public IntPtr HandleCount;
            public IntPtr ProcessCount;
            public IntPtr ThreadCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_MEMORY_COUNTERS_EX
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(
            IntPtr hProcess,
            out PROCESS_MEMORY_COUNTERS_EX counters,
            uint size);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        #endregion
    }
}

