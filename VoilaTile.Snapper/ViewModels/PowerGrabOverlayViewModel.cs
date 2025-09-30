namespace VoilaTile.Snapper.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services;

    /// <summary>
    /// View model for the Power Mode overlay (icons + titles only in Step 5).
    /// </summary>
    internal sealed class PowerGrabOverlayViewModel : ObservableObject
    {
        private readonly IHintService hints;
        private string hintBuffer = string.Empty;
        private string fullTitle = string.Empty;

        /// <summary>
        /// Tracks whether the preview overlay is currently visible.
        /// </summary>
        private bool isPreviewVisible;


        public PowerGrabOverlayViewModel(
            IHintService hints,
            IReadOnlyList<WindowEntry> windows)
        {
            this.hints = hints ?? throw new ArgumentNullException(nameof(hints));

            var labels = this.hints.AssignHints(windows.Count);
            this.Cards = new ObservableCollection<WindowCardViewModel>(
                windows.Select((w, i) => new WindowCardViewModel(w, labels[i])));

            // Select first card by default if exists.
            if (this.Cards.Count > 0)
            {
                this.Cards[0].IsSelected = true;
                this.FullTitle = this.Cards[0].Title;
            }
        }

        public event Action? OnShowPreview;
        public event Action? OnHidePreview;

        public ObservableCollection<WindowCardViewModel> Cards { get; }

        /// <summary>
        /// Gets the card currently targeted by the preview (the Selected card).
        /// </summary>
        public WindowCardViewModel? PreviewTarget => this.Selected;

        /// <summary>
        /// Gets a value indicating whether the preview overlay should be visible (held by Tab).
        /// </summary>
        public bool IsPreviewVisible
        {
            get => this.isPreviewVisible;
            private set
            {
                if (value == this.isPreviewVisible)
                {
                    return;
                }

                this.isPreviewVisible = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.PreviewTarget));
                this.OnPropertyChanged(nameof(this.FullTitle));
            }
        }

        public string FullTitle
        {
            get => this.fullTitle;
            private set
            {
                if (value == this.fullTitle) return;
                this.fullTitle = value;
                this.OnPropertyChanged();
            }
        }

        public string HintBuffer
        {
            get => this.hintBuffer;
            private set
            {
                if (value == this.hintBuffer) return;
                this.hintBuffer = value;
                this.OnPropertyChanged();
                this.ApplyFilter();
            }
        }

        public WindowCardViewModel? Selected =>
            this.Cards.FirstOrDefault(c => c.IsSelected);

        public void TypeChar(char c)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c)) return;
            this.HintBuffer += char.ToUpperInvariant(c);
            Debug.WriteLine($"[ViewModel] Typed char {c}");
        }

        public void Backspace()
        {
            if (this.HintBuffer.Length == 0) return;
            this.HintBuffer = this.HintBuffer.Substring(0, this.HintBuffer.Length - 1);
        }

        public void SelectNext()
        {
            if (this.Cards.Count == 0) return;
            var visible = this.Cards.Where(c => c.IsMatch).ToList();
            if (visible.Count == 0) return;

            int idx = Math.Max(0, visible.FindIndex(c => c.IsSelected));
            this.SetSelected(visible[(idx + 1) % visible.Count]);
        }

        public void SelectPrev()
        {
            if (this.Cards.Count == 0) return;
            var visible = this.Cards.Where(c => c.IsMatch).ToList();
            if (visible.Count == 0) return;

            int idx = Math.Max(0, visible.FindIndex(c => c.IsSelected));
            int next = (idx - 1 + visible.Count) % visible.Count;
            this.SetSelected(visible[next]);
        }

        public WindowEntry? CommitSelection()
        {
            // Exact match on hint buffer wins; otherwise the current Selected.
            if (!string.IsNullOrEmpty(this.HintBuffer))
            {
                var exact = this.Cards.FirstOrDefault(c => c.Hint.Equals(this.HintBuffer, StringComparison.OrdinalIgnoreCase));
                if (exact is not null)
                {
                    return exact.Entry;
                }
            }

            return this.Selected?.Entry;
        }

        private void ApplyFilter()
        {
            Debug.WriteLine($"[ApplyFilter] Buffer='{this.HintBuffer}'");

            if (string.IsNullOrEmpty(this.HintBuffer))
            {
                Debug.WriteLine("[ApplyFilter] Buffer is empty → all cards visible, keep current selection.");
                foreach (var c in this.Cards)
                {
                    c.IsMatch = true;
                    Debug.WriteLine($"    Card {c.Hint} → IsMatch=true");
                }
                return;
            }

            // 1) Update IsMatch for each card
            foreach (var c in this.Cards)
            {
                bool match = c.Hint.StartsWith(this.HintBuffer, StringComparison.OrdinalIgnoreCase);
                c.IsMatch = match;
                Debug.WriteLine($"    Card {c.Hint}: match={match}, IsSelected={c.IsSelected}");
            }

            // 2) Gather matches
            var matches = this.Cards.Where(c => c.IsMatch).ToList();
            Debug.WriteLine($"[ApplyFilter] Matches={matches.Count}");

            if (matches.Count == 0)
            {
                Debug.WriteLine("[ApplyFilter] No matches found → keep current selection.");
                return;
            }

            // 3) Look for exact match
            var exact = matches.FirstOrDefault(c =>
                c.Hint.Equals(this.HintBuffer, StringComparison.OrdinalIgnoreCase));

            if (exact is not null)
            {
                Debug.WriteLine($"[ApplyFilter] Exact match found → {exact.Hint}");
            }
            else
            {
                Debug.WriteLine("[ApplyFilter] No exact match → fall back to first match.");
            }

            var target = exact ?? matches[0];

            // 4) Compare against current selection
            if (this.Selected is not null)
            {
                Debug.WriteLine($"[ApplyFilter] Current selection={this.Selected.Hint}");
            }
            else
            {
                Debug.WriteLine("[ApplyFilter] Current selection=null");
            }

            if (!ReferenceEquals(target, this.Selected))
            {
                Debug.WriteLine($"[ApplyFilter] Changing selection → {target.Hint}");
                this.SetSelected(target);
            }
            else
            {
                Debug.WriteLine($"[ApplyFilter] Selection unchanged → still {target.Hint}");
            }
        }

        private void SetSelected(WindowCardViewModel card)
        {
            foreach (var c in this.Cards) c.IsSelected = false;
            card.IsSelected = true;
            this.FullTitle = card.Title;
            Debug.WriteLine($"[PowerMode ViewModel] Selected Card {card.Title}");
        }

        /// <summary>
        /// Shows the preview overlay (called when Tab is pressed).
        /// </summary>
        public void ShowPreview()
        {
            if (this.Cards.Count == 0)
            {
                this.IsPreviewVisible = false;
                return;
            }

            // Ensure we have a selected card (should already be true).
            if (this.Selected is null)
            {
                this.Cards[0].IsSelected = true;
                this.FullTitle = this.Cards[0].Title;
            }

            this.IsPreviewVisible = true;

            // Keep FullTitle in sync with preview target.
            if (this.Selected is not null)
            {
                this.FullTitle = this.Selected.Title;
            }

            this.OnShowPreview?.Invoke();
        }

        /// <summary>
        /// Hides the preview overlay (called when Tab is released).
        /// </summary>
        public void HidePreview()
        {
            this.IsPreviewVisible = false;
            this.OnHidePreview?.Invoke();
        }
    }
}
