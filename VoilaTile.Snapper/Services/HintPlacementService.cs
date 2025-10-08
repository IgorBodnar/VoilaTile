// -------------------------------------------------------------------------------------
// <copyright file="HintPlacementService.cs">
//   Copyright © VoilaTile.
// </copyright>
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Text;
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.Interop;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services.Interfaces;

    /// <summary>
    /// Computes hint placements for currently visible windows. A window gets a hint if its visible
    /// area (portion on-screen and not occluded by higher-Z windows) is at least 10% of the full
    /// window area. The hint is placed at the center of the single largest visible piece. If that
    /// center is too close to monitor borders, it is nudged inside the chosen monitor's work area
    /// with a small margin, while staying as close as possible to the original center.
    /// </summary>
    internal sealed class HintPlacementService : IHintPlacementService
    {
        #region Fields

        /// <summary>
        /// The minimal visible-area ratio (10%) for a window to receive a hint.
        /// </summary>
        private const double VisibleRatioThreshold = 0.10;

        /// <summary>
        /// Hard cap on the number of occluder rectangles we keep per monitor to avoid explosive fragmentation.
        /// If the cap is exceeded, we coalesce to a bounding rectangle to keep performance predictable.
        /// </summary>
        private const int MaxOccluderPiecesPerMonitor = 64;

        /// <summary>
        /// Margin (in device pixels) to keep the final point away from monitor borders when nudging.
        /// </summary>
        private const int MonitorInsetMarginPx = 12;

        /// <summary>
        /// Indicates whether verbose tracing is enabled for diagnostic output.
        /// </summary>
        private readonly bool tracingEnabled;

        /// <summary>
        /// Delegate sink that receives trace lines when tracing is enabled.
        /// Defaults to <see cref="Debug.WriteLine(string?)"/>.
        /// </summary>
        private readonly Action<string> traceSink;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HintPlacementService"/> class with tracing disabled.
        /// </summary>
        public HintPlacementService()
            : this(false, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HintPlacementService"/> class.
        /// </summary>
        /// <param name="enableTracing">If set to <c>true</c>, verbose diagnostic tracing is enabled.</param>
        /// <param name="sink">
        /// Optional trace sink delegate. If <c>null</c>, <see cref="Debug.WriteLine(string?)"/> is used.
        /// The sink is invoked with newline-terminated lines.
        /// </param>
        public HintPlacementService(bool enableTracing, Action<string>? sink)
        {
            this.tracingEnabled = enableTracing;
            this.traceSink = sink ?? (s => Debug.WriteLine(s));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Computes hint placements for the provided window list given the specified monitors.
        /// </summary>
        /// <param name="windows">The collection of candidate windows to consider.</param>
        /// <param name="monitors">The available monitor set including work-area bounds and DPI.</param>
        /// <returns>A read-only list of computed hint placements.</returns>
        public IReadOnlyList<HintPlacement> ComputePlacements(
            IReadOnlyList<WindowEntry> windows,
            IReadOnlyList<MonitorInfo> monitors)
        {
            if (windows is null || monitors is null || windows.Count == 0 || monitors.Count == 0)
            {
                return Array.Empty<HintPlacement>();
            }

            var sessionId = Environment.TickCount;

            // Precompute monitor work-area rectangles (device pixels).
            var monitorRects = new RectPx[monitors.Count];
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                monitorRects[i] = new RectPx(m.WorkX, m.WorkY, m.WorkWidth, m.WorkHeight);
            }

            var zOrder = BuildZTopToBottom(windows);

            // Per-monitor occluder lists.
            var occludersPerMonitor = new List<RectPx>[monitors.Count];
            for (int i = 0; i < occludersPerMonitor.Length; i++)
            {
                occludersPerMonitor[i] = new List<RectPx>(16);
            }

            var result = new List<HintPlacement>(windows.Count);

            this.TraceHeader(sessionId, monitors, monitorRects, zOrder);

            // Process top -> bottom.
            for (int zi = 0; zi < zOrder.Count; zi++)
            {
                var w = zOrder[zi];
                if (!w.IsVisible || w.IsCloaked)
                {
                    this.TraceLine($"[#{sessionId}] Z={zi} HWND=0x{w.Id.Hwnd.ToInt64():X} SKIP (IsVisible={w.IsVisible}, IsCloaked={w.IsCloaked})");
                    continue;
                }

                var full = ToRectPx(w.BoundsPx.X, w.BoundsPx.Y, w.BoundsPx.W, w.BoundsPx.H);
                if (full.W <= 0 || full.H <= 0)
                {
                    this.TraceLine($"[#{sessionId}] Z={zi} HWND=0x{w.Id.Hwnd.ToInt64():X} SKIP (empty bounds {Fmt(full)})");
                    continue;
                }

                this.TraceLine($"[#{sessionId}] Z={zi} HWND=0x{w.Id.Hwnd.ToInt64():X} PROC Title='{Safe(w.Title)}' Proc='{Safe(w.ProcessName)}' Bounds={Fmt(full)}");

                // Compute visible pieces per monitor and total visible area.
                double totalVisibleArea = 0;
                var visiblePiecesPerMonitor = new List<RectPx>[monitors.Count];

                for (int mi = 0; mi < monitors.Count; mi++)
                {
                    var work = monitorRects[mi];
                    var clip = Intersect(full, work);
                    if (clip is null)
                    {
                        continue;
                    }

                    if (this.tracingEnabled)
                    {
                        this.TraceOccluderSummary(sessionId, zi, mi, occludersPerMonitor[mi]);
                    }

                    var pieces = this.SubtractManySingle_Tracing(clip.Value, occludersPerMonitor[mi], sessionId, zi, mi);
                    if (pieces.Count == 0)
                    {
                        this.TraceLine($"[#{sessionId}]   Z={zi} MON={mi} VisiblePieces: 0");
                        continue;
                    }

                    visiblePiecesPerMonitor[mi] = pieces;

                    long areaSum = 0;
                    for (int p = 0; p < pieces.Count; p++)
                    {
                        var r = pieces[p];
                        long a = (long)r.W * r.H;
                        if (a <= 0)
                        {
                            continue;
                        }

                        areaSum += a;
                        totalVisibleArea += a;
                    }

                    if (this.tracingEnabled)
                    {
                        var sb = new StringBuilder();
                        sb.Append($"[#{sessionId}]   Z={zi} MON={mi} VisiblePieces: {pieces.Count} Area={areaSum}");
                        for (int p = 0; p < pieces.Count; p++)
                        {
                            sb.Append(" | ").Append(Fmt(pieces[p]));
                        }

                        this.TraceLine(sb.ToString());
                    }
                }

                long fullArea = (long)full.W * full.H;
                double ratio = totalVisibleArea / Math.Max(1.0, fullArea);
                this.TraceLine($"[#{sessionId}] Z={zi} VisibleTotal={totalVisibleArea} / Full={fullArea} Ratio={(ratio * 100.0):F2}%");

                if (totalVisibleArea <= 0 || ratio < VisibleRatioThreshold)
                {
                    this.TraceLine($"[#{sessionId}] Z={zi} -> NO HINT (below {VisibleRatioThreshold:P0} threshold). Add as occluder.");
                    this.AddWindowToOccluders_Tracing(full, monitors, monitorRects, occludersPerMonitor, sessionId, zi);
                    continue;
                }

                // Choose the single largest visible piece (across all monitors).
                int bestMonitor = -1;
                int bestPieceIndex = -1;
                long bestPieceArea = -1;
                RectPx bestPiece = default;

                for (int mi = 0; mi < monitors.Count; mi++)
                {
                    var pieces = visiblePiecesPerMonitor[mi];
                    if (pieces is null)
                    {
                        continue;
                    }

                    for (int p = 0; p < pieces.Count; p++)
                    {
                        var r = pieces[p];
                        long a = (long)r.W * r.H;
                        if (a > bestPieceArea)
                        {
                            bestPieceArea = a;
                            bestPiece = r;
                            bestMonitor = mi;
                            bestPieceIndex = p;
                        }
                    }
                }

                if (bestMonitor < 0 || bestPieceArea <= 0)
                {
                    // Defensive: if for some reason no piece was found, skip placing.
                    this.TraceLine($"[#{sessionId}] Z={zi} -> NO HINT (no largest piece). Add as occluder.");
                    this.AddWindowToOccluders_Tracing(full, monitors, monitorRects, occludersPerMonitor, sessionId, zi);
                    continue;
                }

                // Center of the largest visible piece.
                var center = new PointPx(
                    bestPiece.X + (bestPiece.W / 2),
                    bestPiece.Y + (bestPiece.H / 2));

                // Nudge/clamp the point inside the chosen monitor's work area with a small inset margin.
                var finalPoint = ClampToInset(center, monitorRects[bestMonitor], MonitorInsetMarginPx);

                this.TraceLine($"[#{sessionId}] Z={zi} LargestPiece MON={bestMonitor} idx={bestPieceIndex} Rect={Fmt(bestPiece)} " +
                               $"Center=({center.X},{center.Y}) Final(Nudged)=({finalPoint.X},{finalPoint.Y}) DevID='{monitors[bestMonitor].DeviceID}'");

                result.Add(new HintPlacement(w.Id, monitors[bestMonitor].DeviceID, finalPoint.X, finalPoint.Y, zi));

                // Add current window to occluders (so it hides those below).
                this.AddWindowToOccluders_Tracing(full, monitors, monitorRects, occludersPerMonitor, sessionId, zi);
            }

            this.TraceLine($"[#{sessionId}] DONE. Placements={result.Count}");
            return result;
        }

        // ======================= Tracing wrappers & helpers =======================

        /// <summary>
        /// Writes initial session header including monitors and computed z-order if tracing is enabled.
        /// </summary>
        /// <param name="sessionId">Numeric identifier for the current computation run.</param>
        /// <param name="monitors">Monitor collection.</param>
        /// <param name="mrects">Precomputed monitor work rectangles (device pixels).</param>
        /// <param name="z">Windows ordered from top to bottom.</param>
        private void TraceHeader(int sessionId, IReadOnlyList<MonitorInfo> monitors, RectPx[] mrects, List<WindowEntry> z)
        {
            if (!this.tracingEnabled)
            {
                return;
            }

            this.TraceLine($"[#{sessionId}] === HintPlacement BEGIN ===");
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                this.TraceLine($"[#{sessionId}] MON={i} DevID='{m.DeviceID}' Work={Fmt(mrects[i])} DPI=({m.DpiX:F2},{m.DpiY:F2})");
            }

            for (int i = 0; i < z.Count; i++)
            {
                var w = z[i];
                var r = new RectPx(w.BoundsPx.X, w.BoundsPx.Y, w.BoundsPx.W, w.BoundsPx.H);
                this.TraceLine($"[#{sessionId}] Z={i} HWND=0x{w.Id.Hwnd.ToInt64():X} Title='{Safe(w.Title)}' Proc='{Safe(w.ProcessName)}' Bounds={Fmt(r)} Vis={w.IsVisible} Cloaked={w.IsCloaked}");
            }
        }

        /// <summary>
        /// Outputs a brief summary of the current occluder set on a monitor for the given z-index.
        /// </summary>
        /// <param name="sessionId">Current session identifier.</param>
        /// <param name="zIndex">Index of the window in z-order (top=0).</param>
        /// <param name="monIndex">Monitor index.</param>
        /// <param name="occ">Occluder list for that monitor.</param>
        private void TraceOccluderSummary(int sessionId, int zIndex, int monIndex, List<RectPx> occ)
        {
            if (!this.tracingEnabled)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append($"[#{sessionId}]   Z={zIndex} MON={monIndex} Occluders: {occ.Count}");
            int show = Math.Min(occ.Count, 8);
            for (int i = 0; i < show; i++)
            {
                sb.Append(" | ").Append(Fmt(occ[i]));
            }

            if (occ.Count > show)
            {
                sb.Append(" | ...");
            }

            this.TraceLine(sb.ToString());
        }

        /// <summary>
        /// Adds a window's rectangle as an occluder to all monitors where it intersects the work area.
        /// Emits tracing for each insertion and applies the fragmentation cap with bounding box collapse.
        /// </summary>
        /// <param name="windowRect">The window rectangle in device pixels.</param>
        /// <param name="monitors">Monitor collection.</param>
        /// <param name="monitorRects">Precomputed monitor work rectangles.</param>
        /// <param name="occludersPerMonitor">Mutable per-monitor occluder lists.</param>
        /// <param name="sessionId">Current session identifier.</param>
        /// <param name="zIndex">Current z-index of the processed window.</param>
        private void AddWindowToOccluders_Tracing(
            RectPx windowRect,
            IReadOnlyList<MonitorInfo> monitors,
            RectPx[] monitorRects,
            List<RectPx>[] occludersPerMonitor,
            int sessionId,
            int zIndex)
        {
            for (int mi = 0; mi < monitors.Count; mi++)
            {
                var clip = Intersect(windowRect, monitorRects[mi]);
                if (clip is null)
                {
                    continue;
                }

                var list = occludersPerMonitor[mi];
                int before = list.Count;

                list.Add(clip.Value);

                if (list.Count > MaxOccluderPiecesPerMonitor)
                {
                    var bb = BoundingBox(list);
                    list.Clear();
                    if (bb.W > 0 && bb.H > 0)
                    {
                        list.Add(bb);
                    }

                    this.TraceLine($"[#{sessionId}]   Z={zIndex} MON={mi} Occluder+ (clip {Fmt(clip.Value)}), CAP HIT -> collapse to BB {Fmt(bb)} (was {before}+1 parts)");
                }
                else
                {
                    this.TraceLine($"[#{sessionId}]   Z={zIndex} MON={mi} Occluder+ (clip {Fmt(clip.Value)}), parts: {before} -> {list.Count}");
                }
            }
        }

        /// <summary>
        /// Performs traced subtraction of a set of cutter rectangles from a source rectangle, producing visible pieces.
        /// Writes detailed steps to the trace sink when tracing is enabled.
        /// </summary>
        /// <param name="source">The source rectangle to subtract from (typically window ∩ monitor work area).</param>
        /// <param name="cutters">The list of occluding rectangles to subtract.</param>
        /// <param name="sessionId">Current session identifier.</param>
        /// <param name="zIndex">Current z-index of the processed window.</param>
        /// <param name="monIndex">Monitor index.</param>
        /// <returns>A new list of visible rectangles after subtraction.</returns>
        private List<RectPx> SubtractManySingle_Tracing(RectPx source, List<RectPx> cutters, int sessionId, int zIndex, int monIndex)
        {
            if (!this.tracingEnabled)
            {
                return SubtractManySingle(source, cutters);
            }

            this.TraceLine($"[#{sessionId}]   Z={zIndex} MON={monIndex} SUBTRACT START Source={Fmt(source)} Cutters={cutters.Count}");

            var current = TempListPool.Rent();
            current.Add(source);

            for (int i = 0; i < cutters.Count; i++)
            {
                var c = cutters[i];
                if (current.Count == 0)
                {
                    break;
                }

                var next = TempListPool.Rent();

                for (int j = 0; j < current.Count; j++)
                {
                    var s = current[j];
                    var inter = Intersect(s, c);
                    if (inter is null)
                    {
                        next.Add(s);
                        continue;
                    }

                    var parts = Subtract(s, c);

                    if (parts.Count != 0)
                    {
                        for (int k = 0; k < parts.Count; k++)
                        {
                            var p = parts[k];
                            if (p.W > 0 && p.H > 0)
                            {
                                next.Add(p);
                            }
                        }

                        this.TraceLine($"[#{sessionId}]     cutter[{i}] {Fmt(c)} hit {Fmt(s)} -> parts {parts.Count}");
                    }
                    else
                    {
                        this.TraceLine($"[#{sessionId}]     cutter[{i}] {Fmt(c)} fully covered {Fmt(s)} -> removed");
                    }
                }

                TempListPool.Return(current);
                current = next;

                if (current.Count > MaxOccluderPiecesPerMonitor)
                {
                    var bb = BoundingBox(current);
                    current.Clear();
                    if (bb.W > 0 && bb.H > 0)
                    {
                        current.Add(bb);
                    }

                    this.TraceLine($"[#{sessionId}]     piece CAP -> collapse to BB {Fmt(bb)}");
                    break;
                }
            }

            var result = new List<RectPx>(current);
            if (result.Count == 0)
            {
                this.TraceLine($"[#{sessionId}]   Z={zIndex} MON={monIndex} SUBTRACT END -> 0 pieces");
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append($"[#{sessionId}]   Z={zIndex} MON={monIndex} SUBTRACT END -> pieces={result.Count}");
                for (int i = 0; i < result.Count && i < 12; i++)
                {
                    sb.Append(" | ").Append(Fmt(result[i]));
                }

                if (result.Count > 12)
                {
                    sb.Append(" | ...");
                }

                this.TraceLine(sb.ToString());
            }

            TempListPool.Return(current);
            return result;
        }

        /// <summary>
        /// Emits a single trace line using the configured sink, if tracing is enabled.
        /// </summary>
        /// <param name="line">The line to write; should not be <c>null</c>.</param>
        private void TraceLine(string line)
        {
            if (!this.tracingEnabled)
            {
                return;
            }

            this.traceSink(line);
        }

        /// <summary>
        /// Formats a rectangle as a concise string representation: [x,y,w×h].
        /// </summary>
        /// <param name="r">The rectangle to format.</param>
        /// <returns>The formatted string.</returns>
        private static string Fmt(RectPx r) => $"[{r.X},{r.Y},{r.W}×{r.H}]";

        /// <summary>
        /// Returns a sanitized version of a string for trace output (linebreaks escaped).
        /// </summary>
        /// <param name="s">The input string or <c>null</c>.</param>
        /// <returns>A safe one-line string.</returns>
        private static string Safe(string? s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\r", "\\r").Replace("\n", "\\n");

        // ======================= Core helpers =======================

        /// <summary>
        /// Builds and returns windows ordered top-to-bottom according to the desktop Z-order.
        /// </summary>
        /// <param name="windows">The windows to arrange into z-order.</param>
        /// <returns>A list of windows with index 0 being the topmost.</returns>
        private static List<WindowEntry> BuildZTopToBottom(IReadOnlyList<WindowEntry> windows)
        {
            var map = new Dictionary<IntPtr, WindowEntry>(windows.Count);
            for (int i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                map[w.Id.Hwnd] = w;
            }

            var ordered = new List<WindowEntry>(windows.Count);
            var cur = Win32.GetTopWindow(IntPtr.Zero);
            if (cur == IntPtr.Zero)
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    ordered.Add(windows[i]);
                }

                return ordered;
            }

            int guard = 20000;
            for (var h = cur; h != IntPtr.Zero && guard-- > 0; h = Win32.GetWindow(h, Win32.GW_HWNDNEXT))
            {
                if (map.TryGetValue(h, out var entry))
                {
                    ordered.Add(entry);
                }
            }

            if (ordered.Count != windows.Count)
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    var w = windows[i];
                    if (!map.TryGetValue(w.Id.Hwnd, out _))
                    {
                        ordered.Add(w);
                    }
                }
            }

            return ordered;
        }

        // ------------------------ Geometry helpers (device px) ------------------------

        /// <summary>
        /// Immutable axis-aligned rectangle in device pixels.
        /// </summary>
        /// <param name="X">The left coordinate.</param>
        /// <param name="Y">The top coordinate.</param>
        /// <param name="W">The width in pixels.</param>
        /// <param name="H">The height in pixels.</param>
        private readonly record struct RectPx(int X, int Y, int W, int H);

        /// <summary>
        /// Immutable point in device pixels.
        /// </summary>
        /// <param name="X">The X coordinate.</param>
        /// <param name="Y">The Y coordinate.</param>
        private readonly record struct PointPx(int X, int Y);

        /// <summary>
        /// Creates a <see cref="RectPx"/> from explicit components.
        /// </summary>
        /// <param name="x">Left coordinate.</param>
        /// <param name="y">Top coordinate.</param>
        /// <param name="w">Width in pixels.</param>
        /// <param name="h">Height in pixels.</param>
        /// <returns>The constructed rectangle.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RectPx ToRectPx(int x, int y, int w, int h) => new RectPx(x, y, w, h);

        /// <summary>
        /// Computes the intersection of two rectangles.
        /// </summary>
        /// <param name="a">First rectangle.</param>
        /// <param name="b">Second rectangle.</param>
        /// <returns>
        /// The intersection rectangle if non-empty; otherwise <c>null</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RectPx? Intersect(in RectPx a, in RectPx b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.W, b.X + b.W);
            int y2 = Math.Min(a.Y + a.H, b.Y + b.H);
            if (x2 <= x1 || y2 <= y1)
            {
                return null;
            }

            return new RectPx(x1, y1, x2 - x1, y2 - y1);
        }

        /// <summary>
        /// Produces a bounding box that encloses all rectangles in the list.
        /// </summary>
        /// <param name="rects">Rectangles to enclose.</param>
        /// <returns>The minimal axis-aligned bounding rectangle, or default if invalid.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static RectPx BoundingBox(List<RectPx> rects)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < rects.Count; i++)
            {
                var r = rects[i];
                if (r.X < minX) minX = r.X;
                if (r.Y < minY) minY = r.Y;
                int rx2 = r.X + r.W;
                int ry2 = r.Y + r.H;
                if (rx2 > maxX) maxX = rx2;
                if (ry2 > maxY) maxY = ry2;
            }

            if (minX >= maxX || minY >= maxY)
            {
                return default;
            }

            return new RectPx(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// Clamps a point into a monitor work rectangle inset by the provided margin.
        /// </summary>
        /// <param name="p">Original point.</param>
        /// <param name="monitorWork">Monitor work-area rectangle.</param>
        /// <param name="margin">Inset margin in pixels.</param>
        /// <returns>The clamped/nudged point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PointPx ClampToInset(in PointPx p, in RectPx monitorWork, int margin)
        {
            int minX = monitorWork.X + margin;
            int minY = monitorWork.Y + margin;
            int maxX = monitorWork.X + Math.Max(0, monitorWork.W - 1 - margin);
            int maxY = monitorWork.Y + Math.Max(0, monitorWork.H - 1 - margin);

            int x = p.X < minX ? minX : (p.X > maxX ? maxX : p.X);
            int y = p.Y < minY ? minY : (p.Y > maxY ? maxY : p.Y);
            return new PointPx(x, y);
        }

        /// <summary>
        /// Subtracts a collection of cutter rectangles from a single source rectangle, yielding the visible pieces.
        /// </summary>
        /// <param name="source">The source rectangle to subtract from.</param>
        /// <param name="cutters">The cutter rectangles to subtract.</param>
        /// <returns>A new list containing the visible pieces.</returns>
        private static List<RectPx> SubtractManySingle(RectPx source, List<RectPx> cutters)
        {
            var current = TempListPool.Rent();
            current.Add(source);

            for (int i = 0; i < cutters.Count; i++)
            {
                var c = cutters[i];
                if (current.Count == 0)
                {
                    break;
                }

                var next = TempListPool.Rent();
                for (int j = 0; j < current.Count; j++)
                {
                    var s = current[j];

                    var inter = Intersect(s, c);
                    if (inter is null)
                    {
                        next.Add(s);
                        continue;
                    }

                    var parts = Subtract(s, c);

                    if (parts.Count != 0)
                    {
                        for (int k = 0; k < parts.Count; k++)
                        {
                            var p = parts[k];
                            if (p.W > 0 && p.H > 0)
                            {
                                next.Add(p);
                            }
                        }
                    }
                    else
                    {
                        // fully covered -> drop
                    }
                }

                TempListPool.Return(current);
                current = next;

                if (current.Count > MaxOccluderPiecesPerMonitor)
                {
                    var bb = BoundingBox(current);
                    current.Clear();
                    if (bb.W > 0 && bb.H > 0)
                    {
                        current.Add(bb);
                    }

                    break;
                }
            }

            var result = new List<RectPx>(current);
            TempListPool.Return(current);
            return result;
        }

        /// <summary>
        /// Subtracts one rectangle from another, possibly producing up to four disjoint rectangles.
        /// </summary>
        /// <param name="a">The rectangle to subtract from.</param>
        /// <param name="b">The rectangle to subtract.</param>
        /// <returns>New list of rectangles representing <paramref name="a"/> \ <paramref name="b"/>.</returns>
        private static List<RectPx> Subtract(RectPx a, RectPx b)
        {
            var r = Intersect(a, b);
            if (r is null)
            {
                return TempListPool.Empty;
            }

            var i = r.Value;
            var pieces = TempListPool.Rent();

            // above
            if (i.Y > a.Y)
            {
                pieces.Add(new RectPx(a.X, a.Y, a.W, i.Y - a.Y));
            }

            // below
            int aB = a.Y + a.H, iB = i.Y + i.H;
            if (iB < aB)
            {
                pieces.Add(new RectPx(a.X, iB, a.W, aB - iB));
            }

            // left band
            if (i.X > a.X)
            {
                pieces.Add(new RectPx(a.X, i.Y, i.X - a.X, i.H));
            }

            // right band
            int aR = a.X + a.W, iR = i.X + i.W;
            if (iR < aR)
            {
                pieces.Add(new RectPx(iR, i.Y, aR - iR, i.H));
            }

            return pieces;
        }

        #endregion

        #region Local: TempListPool

        /// <summary>
        /// Small single-threaded list pool for <see cref="RectPx"/> to reduce GC pressure in hot paths.
        /// </summary>
        private static class TempListPool
        {
            /// <summary>
            /// An immutable empty list instance used for fast returns from subtraction when no pieces exist.
            /// </summary>
            public static readonly List<RectPx> Empty = new(0);

            /// <summary>
            /// Thread-local stack of reusable <see cref="List{RectPx}"/> instances.
            /// </summary>
            [ThreadStatic]
            private static Stack<List<RectPx>>? pool;

            /// <summary>
            /// Rents a list from the pool or creates a new list if none are available.
            /// </summary>
            /// <returns>A cleared <see cref="List{RectPx}"/> ready for use.</returns>
            public static List<RectPx> Rent()
            {
                pool ??= new Stack<List<RectPx>>(16);
                return pool.Count > 0 ? pool.Pop() : new List<RectPx>(8);
            }

            /// <summary>
            /// Returns a list to the pool after clearing its contents.
            /// </summary>
            /// <param name="list">The list to return.</param>
            public static void Return(List<RectPx> list)
            {
                list.Clear();
                pool ??= new Stack<List<RectPx>>(16);
                pool.Push(list);
            }
        }

        #endregion
    }
}

