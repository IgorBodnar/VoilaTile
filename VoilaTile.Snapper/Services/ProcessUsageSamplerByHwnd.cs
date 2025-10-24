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
    /// Long-lived sampler that can be retargeted to different hwnds without reinitializing PDH.
    /// PDH counters are wildcarded (\Process(*)) and remain open; retargeting only refreshes the PID group.
    /// The PID group is built primarily by <b>executable family + session</b>, where family is matched by:
    ///  - PDH base instance name (e.g., "opera" from "opera#7"), or
    ///  - Executable path (same image name and same directory as the anchor process).
    /// Then we optionally augment the group with PDH descendants via "Creating Process ID".
    /// </summary>
    internal sealed class ProcessUsageSamplerByHwnd : IDisposable
    {
        #region Constants

        private const int SampleIntervalMs = 1000;
        private const int WarmupGapMs = 250;
        private const int InitialDelayMs = 60;
        private const int GroupRefreshEveryNSamples = 5;

        private const uint PDH_FMT_DOUBLE = 0x00000200;
        private const uint PDH_FMT_LARGE = 0x00000400;
        private const int PDH_MORE_DATA = unchecked((int)0x800007D2);

        private const uint GA_ROOTOWNER = 3;

        // OpenProcess access flags for QueryFullProcessImageNameW (no admin required).
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        #endregion

        #region Fields

        /// <summary>
        /// Gate for start/stop lifecycle.
        /// </summary>
        private readonly object startStopGate = new();

        /// <summary>
        /// Gate for retarget operations (updates hwnd/group/family).
        /// </summary>
        private readonly object targetGate = new();

        /// <summary>
        /// Cancellation source for the background loop.
        /// </summary>
        private CancellationTokenSource? cts;

        /// <summary>
        /// Current hwnd we target (0 means no target yet).
        /// </summary>
        private IntPtr targetHwnd = IntPtr.Zero;

        /// <summary>
        /// Current PID group. Swapped atomically on retarget/refresh.
        /// </summary>
        private HashSet<int> groupPids = new();

        /// <summary>
        /// Anchored family base name (e.g., "opera").
        /// </summary>
        private string anchorBaseName = string.Empty;

        /// <summary>
        /// Anchored executable file name (e.g., "opera.exe"), lowercased.
        /// </summary>
        private string anchorExeName = string.Empty;

        /// <summary>
        /// Anchored executable directory (normalized, trailing backslash), lowercased.
        /// </summary>
        private string anchorExeDir = string.Empty;

        /// <summary>
        /// Session id for the anchored app.
        /// </summary>
        private int anchorSessionId = -1;

        /// <summary>
        /// Monotonically increasing version for each retarget.
        /// </summary>
        private int targetVersion;

        // PDH handles (long-lived)
        private IntPtr hQuery = IntPtr.Zero;
        private IntPtr hPid = IntPtr.Zero;     // \Process(*)\ID Process
        private IntPtr hPpid = IntPtr.Zero;    // \Process(*)\Creating Process ID
        private IntPtr hCpu = IntPtr.Zero;     // \Process(*)\% Processor Time
        private IntPtr hPrivWs = IntPtr.Zero;  // \Process(*)\Working Set - Private
        private bool pdhReady;

        // Scratch buffer for PDH arrays
        private byte[] scratch = Array.Empty<byte>();

        // Sample counter (for periodic group refresh)
        private int sampleCounter;

        // Last computed stats (for TrySnapshotCurrent).
        private volatile ProcessResourceStats? lastStats;

        // Debug flag
        private volatile bool debugEnabled;

        #endregion

        #region Events

        /// <summary>
        /// Raised every time a new sample is computed.
        /// </summary>
        public event Action<ProcessResourceStats>? OnSample;

        #endregion

        #region Construction / Disposal

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessUsageSamplerByHwnd"/> class.
        /// </summary>
        public ProcessUsageSamplerByHwnd()
        {
        }

        /// <summary>
        /// Disposes the sampler, stopping background work and closing PDH handles.
        /// </summary>
        public void Dispose()
        {
            this.Stop();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Enables or disables Debug.WriteLine logging.
        /// </summary>
        /// <param name="enable">True to enable logging; otherwise false.</param>
        public void SetDebugLogging(bool enable)
        {
            this.debugEnabled = enable;
            if (enable) this.Dbg("Debug logging ENABLED");
        }

        /// <summary>
        /// Starts the background sampling loop (no-op if already started).
        /// </summary>
        public void Start()
        {
            lock (this.startStopGate)
            {
                if (this.cts is not null) return;

                var localCts = new CancellationTokenSource();
                this.cts = localCts;
                CancellationToken token = localCts.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(InitialDelayMs, token).ConfigureAwait(false);

                        // PDH setup once.
                        this.EnsurePdhQuery();

                        // Warmup for CPU deltas.
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
                            int familySession;
                            lock (this.targetGate)
                            {
                                hwnd = this.targetHwnd;
                                group = this.groupPids;
                                version = this.targetVersion;
                                family = this.anchorBaseName;
                                exeName = this.anchorExeName;
                                exeDir = this.anchorExeDir;
                                familySession = this.anchorSessionId;
                            }

                            if (hwnd == IntPtr.Zero || group.Count == 0)
                            {
                                await Task.Delay(SampleIntervalMs, token).ConfigureAwait(false);
                                continue;
                            }

                            PdhCollectQueryData(this.hQuery);

                            // Instance maps for this tick
                            var pidByInstance = this.ReadPidByInstance();
                            var cpuByInstance = this.ReadDoubleByInstance(this.hCpu);
                            var wsByInstance = this.ReadLongByInstance(this.hPrivWs);

                            // Periodic refresh: rebuild by family (name OR path) + session from PDH (fast).
                            this.sampleCounter++;
                            if ((this.sampleCounter % GroupRefreshEveryNSamples) == 0)
                            {
                                var refreshed = this.RebuildGroupFromPdh(pidByInstance, family, exeName, exeDir, familySession);
                                if (refreshed.Count > 0)
                                {
                                    lock (this.targetGate) this.groupPids = refreshed;
                                    this.Dbg($"[v{version}] PDH refresh => groupSize={refreshed.Count}");
                                }
                            }

                            // Aggregate per PID (only those in the current group)
                            var perPid = new Dictionary<int, (double cpu, long ws, int inst)>(group.Count);

                            foreach (var (inst, pid) in pidByInstance)
                            {
                                if (!group.Contains(pid)) continue;

                                double c = 0;
                                long w = 0;
                                if (cpuByInstance.TryGetValue(inst, out var cv)) c = cv;
                                if (wsByInstance.TryGetValue(inst, out var wv)) w = wv;

                                if (perPid.TryGetValue(pid, out var agg))
                                    perPid[pid] = (agg.cpu + c, agg.ws + w, agg.inst + 1);
                                else
                                    perPid[pid] = (c, w, 1);
                            }

                            double cpuSum = 0;
                            long wsSum = 0;
                            foreach (var v in perPid.Values) { cpuSum += v.cpu; wsSum += v.ws; }

                            int cores = Math.Max(1, Environment.ProcessorCount);
                            double cpuPct = Math.Min(100.0, cpuSum / cores);

                            var total = this.QueryTotalPhysicalMemory();
                            double ramPct = total > 0 ? Math.Min(100.0, (double)wsSum / total * 100.0) : 0;

                            var stats = new ProcessResourceStats
                            {
                                CpuPercent = cpuPct,
                                WorkingSetBytes = wsSum,
                                RamPercent = ramPct
                            };

                            this.lastStats = stats;
                            this.OnSample?.Invoke(stats);

                            if (this.debugEnabled)
                            {
                                this.Dbg($"[tick v{version}] group:{group.Count} pidItems:{pidByInstance.Count} cpuItems:{cpuByInstance.Count} wsItems:{wsByInstance.Count}");
                                foreach (var pid in perPid.Keys.OrderBy(p => p))
                                {
                                    var (cpu, ws, inst) = perPid[pid];
                                    string name = this.SafeProcName(pid);
                                    this.Dbg($"    PID {pid} ({name}) inst:{inst} cpuRaw:{cpu:F2} ws:{this.FormatBytes(ws)}");
                                }
                                this.Dbg($"    SUM cpuRaw:{cpuSum:F2} / cores:{cores} => cpu%:{cpuPct:F1}  WS:{this.FormatBytes(wsSum)}  RAM%:{ramPct:F2}");
                            }

                            await Task.Delay(SampleIntervalMs, token).ConfigureAwait(false);
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        this.Dbg($"Background loop error: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// Stops the background sampling loop and closes PDH counters.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource? toCancel = null;

            lock (this.startStopGate)
            {
                if (this.cts is null) return;
                toCancel = this.cts;
                this.cts = null;
            }

            try { toCancel?.Cancel(); } catch { }
            try { toCancel?.Dispose(); } catch { }

            if (this.hPid != IntPtr.Zero) { PdhRemoveCounter(this.hPid); this.hPid = IntPtr.Zero; }
            if (this.hPpid != IntPtr.Zero) { PdhRemoveCounter(this.hPpid); this.hPpid = IntPtr.Zero; }
            if (this.hCpu != IntPtr.Zero) { PdhRemoveCounter(this.hCpu); this.hCpu = IntPtr.Zero; }
            if (this.hPrivWs != IntPtr.Zero) { PdhRemoveCounter(this.hPrivWs); this.hPrivWs = IntPtr.Zero; }
            if (this.hQuery != IntPtr.Zero) { PdhCloseQuery(this.hQuery); this.hQuery = IntPtr.Zero; }
            this.pdhReady = false;

            lock (this.targetGate)
            {
                this.groupPids = new HashSet<int>();
                this.anchorBaseName = string.Empty;
                this.anchorExeName = string.Empty;
                this.anchorExeDir = string.Empty;
                this.anchorSessionId = -1;
                this.targetHwnd = IntPtr.Zero;
                this.targetVersion++;
            }

            this.lastStats = null;

            this.Dbg("Stopped and PDH released.");
        }

        /// <summary>
        /// Retargets the sampler to a new hwnd and optionally requests an immediate sample.
        /// </summary>
        /// <param name="hwnd">The new window handle to target.</param>
        /// <param name="immediate">If true, performs a quick synchronous PDH collect and publish.</param>
        public void SetTarget(IntPtr hwnd, bool immediate = true)
        {
            if (hwnd == IntPtr.Zero) return;

            // Determine owner/root pid of the hwnd for family discovery.
            var root = GetAncestor(hwnd, GA_ROOTOWNER);
            if (root == IntPtr.Zero) root = hwnd;
            _ = GetWindowThreadProcessId(root, out uint pidU);
            int rootPid = unchecked((int)pidU);

            string baseName = string.Empty;
            int sessionId = -1;
            string exePath = string.Empty;

            try
            {
                using var p = Process.GetProcessById(rootPid);
                baseName = p.ProcessName; // e.g., "opera"
                sessionId = p.SessionId;
            }
            catch { }

            // Try to get exe path with a robust API (works across bitness without admin).
            if (!this.TryGetProcessImagePath(rootPid, out exePath))
            {
                // Fallback to Process if available.
                try { using var p = Process.GetProcessById(rootPid); exePath = p.MainModule?.FileName ?? string.Empty; } catch { }
            }

            var exeNameLc = this.SafeFileNameLower(exePath);
            var exeDirLc = this.SafeDirectoryLowerWithSlash(exePath);

            // PDH snapshot → group by name OR path + session.
            try
            {
                this.EnsurePdhQuery();
                PdhCollectQueryData(this.hQuery);

                var pidByInstance = this.ReadPidByInstance();

                var groupFromPdh = this.BuildGroupFromPdh(pidByInstance, baseName, exeNameLc, exeDirLc, sessionId);

                // Augment with PDH parentage (optional): include any descendants of already-included PIDs.
                var ppidByInstance = this.ReadParentPidByInstance();
                var augmented = this.AugmentWithPdhDescendants(groupFromPdh, pidByInstance, ppidByInstance);

                lock (this.targetGate)
                {
                    this.targetHwnd = hwnd;
                    this.groupPids = augmented;
                    this.anchorBaseName = baseName;
                    this.anchorExeName = exeNameLc;
                    this.anchorExeDir = exeDirLc;
                    this.anchorSessionId = sessionId;
                    this.targetVersion++;
                }

                this.Dbg($"SetTarget hwnd=0x{hwnd.ToInt64():X} rootPid={rootPid} baseName='{baseName}' exeName='{exeNameLc}' exeDir='{exeDirLc}' session={sessionId} groupSize={augmented.Count} group=[{string.Join(",", augmented.OrderBy(p => p))}]");

                if (immediate && this.pdhReady)
                {
                    // Do a quick immediate aggregate using the same PDH snapshot we just took.
                    var cpuByInstance = this.ReadDoubleByInstance(this.hCpu);
                    var wsByInstance = this.ReadLongByInstance(this.hPrivWs);
                    this.ImmediatePulse(augmented, pidByInstance, cpuByInstance, wsByInstance);
                }
            }
            catch (Exception ex)
            {
                this.Dbg($"SetTarget error: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the last computed stats (if any) immediately.
        /// </summary>
        /// <returns>The latest stats or <c>null</c> if none yet.</returns>
        public ProcessResourceStats? TrySnapshotCurrent()
        {
            return this.lastStats;
        }

        #endregion

        #region Group building (name OR path + session)

        /// <summary>
        /// Rebuilds the group from PDH by matching either instance base name or image path (same exe name and directory) within the same session.
        /// </summary>
        private HashSet<int> RebuildGroupFromPdh(Dictionary<string, int> pidByInstance, string baseName, string exeNameLc, string exeDirLc, int sessionId)
        {
            return this.BuildGroupFromPdh(pidByInstance, baseName, exeNameLc, exeDirLc, sessionId);
        }

        /// <summary>
        /// Builds the group from a PDH snapshot by matching any of:
        ///  - base instance name equals <paramref name="baseName"/> (case-insensitive), or helper name of that family,
        ///  - image path has the same file name as <paramref name="exeNameLc"/> AND lives in <paramref name="exeDirLc"/>,
        /// and then filters down to the same <paramref name="sessionId"/>.
        /// </summary>
        private HashSet<int> BuildGroupFromPdh(Dictionary<string, int> pidByInstance, string baseName, string exeNameLc, string exeDirLc, int sessionId)
        {
            var set = new HashSet<int>();
            int added = 0;

            foreach (var (inst, pid) in pidByInstance)
            {
                // Session filter (fast)
                if (sessionId >= 0 && (!this.TryGetProcessSession(pid, out int pidSess) || pidSess != sessionId))
                    continue;

                bool include = false;
                string reason = string.Empty;

                // First pass: name family match.
                var instBase = this.GetBaseInstanceName(inst);
                if (!string.IsNullOrEmpty(baseName) &&
                    (instBase.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
                     this.IsHelperOfFamily(instBase, baseName)))
                {
                    include = true;
                    reason = $"name:{instBase}";
                }
                else
                {
                    // Second pass: path family match (same dir + same exe name).
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
                    if (this.debugEnabled) this.Dbg($"    + grouped PID {pid} ({this.SafeProcName(pid)}) via {reason}");
                }
            }

            if (this.debugEnabled)
            {
                this.Dbg($"BuildGroupByFamilyFromPdh base='{baseName}' exeName='{exeNameLc}' exeDir='{exeDirLc}' session={sessionId} => added:{added} size:{set.Count}");
            }

            return set;
        }

        /// <summary>
        /// Augments an existing set with all PDH descendants (Creating Process ID) of any PID already in the set.
        /// If PPID data is missing, returns the original set.
        /// </summary>
        private HashSet<int> AugmentWithPdhDescendants(HashSet<int> seed,
                                                       Dictionary<string, int> pidByInstance,
                                                       Dictionary<string, int> ppidByInstance)
        {
            if (seed.Count == 0 || ppidByInstance.Count == 0) return seed;

            // Build parent->children adjacency strictly from PDH
            var children = new Dictionary<int, List<int>>(256);
            foreach (var inst in pidByInstance.Keys)
            {
                int pid = pidByInstance[inst];
                if (!ppidByInstance.TryGetValue(inst, out int parent)) continue;

                if (!children.TryGetValue(parent, out var list)) children[parent] = list = new List<int>(2);
                list.Add(pid);
            }

            var set = new HashSet<int>(seed);
            var q = new Queue<int>(seed);

            while (q.Count > 0)
            {
                int p = q.Dequeue();
                if (!children.TryGetValue(p, out var kids)) continue;
                for (int i = 0; i < kids.Count; i++)
                {
                    int k = kids[i];
                    if (set.Add(k)) q.Enqueue(k);
                }
            }

            if (this.debugEnabled) this.Dbg($"AugmentWithPdhDescendants seed:{seed.Count} => final:{set.Count}");
            return set;
        }

        /// <summary>
        /// Returns the part of a PDH process instance name before any '#N' suffix (e.g., "opera#3" → "opera").
        /// </summary>
        private string GetBaseInstanceName(string instanceName)
        {
            int hash = instanceName.IndexOf('#');
            return hash >= 0 ? instanceName.Substring(0, hash) : instanceName;
        }

        /// <summary>
        /// Returns true if <paramref name="candidateBase"/> is a known helper for the given <paramref name="familyBase"/>.
        /// Used to include things like "opera_crashreporter" with "opera".
        /// </summary>
        private bool IsHelperOfFamily(string candidateBase, string familyBase)
        {
            if (candidateBase.Equals(familyBase, StringComparison.OrdinalIgnoreCase)) return true;

            // Heuristic: helpers often start with the family base, or are known names.
            if (candidateBase.StartsWith(familyBase, StringComparison.OrdinalIgnoreCase)) return true;

            if (familyBase.Equals("opera", StringComparison.OrdinalIgnoreCase))
            {
                return candidateBase.Equals("opera_crashreporter", StringComparison.OrdinalIgnoreCase) ||
                       candidateBase.Equals("opera_autoupdate", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Tries to get the session id for a PID.
        /// </summary>
        private bool TryGetProcessSession(int pid, out int sessionId)
        {
            try { using var p = Process.GetProcessById(pid); sessionId = p.SessionId; return true; }
            catch { sessionId = -1; return false; }
        }

        #endregion

        #region PDH setup / reads (aligned by instance name)

        /// <summary>
        /// Ensures the PDH query and counters are created once.
        /// </summary>
        private void EnsurePdhQuery()
        {
            if (this.pdhReady) return;

            this.Check(PdhOpenQuery(IntPtr.Zero, IntPtr.Zero, out this.hQuery));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\ID Process", IntPtr.Zero, out this.hPid));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Creating Process ID", IntPtr.Zero, out this.hPpid));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\% Processor Time", IntPtr.Zero, out this.hCpu));
            this.Check(PdhAddCounter(this.hQuery, @"\Process(*)\Working Set - Private", IntPtr.Zero, out this.hPrivWs));

            this.pdhReady = true;
            this.Dbg("PDH query initialized (ID Process, Creating Process ID, % Processor Time, Working Set - Private).");
        }

        /// <summary>
        /// Reads an instance→PID map from the ID Process counter for the current sample.
        /// </summary>
        private Dictionary<string, int> ReadPidByInstance()
        {
            var result = new Dictionary<string, int>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsLarge(this.hPid, PDH_FMT_LARGE, (name, value) =>
            {
                result[name] = unchecked((int)value);
            });
            return result;
        }

        /// <summary>
        /// Reads an instance→parent PID map from the Creating Process ID counter.
        /// </summary>
        private Dictionary<string, int> ReadParentPidByInstance()
        {
            var result = new Dictionary<string, int>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsLarge(this.hPpid, PDH_FMT_LARGE, (name, value) =>
            {
                result[name] = unchecked((int)value);
            });
            return result;
        }

        /// <summary>
        /// Reads an instance→double map for the given counter (e.g., % Processor Time).
        /// </summary>
        private Dictionary<string, double> ReadDoubleByInstance(IntPtr hCounter)
        {
            var result = new Dictionary<string, double>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsDouble(hCounter, PDH_FMT_DOUBLE, (name, value) => result[name] = value);
            return result;
        }

        /// <summary>
        /// Reads an instance→long map for the given counter (e.g., Working Set - Private).
        /// </summary>
        private Dictionary<string, long> ReadLongByInstance(IntPtr hCounter)
        {
            var result = new Dictionary<string, long>(256, StringComparer.Ordinal);
            this.ReadCounterArrayItemsLarge(hCounter, PDH_FMT_LARGE, (name, value) => result[name] = value);
            return result;
        }

        /// <summary>
        /// Reads formatted counter array items (DOUBLE version).
        /// </summary>
        private void ReadCounterArrayItemsDouble(IntPtr hCounter, uint fmt, Action<string, double> onItem)
        {
            uint bufBytes = 0, num = 0;
            int status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, IntPtr.Zero);
            if (status != PDH_MORE_DATA) return;

            this.EnsureScratch((int)bufBytes);
            IntPtr bufPinned = Marshal.UnsafeAddrOfPinnedArrayElement(this.scratch, 0);
            status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, bufPinned);
            if (status != 0 && status != 1) return;

            int itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM_DOUBLE>();
            IntPtr cur = bufPinned;
            for (int i = 0; i < num; i++)
            {
                var it = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM_DOUBLE>(cur);
                string? name = Marshal.PtrToStringUni(it.szName);
                if (!string.IsNullOrEmpty(name)) onItem(name, it.FmtValue.doubleValue);
                cur = IntPtr.Add(cur, itemSize);
            }
        }

        /// <summary>
        /// Reads formatted counter array items (LARGE version).
        /// </summary>
        private void ReadCounterArrayItemsLarge(IntPtr hCounter, uint fmt, Action<string, long> onItem)
        {
            uint bufBytes = 0, num = 0;
            int status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, IntPtr.Zero);
            if (status != PDH_MORE_DATA) return;

            this.EnsureScratch((int)bufBytes);
            IntPtr bufPinned = Marshal.UnsafeAddrOfPinnedArrayElement(this.scratch, 0);
            status = PdhGetFormattedCounterArray(hCounter, fmt, ref bufBytes, ref num, bufPinned);
            if (status != 0 && status != 1) return;

            int itemSize = Marshal.SizeOf<PDH_FMT_COUNTERVALUE_ITEM_LARGE>();
            IntPtr cur = bufPinned;
            for (int i = 0; i < num; i++)
            {
                var it = Marshal.PtrToStructure<PDH_FMT_COUNTERVALUE_ITEM_LARGE>(cur);
                string? name = Marshal.PtrToStringUni(it.szName);
                if (!string.IsNullOrEmpty(name)) onItem(name, it.FmtValue.largeValue);
                cur = IntPtr.Add(cur, itemSize);
            }
        }

        /// <summary>
        /// Ensures the scratch buffer is at least the requested size.
        /// </summary>
        private void EnsureScratch(int bytes)
        {
            if (this.scratch.Length < bytes)
            {
                Array.Resize(ref this.scratch, Math.Max(bytes, this.scratch.Length == 0 ? 4096 : this.scratch.Length * 2));
            }
        }

        #endregion

        #region Immediate pulse

        /// <summary>
        /// Publishes one immediate sample from already-read PDH maps.
        /// </summary>
        private void ImmediatePulse(
            HashSet<int> group,
            Dictionary<string, int> pidByInstance,
            Dictionary<string, double> cpuByInstance,
            Dictionary<string, long> wsByInstance)
        {
            try
            {
                var perPid = new Dictionary<int, (double cpu, long ws, int inst)>(group.Count);
                foreach (var (inst, pid) in pidByInstance)
                {
                    if (!group.Contains(pid)) continue;

                    double c = 0; if (cpuByInstance.TryGetValue(inst, out var cv)) c = cv;
                    long w = 0; if (wsByInstance.TryGetValue(inst, out var wv)) w = wv;

                    if (perPid.TryGetValue(pid, out var agg))
                        perPid[pid] = (agg.cpu + c, agg.ws + w, agg.inst + 1);
                    else
                        perPid[pid] = (c, w, 1);
                }

                double cpuSum = 0;
                long wsSum = 0;
                foreach (var v in perPid.Values) { cpuSum += v.cpu; wsSum += v.ws; }

                int cores = Math.Max(1, Environment.ProcessorCount);
                double cpuPct = Math.Min(100.0, cpuSum / cores);

                var total = this.QueryTotalPhysicalMemory();
                double ramPct = total > 0 ? Math.Min(100.0, (double)wsSum / total * 100.0) : 0;

                var stats = new ProcessResourceStats
                {
                    CpuPercent = cpuPct,
                    WorkingSetBytes = wsSum,
                    RamPercent = ramPct
                };

                this.lastStats = stats;
                this.OnSample?.Invoke(stats);

                if (this.debugEnabled)
                {
                    this.Dbg($"[immediate] pidItems:{pidByInstance.Count} cpuItems:{cpuByInstance.Count} wsItems:{wsByInstance.Count}");
                    foreach (var pid in perPid.Keys.OrderBy(p => p))
                    {
                        var (cpu, ws, inst) = perPid[pid];
                        string name = this.SafeProcName(pid);
                        this.Dbg($"    PID {pid} ({name}) inst:{inst} cpuRaw:{cpu:F2} ws:{this.FormatBytes(ws)}");
                    }
                    this.Dbg($"    SUM cpuRaw:{cpuSum:F2} / cores:{cores} => cpu%:{cpuPct:F1}  WS:{this.FormatBytes(wsSum)}  RAM%:{ramPct:F2}");
                }
            }
            catch (Exception ex)
            {
                this.Dbg($"Immediate pulse error: {ex.Message}");
            }
        }

        #endregion

        #region Interop + utilities

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
            public IntPtr szName; // LPWSTR
            public PDH_FMT_COUNTERVALUE_DOUBLE FmtValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_ITEM_LARGE
        {
            public IntPtr szName; // LPWSTR
            public PDH_FMT_COUNTERVALUE_LARGE FmtValue;
        }

        /// <summary>
        /// Throws on PDH failure (non-OK/non-NEW_DATA).
        /// </summary>
        private void Check(int status)
        {
            if (status != 0 && status != 1)
            {
                throw new InvalidOperationException("PDH error: 0x" + status.ToString("X8"));
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        /// <summary>
        /// Returns total physical memory in bytes, or 0 if unavailable.
        /// </summary>
        private ulong QueryTotalPhysicalMemory()
        {
            try
            {
                MEMORYSTATUSEX s = default;
                s.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
                if (GlobalMemoryStatusEx(ref s)) return s.ullTotalPhys;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Formats bytes as a short human-readable string.
        /// </summary>
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

        /// <summary>
        /// Writes a debug line if logging is enabled.
        /// </summary>
        private void Dbg(string message)
        {
            if (!this.debugEnabled) return;
            Debug.WriteLine($"[Sampler] {message}");
        }

        /// <summary>
        /// Safe process name for logs.
        /// </summary>
        private string SafeProcName(int pid)
        {
            try { using var p = Process.GetProcessById(pid); return p.ProcessName; }
            catch { return "?"; }
        }

        /// <summary>
        /// Returns the lower-cased file name part of a path, or empty if not available.
        /// </summary>
        private string SafeFileNameLower(string path)
        {
            try { if (string.IsNullOrEmpty(path)) return string.Empty; return System.IO.Path.GetFileName(path).ToLowerInvariant(); }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Returns the lower-cased directory part of a path, normalized with a trailing backslash, or empty if not available.
        /// </summary>
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
        /// Robustly retrieves the full process image path via Win32 (works across bitness without admin).
        /// </summary>
        private bool TryGetProcessImagePath(int pid, out string fullPath)
        {
            fullPath = string.Empty;
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
                if (h == IntPtr.Zero) return false;

                var sb = new StringBuilder(32768);
                int size = sb.Capacity;
                if (!QueryFullProcessImageName(h, 0, sb, ref size)) return false;

                fullPath = sb.ToString(0, size);
                return !string.IsNullOrEmpty(fullPath);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (h != IntPtr.Zero) try { CloseHandle(h); } catch { }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        #endregion
    }
}

