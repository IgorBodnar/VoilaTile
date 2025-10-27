namespace VoilaTile.Snapper.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using CommunityToolkit.Mvvm.ComponentModel;
    using VoilaTile.Common.Models;
    using VoilaTile.Snapper.Records;

    /// <summary>
    /// Per-monitor view model for Quick Grab overlay (text-only badges).
    /// Owns the ObservableCollection of hints; services merely feed data.
    /// </summary>
    public sealed partial class QuickGrabOverlayViewModel : ObservableObject, IOverlayViewModel
    {
        private static readonly StringComparison Comparison = StringComparison.OrdinalIgnoreCase;
        private readonly Action<QuickHintViewModel?> matchReportedCallback;

        public QuickGrabOverlayViewModel(MonitorInfo monitor, Action<QuickHintViewModel?> reportMatch)
        {
            this.matchReportedCallback = reportMatch;
            Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            Hints = new ObservableCollection<QuickHintViewModel>();
        }

        /// <summary>Monitor this overlay is bound to.</summary>
        public MonitorInfo Monitor { get; }

        /// <summary>All hint badges belonging to this monitor (bind to ItemsControl).</summary>
        public ObservableCollection<QuickHintViewModel> Hints { get; }

        [ObservableProperty]
        private string buffer = string.Empty;

        /// <summary>Hwnd of the unique match (if any) for confirm action (null when no unique match).</summary>
        [ObservableProperty]
        private WindowId? uniqueMatch;

        // ---------- IOverlayViewModel input hooks ----------
        public void AppendCharacter(char c)
        {
            this.Buffer = (this.Buffer ?? string.Empty) + char.ToUpper(c);
        }

        public void Backspace()
        {
            if (!string.IsNullOrEmpty(this.Buffer))
            {
                this.Buffer = this.Buffer[..^1];
            }
        }

        // ---------- Data feeding from coordinator ----------
        /// <summary>
        /// Replaces all hints with a new batch and re-applies the current filter.
        /// Call this from the coordinator after placements + hint assignment.
        /// </summary>
        public void SetBadges(IEnumerable<QuickGrabBadge> badges)
        {
            Hints.Clear();

            if (badges != null)
            {
                foreach (var b in badges.OrderBy(x => x.Z).ThenBy(x => x.Xdip).ThenBy(x => x.Ydip))
                {
                    Hints.Add(new QuickHintViewModel(b.Id, b.HintText.ToUpper(), b.Xdip, b.Ydip, b.Z));
                }
            }

            ApplyFilter();
        }

        // Auto-called when Buffer changes
        partial void OnBufferChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            // If there's no buffer, nothing is selected.
            if (string.IsNullOrEmpty(Buffer))
            {
                foreach (var h in Hints) h.IsSelected = false;
                this.matchReportedCallback?.Invoke(null);
                return;
            }

            QuickHintViewModel? best = null;

            // Tie-breaker: longer prefix match wins; if equal, shorter hint wins; then lower Z; then stable index.
            int index = 0, bestIndex = int.MaxValue;
            int bestPrefixLen = -1;
            int bestHintLen = int.MaxValue;
            int bestZ = int.MaxValue;

            foreach (var h in Hints)
            {
                // Only consider hints that start with the buffer (case-insensitive).
                if (!h.HintText.StartsWith(Buffer, StringComparison.OrdinalIgnoreCase))
                {
                    index++;
                    continue;
                }

                int prefixLen = Buffer.Length;
                int hintLen = h.HintText.Length;
                int z = h.Z;

                bool isBetter =
                    (prefixLen > bestPrefixLen) ||
                    (prefixLen == bestPrefixLen && hintLen < bestHintLen) ||
                    (prefixLen == bestPrefixLen && hintLen == bestHintLen && z < bestZ) ||
                    (prefixLen == bestPrefixLen && hintLen == bestHintLen && z == bestZ && index < bestIndex);

                if (isBetter)
                {
                    best = h;
                    bestPrefixLen = prefixLen;
                    bestHintLen = hintLen;
                    bestZ = z;
                    bestIndex = index;
                }

                index++;
            }

            // Clear previous selection, select only the winner.
            foreach (var h in Hints) h.IsSelected = false;
            if (best is not null)
            {
                best.IsSelected = true;
                this.matchReportedCallback?.Invoke(best);
            }
        }
    }
}

