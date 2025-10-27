// -------------------------------------------------------------------------------------
// <copyright file="PowerGrabOverlayViewModel.cs">
//   Copyright © VoilaTile.
// </copyright>
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services;

    /// <summary>
    /// View model for the Power Mode overlay.
    /// </summary>
    internal sealed class PowerGrabOverlayViewModel : ObservableObject, IDisposable
    {
        #region Fields

        /// <summary>
        /// Provides keyboard hints for cards.
        /// </summary>
        private readonly IHintService hints;

        /// <summary>
        /// Dispatcher captured from UI thread.
        /// </summary>
        private readonly Dispatcher dispatcher = Application.Current.Dispatcher;

        /// <summary>
        /// Single long-lived process usage sampler.
        /// </summary>
        private readonly ProcessUsageSamplerByHwnd sampler;

        /// <summary>
        /// Latest sampled stats.
        /// </summary>
        private ProcessResourceStats? previewStats;

        /// <summary>
        /// Current user-typed hint buffer.
        /// </summary>
        private string hintBuffer = string.Empty;

        /// <summary>
        /// Title of currently selected card.
        /// </summary>
        private string fullTitle = string.Empty;

        /// <summary>
        /// Whether preview overlay is visible.
        /// </summary>
        private bool isPreviewVisible;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerGrabOverlayViewModel"/> class.
        /// </summary>
        /// <param name="hints">The hint generation service.</param>
        /// <param name="windows">The list of candidate windows.</param>
        public PowerGrabOverlayViewModel(IHintService hints, IReadOnlyList<WindowEntry> windows)
        {
            this.hints = hints ?? throw new ArgumentNullException(nameof(hints));

            var labels = this.hints.AssignHints(windows.Count);
            this.Cards = new ObservableCollection<WindowCardViewModel>(
                windows.Select((w, i) => new WindowCardViewModel(w, labels[i])));

            this.sampler = new ProcessUsageSamplerByHwnd();
            this.sampler.SetDebugLogging(false);
            this.sampler.OnSample += this.OnSamplerSampleReceived;
            this.sampler.Start();

            if (this.Cards.Count > 0)
            {
                this.Cards[0].IsSelected = true;
                this.FullTitle = this.Cards[0].Title;

                this.SetSamplerTargetForSelected();
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
        /// Gets a value indicating whether the preview overlay should be visible.
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

        /// <summary>
        /// Gets live CPU/RAM stats for the preview target.
        /// </summary>
        public ProcessResourceStats? PreviewStats
        {
            get => this.previewStats;
            private set
            {
                if (ReferenceEquals(value, this.previewStats))
                {
                    return;
                }

                this.previewStats = value;
                this.OnPropertyChanged();
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
                if (value == this.fullTitle)
                {
                    return;
                }

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
                if (value == this.hintBuffer)
                {
                    return;
                }

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
        /// Disposes of the view model by unsubscribing from the events.
        /// </summary>
        public void Dispose()
        {
            this.sampler.OnSample -= this.OnSamplerSampleReceived;
            this.sampler.Dispose();
        }

        /// <summary>
        /// Handles typed characters: '=' scrolls down, '-' scrolls up, others extend buffer.
        /// </summary>
        /// <param name="c">The input character.</param>
        public void TypeChar(char c)
        {
            if (char.IsControl(c))
            {
                return;
            }

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

            if (char.IsWhiteSpace(c))
            {
                return;
            }

            this.HintBuffer += char.ToUpperInvariant(c);
            Debug.WriteLine($"[ViewModel] Typed char {c}");
        }

        /// <summary>
        /// Removes the last character from the hint buffer, if any.
        /// </summary>
        public void Backspace()
        {
            if (this.HintBuffer.Length == 0)
            {
                return;
            }

            this.HintBuffer = this.HintBuffer.Substring(0, this.HintBuffer.Length - 1);
        }

        /// <summary>
        /// Selects the next visible card.
        /// </summary>
        public void SelectNext()
        {
            if (this.Cards.Count == 0)
            {
                return;
            }

            var visible = this.Cards.Where(c => c.IsMatch).ToList();
            if (visible.Count == 0)
            {
                return;
            }

            int idx = Math.Max(0, visible.FindIndex(c => c.IsSelected));
            this.SetSelected(visible[(idx + 1) % visible.Count]);
        }

        /// <summary>
        /// Selects the previous visible card.
        /// </summary>
        public void SelectPrev()
        {
            if (this.Cards.Count == 0)
            {
                return;
            }

            var visible = this.Cards.Where(c => c.IsMatch).ToList();
            if (visible.Count == 0)
            {
                return;
            }

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
                if (exact is not null)
                {
                    return exact.Entry;
                }
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
                foreach (var c in this.Cards)
                {
                    c.IsMatch = true;
                }

                return;
            }

            foreach (var c in this.Cards)
            {
                c.IsMatch = c.Hint.StartsWith(this.HintBuffer, StringComparison.OrdinalIgnoreCase);
            }

            var matches = this.Cards.Where(c => c.IsMatch).ToList();
            if (matches.Count == 0)
            {
                return;
            }

            var exact = matches.FirstOrDefault(c => c.Hint.Equals(this.HintBuffer, StringComparison.OrdinalIgnoreCase));
            var target = exact ?? matches[0];

            if (!ReferenceEquals(target, this.Selected))
            {
                this.SetSelected(target);
            }
        }

        /// <summary>
        /// Sets the selected card and retargets the sampler.
        /// </summary>
        /// <param name="card">The card to select.</param>
        private void SetSelected(WindowCardViewModel card)
        {
            foreach (var c in this.Cards)
            {
                c.IsSelected = false;
            }

            card.IsSelected = true;
            this.FullTitle = card.Title;

            // Retarget the long-lived sampler (non-blocking, immediate pulse).
            this.SetSamplerTargetForSelected();
        }

        /// <summary>
        /// Shows the preview overlay (called when Tab is held).
        /// </summary>
        public void ShowPreview()
        {
            if (this.Cards.Count == 0)
            {
                this.IsPreviewVisible = false;
                return;
            }

            if (this.Selected is null)
            {
                this.Cards[0].IsSelected = true;
                this.FullTitle = this.Cards[0].Title;
            }

            this.IsPreviewVisible = true;
            if (this.Selected is not null)
            {
                this.FullTitle = this.Selected.Title;
            }

            // Ensure the sampler is already looking at the current selection.
            this.SetSamplerTargetForSelected();
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

        /// <summary>
        /// Retargets the long-lived sampler to the selected window and requests an immediate sample.
        /// </summary>
        private void SetSamplerTargetForSelected()
        {
            var sel = this.Selected;
            if (sel is null)
            {
                return;
            }

            var hwnd = sel.Entry.Id.Hwnd;

            _ = Task.Run(() =>
            {
                try
                {
                    this.sampler.SetTarget(hwnd, immediate: true);

                    var snap = this.sampler.TrySnapshotCurrent();
                    if (snap is not null)
                    {
                        if (this.dispatcher is not null && !this.dispatcher.CheckAccess())
                        {
                            _ = this.dispatcher.InvokeAsync(() => this.PreviewStats = snap);
                        }
                        else
                        {
                            this.PreviewStats = snap;
                        }
                    }
                }
                catch
                {
                }
            });
        }

        /// <summary>
        /// Receives new sample updates from sampler thread.
        /// </summary>
        /// <param name="stats">Stats for the previewed process group.</param>
        private void OnSamplerSampleReceived(ProcessResourceStats stats)
        {
            if (this.dispatcher is not null && !this.dispatcher.CheckAccess())
            {
                _ = this.dispatcher.InvokeAsync(() => this.PreviewStats = stats);
            }
            else
            {
                this.PreviewStats = stats;
            }
        }

        #endregion
    }
}

