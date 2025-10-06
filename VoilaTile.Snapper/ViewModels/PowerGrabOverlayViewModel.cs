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
    /// View model for the Power Mode overlay.
    /// </summary>
    internal sealed class PowerGrabOverlayViewModel : ObservableObject
    {
        #region Fields

        /// <summary>
        /// Provides keyboard hints for cards.
        /// </summary>
        private readonly IHintService hints;

        /// <summary>
        /// The current hint buffer built from user input.
        /// </summary>
        private string hintBuffer = string.Empty;

        /// <summary>
        /// The full title for the currently selected/previewed card.
        /// </summary>
        private string fullTitle = string.Empty;

        /// <summary>
        /// Indicates whether the preview overlay should be visible.
        /// </summary>
        private bool isPreviewVisible;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerGrabOverlayViewModel"/> class.
        /// </summary>
        /// <param name="hints">Service providing keyboard hints.</param>
        /// <param name="windows">The list of candidate windows.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="hints"/> is <c>null</c>.</exception>
        public PowerGrabOverlayViewModel(IHintService hints, IReadOnlyList<WindowEntry> windows)
        {
            this.hints = hints ?? throw new ArgumentNullException(nameof(hints));

            var labels = this.hints.AssignHints(windows.Count);
            this.Cards = new ObservableCollection<WindowCardViewModel>(
                windows.Select((w, i) => new WindowCardViewModel(w, labels[i])));

            if (this.Cards.Count > 0)
            {
                this.Cards[0].IsSelected = true;
                this.FullTitle = this.Cards[0].Title;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the UI should scroll the viewport by exactly one row.
        /// Positive delta scrolls down, negative scrolls up.
        /// </summary>
        public event Action<int>? OnScrollRows;

        /// <summary>
        /// Raised when the preview should be shown.
        /// </summary>
        public event Action? OnShowPreview;

        /// <summary>
        /// Raised when the preview should be hidden.
        /// </summary>
        public event Action? OnHidePreview;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the collection of window cards.
        /// </summary>
        public ObservableCollection<WindowCardViewModel> Cards { get; }

        /// <summary>
        /// Gets the card currently targeted by the preview (the selected card).
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
                if (value == this.isPreviewVisible) return;
                this.isPreviewVisible = value;
                this.OnPropertyChanged();
                this.OnPropertyChanged(nameof(this.PreviewTarget));
                this.OnPropertyChanged(nameof(this.FullTitle));
            }
        }

        /// <summary>
        /// Gets the full title of the currently selected/previewed card.
        /// </summary>
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

        /// <summary>
        /// Gets the current hint buffer (uppercased letters typed by the user).
        /// </summary>
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

        /// <summary>
        /// Gets the currently selected card, or <c>null</c> if none.
        /// </summary>
        public WindowCardViewModel? Selected => this.Cards.FirstOrDefault(c => c.IsSelected);

        #endregion

        #region Methods

        /// <summary>
        /// Handles a typed character:
        /// - '=' → scroll one row down (not added to buffer)
        /// - '-' → scroll one row up (not added to buffer)
        /// - otherwise → appended to hint buffer (uppercased)
        /// </summary>
        /// <param name="c">The input character.</param>
        public void TypeChar(char c)
        {
            if (char.IsControl(c)) return;

            if (c == '=')
            {
                this.OnScrollRows?.Invoke(+1);
                return;
            }

            if (c == '-')
            {
                this.OnScrollRows?.Invoke(-1);
                return;
            }

            if (char.IsWhiteSpace(c)) return;

            this.HintBuffer += char.ToUpperInvariant(c);
            Debug.WriteLine($"[ViewModel] Typed char {c}");
        }

        /// <summary>
        /// Removes the last character from the hint buffer, if any.
        /// </summary>
        public void Backspace()
        {
            if (this.HintBuffer.Length == 0) return;
            this.HintBuffer = this.HintBuffer.Substring(0, this.HintBuffer.Length - 1);
        }

        /// <summary>
        /// Selects the next visible (matching) card, cycling at the end.
        /// </summary>
        public void SelectNext()
        {
            if (this.Cards.Count == 0) return;
            var visible = this.Cards.Where(c => c.IsMatch).ToList();
            if (visible.Count == 0) return;

            int idx = Math.Max(0, visible.FindIndex(c => c.IsSelected));
            this.SetSelected(visible[(idx + 1) % visible.Count]);
        }

        /// <summary>
        /// Selects the previous visible (matching) card, cycling at the start.
        /// </summary>
        public void SelectPrev()
        {
            if (this.Cards.Count == 0) return;
            var visible = this.Cards.Where(c => c.IsMatch).ToList();
            if (visible.Count == 0) return;

            int idx = Math.Max(0, visible.FindIndex(c => c.IsSelected));
            int next = (idx - 1 + visible.Count) % visible.Count;
            this.SetSelected(visible[next]);
        }

        /// <summary>
        /// Commits the current selection based on the hint buffer or selected card.
        /// </summary>
        /// <returns>The selected <see cref="WindowEntry"/>, or <c>null</c> if none.</returns>
        public WindowEntry? CommitSelection()
        {
            if (!string.IsNullOrEmpty(this.HintBuffer))
            {
                var exact = this.Cards.FirstOrDefault(c => c.Hint.Equals(this.HintBuffer, StringComparison.OrdinalIgnoreCase));
                if (exact is not null) return exact.Entry;
            }
            return this.Selected?.Entry;
        }

        /// <summary>
        /// Applies the current hint filter to all cards and updates selection.
        /// </summary>
        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(this.HintBuffer))
            {
                foreach (var c in this.Cards) c.IsMatch = true;
                return;
            }

            foreach (var c in this.Cards)
            {
                c.IsMatch = c.Hint.StartsWith(this.HintBuffer, StringComparison.OrdinalIgnoreCase);
            }

            var matches = this.Cards.Where(c => c.IsMatch).ToList();
            if (matches.Count == 0) return;

            var exact = matches.FirstOrDefault(c => c.Hint.Equals(this.HintBuffer, StringComparison.OrdinalIgnoreCase));
            var target = exact ?? matches[0];

            if (!ReferenceEquals(target, this.Selected))
            {
                this.SetSelected(target);
            }
        }

        /// <summary>
        /// Sets the specified card as selected and updates the full title.
        /// </summary>
        /// <param name="card">The card to select.</param>
        private void SetSelected(WindowCardViewModel card)
        {
            foreach (var c in this.Cards) c.IsSelected = false;
            card.IsSelected = true;
            this.FullTitle = card.Title;
        }

        /// <summary>
        /// Shows the preview overlay (called when Tab is pressed).
        /// </summary>
        public void ShowPreview()
        {
            if (this.Cards.Count == 0) { this.IsPreviewVisible = false; return; }
            if (this.Selected is null)
            {
                this.Cards[0].IsSelected = true;
                this.FullTitle = this.Cards[0].Title;
            }
            this.IsPreviewVisible = true;
            if (this.Selected is not null) this.FullTitle = this.Selected.Title;
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

        #endregion
    }
}

