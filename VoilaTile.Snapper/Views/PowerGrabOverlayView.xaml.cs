namespace VoilaTile.Snapper.Views
{
    using System;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services;
    using VoilaTile.Snapper.ViewModels;
    using System.Collections.Generic;

    /// <summary>
    /// Represents the Power Mode overlay window that renders live DWM thumbnails for
    /// the currently enumerated windows and performs auto-fit sizing based on card layout.
    /// </summary>
    public partial class PowerGrabOverlayView : Window
    {
        #region Fields

        /// <summary>
        /// Padding applied around the content area of the window.
        /// </summary>
        private const double WindowPadding = 8.0;

        /// <summary>
        /// The outer margin contribution per card (e.g., border margins).
        /// </summary>
        private const double CardOuterMargin = 16.0;

        /// <summary>
        /// The inner margin contribution per card (e.g., internal grid margins).
        /// </summary>
        private const double CardInnerMargin = 24.0;

        /// <summary>
        /// The vertical outer margin contribution per card.
        /// </summary>
        private const double CardVertOuter = 16.0;

        /// <summary>
        /// The vertical inner margin contribution per card.
        /// </summary>
        private const double CardVertInner = 24.0;

        /// <summary>
        /// The minimum allowed card width in device-independent pixels.
        /// </summary>
        private const double MinCardWidth = 80.0;

        /// <summary>
        /// The maximum allowed card width in device-independent pixels.
        /// </summary>
        private const double MaxCardWidth = 420.0;

        /// <summary>
        /// Extra padding to ensure the last row does not visually touch the window edge.
        /// </summary>
        private const double SafetyPad = 12.0;

        /// <summary>
        /// A short-interval debounce timer used to coalesce frequent layout refresh triggers.
        /// </summary>
        private readonly DispatcherTimer debounceTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

        /// <summary>
        /// The factory used to create <see cref="IThumbnailSurface"/> instances bound to this window.
        /// </summary>
        private readonly IDwmThumbnailSurfaceFactory factory;

        /// <summary>
        /// The DWM thumbnail surface that manages registration and updates for individual thumbnails.
        /// </summary>
        private IThumbnailSurface? surface;

        /// <summary>
        /// The X DPI scale for the host window.
        /// </summary>
        private double dpiX = 1.0;

        /// <summary>
        /// The Y DPI scale for the host window.
        /// </summary>
        private double dpiY = 1.0;

        /// <summary>
        /// Indicates whether the overlay is currently in preview mode (single enlarged target).
        /// </summary>
        private bool previewMode;

        /// <summary>
        /// Prevents re-entrant sizing / refresh while FitOverlayToCards is running.
        /// </summary>
        private bool inSizing;

        /// <summary>
        /// Ensures we only do a single "growth" pass on first open to avoid extra tweaks.
        /// </summary>
        private bool hasGrownThisSession;

        /// <summary>
        /// Used to do a clean reveal after the initial layout converges.
        /// </summary>
        private bool firstRevealPending = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerGrabOverlayView"/> class.
        /// Sets up DPI scaling, creates the thumbnail surface on source initialization,
        /// hooks collection and layout events, and wires preview-mode handlers.
        /// </summary>
        /// <param name="factory">The factory used to create the DWM thumbnail surface.</param>
        public PowerGrabOverlayView(IDwmThumbnailSurfaceFactory factory)
        {
            this.InitializeComponent();
            this.Left = this.Top = -10000; // Initilize off-screen until automatically sized and centered.
            this.Opacity = 0;              // Hide visual churn during the very first converge.

            this.factory = factory;

            var dpi = VisualTreeHelper.GetDpi(this);
            this.dpiX = dpi.DpiScaleX; this.dpiY = dpi.DpiScaleY;

            this.SourceInitialized += (_, __) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;

                try
                {
                    this.surface = factory.Create(hwnd);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[PowerGrabOverlayView] Create surface FAILED: " + ex.Message);
                }

                this.RefreshAllThumbnailRects(0);
            };

            this.Loaded += (_, __) =>
            {
                this.HookCardsCollection();
                this.UpdateLayout();
                this.RefreshAllThumbnailRects(0);
                this.FitOverlayToCards();
                this.AttachTabEventHandlers();

                // Reveal only after the first full converge completed inside FitOverlayToCards.
                if (this.firstRevealPending == false && this.Opacity == 0)
                    this.Opacity = 1;

                Debug.WriteLine("[REVEAL] In Loaded");
            };

            this.SizeChanged += (_, __) => this.DebouncedRefresh();
            this.CardsItems.LayoutUpdated += (_, __) => this.DebouncedRefresh();
            this.DataContextChanged += (_, __) => this.HookCardsCollection();
            this.DataContextChanged += (_, __) => this.AttachTabEventHandlers();

            this.Closed += (_, __) =>
            {
                try { this.debounceTimer?.Stop(); } catch { }
                try { this.surface?.Dispose(); } catch { }
                this.surface = null;

                this.DetachTabEventHandlers();
            };

            this.CardsItems.LayoutUpdated += (_, __) =>
            {
                // If preview is visible and selection moved, refresh preview rect
                if (this.DataContext is PowerGrabOverlayViewModel vm && vm.IsPreviewVisible)
                {
                    this.DebouncedRefresh();
                }
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// The collection currently hooked for card change notifications.
        /// </summary>
        private ObservableCollection<WindowCardViewModel>? cardsHooked;

        /// <summary>
        /// Gets the cards collection from the current <see cref="PowerGrabOverlayViewModel"/>, if available.
        /// </summary>
        private ObservableCollection<WindowCardViewModel>? Cards
            => this.DataContext is PowerGrabOverlayViewModel vm ? vm.Cards : null;

        #endregion

        #region Methods

        /// <summary>
        /// Hooks the cards collection to receive change notifications for layout refresh.
        /// </summary>
        private void HookCardsCollection()
        {
            if (this.DataContext is PowerGrabOverlayViewModel vm &&
                vm.Cards is ObservableCollection<WindowCardViewModel> cardCollection)
            {
                if (this.cardsHooked != null) this.cardsHooked.CollectionChanged -= this.OnCardsChanged;
                this.cardsHooked = cardCollection;
                this.cardsHooked.CollectionChanged += this.OnCardsChanged;
            }
        }

        /// <summary>
        /// Attaches preview-mode event handlers to the view model, if present.
        /// </summary>
        private void AttachTabEventHandlers()
        {
            if (this.DataContext is PowerGrabOverlayViewModel vm)
            {
                vm.OnShowPreview += this.EnterPreviewMode;
                vm.OnHidePreview += this.ExitPreviewMode;
            }
        }

        /// <summary>
        /// Detaches preview-mode event handlers from the view model, if present.
        /// </summary>
        private void DetachTabEventHandlers()
        {
            if (this.DataContext is PowerGrabOverlayViewModel vm)
            {
                vm.OnShowPreview -= this.EnterPreviewMode;
                vm.OnHidePreview -= this.ExitPreviewMode;
            }
        }

        /// <summary>
        /// Handles changes in the cards collection by triggering a debounced refresh.
        /// </summary>
        /// <param name="sender">The collection that changed.</param>
        /// <param name="e">The collection change event data.</param>
        private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e) => this.DebouncedRefresh();

        /// <summary>
        /// Restarts the short debounce timer so size/layout recalculation is batched.
        /// </summary>
        private void DebouncedRefresh()
        {
            if (this.inSizing) return; // mute re-entrant triggers while sizing
            this.debounceTimer.Stop();
            this.debounceTimer.Tick -= this.OnDebounce;
            this.debounceTimer.Tick += this.OnDebounce;
            this.debounceTimer.Start();
        }

        /// <summary>
        /// Executes the debounced refresh: updates layout, rects, and auto-fit calculations.
        /// </summary>
        /// <param name="s">The timer source.</param>
        /// <param name="e">The event arguments.</param>
        private void OnDebounce(object? s, EventArgs e)
        {
            if (this.inSizing) return; // extra guard
            this.debounceTimer.Stop();
            this.UpdateLayout();
            this.RefreshAllThumbnailRects();
            this.FitOverlayToCards();
        }

        /// <summary>
        /// Updates DWM thumbnail rectangles for all visible cards, or only the preview target if in preview mode.
        /// </summary>
        private void RefreshAllThumbnailRects(byte opacity = 255)
        {
            if (this.surface is null) { Debug.WriteLine("[Overlay] Refresh skipped (surface=null)"); return; }
            if (this.CardsItems.Items.Count == 0) return;

            // If we're in preview mode, only update the preview slot to avoid grid overdraw.
            if (this.previewMode && this.DataContext is PowerGrabOverlayViewModel pvm && pvm.PreviewTarget is not null)
            {
                var previewSlot = this.FindPreviewSlot();
                if (previewSlot is FrameworkElement slot && slot.IsVisible)
                {
                    var rectPx = this.GetSlotRectInSurfacePixels(slot);
                    if (rectPx.W > 0 && rectPx.H > 0)
                    {
                        try
                        {
                            var entry = pvm.PreviewTarget.Entry;
                            this.surface.EnsureRegistered(entry.Id);

                            if (entry.SourceClientSize is null && this.surface.TryGetSourceSize(entry.Id, out var src))
                            {
                                entry.SourceClientSize = src;
                                pvm.PreviewTarget.SetSourceSize(src);
                            }

                            var destPx = Letterbox(rectPx, entry.SourceClientSize);
                            this.surface.UpdateRect(entry.Id, Intersect(destPx, rectPx), opacity);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Overlay] DWM preview update failed: {ex.Message}");
                        }
                    }
                }
                return;
            }

            // Update grid items.
            int updated = 0, count = this.CardsItems.Items.Count;
            for (int i = 0; i < count; i++)
            {
                var item = this.CardsItems.Items[i];
                if (this.CardsItems.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container) continue;

                var slot = FindThumbnailSlot(container);
                if (slot is null) continue;
                if (item is not WindowCardViewModel vm) continue;

                var rect = this.GetSlotRectInPixels(slot);
                if (rect.W <= 0 || rect.H <= 0)
                {
                    slot.Dispatcher.BeginInvoke(this.RefreshAllThumbnailRects, DispatcherPriority.Loaded);
                    continue;
                }

                try
                {
                    var slotPx = this.GetSlotRectInSurfacePixels(slot);
                    this.surface.EnsureRegistered(vm.Entry.Id);

                    if (vm.Entry.SourceClientSize is null && this.surface.TryGetSourceSize(vm.Entry.Id, out var src))
                    {
                        vm.Entry.SourceClientSize = src;
                        vm.SetSourceSize(src);
                    }

                    var destPx = Letterbox(slotPx, vm.Entry.SourceClientSize);
                    this.surface.UpdateRect(vm.Entry.Id, Intersect(destPx, slotPx), opacity);
                    updated++;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Overlay] DWM update failed hwnd={vm.Entry.Id.Hwnd}: {ex.Message}");
                }
            }

            Debug.WriteLine($"[Overlay] Updated {updated}/{count} thumbnails");
        }

        /// <summary>
        /// Searches for the thumbnail slot element within an item container by name.
        /// </summary>
        private static FrameworkElement? FindThumbnailSlot(FrameworkElement container)
        {
            if (container is ContentPresenter cp)
            {
                cp.ApplyTemplate();
                if (cp.ContentTemplate is not null)
                {
                    var elt = cp.ContentTemplate.FindName("ThumbnailSlot", cp) as FrameworkElement;
                    if (elt is not null) return elt;
                }
                if (VisualTreeHelper.GetChildrenCount(cp) > 0 &&
                    VisualTreeHelper.GetChild(cp, 0) is FrameworkElement root)
                {
                    var deep = FindDescendantByName(root, "ThumbnailSlot");
                    if (deep is not null) return deep;
                }
            }
            return FindDescendantByName(container, "ThumbnailSlot");
        }

        /// <summary>
        /// Performs a depth-first search for a descendant element by name.
        /// </summary>
        private static FrameworkElement? FindDescendantByName(DependencyObject start, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(start);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                if (child is FrameworkElement fe)
                {
                    if (fe.Name == name) return fe;
                    var deeper = FindDescendantByName(fe, name);
                    if (deeper is not null) return deeper;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the preview slot element inside the view (by the known preview slot name).
        /// </summary>
        private FrameworkElement? FindPreviewSlot()
        {
            return FindDescendantByName(this.Root, "PreviewThumbnailSlot");
        }

        /// <summary>
        /// Computes the union of all rendered card containers in device-independent pixels.
        /// </summary>
        private Rect GetCardsBoundingRectDips()
        {
            Rect union = Rect.Empty;
            int n = this.CardsItems.Items.Count;

            for (int i = 0; i < n; i++)
            {
                if (this.CardsItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement cont &&
                    cont.IsVisible &&
                    cont.ActualWidth > 0 &&
                    cont.ActualHeight > 0)
                {
                    var local = new Rect(0, 0, cont.ActualWidth, cont.ActualHeight);
                    var bounds = cont.TransformToAncestor(this.Root).TransformBounds(local);
                    union = union.IsEmpty ? bounds : Rect.Union(union, bounds);
                }
            }

            return union;
        }

        /// <summary>
        /// Attempts to close the overlay safely from any thread, disposing resources as needed.
        /// </summary>
        public void CloseSafely()
        {
            try
            {
                if (this.IsLoaded == false && this.IsVisible == false) return;
                void DoClose()
                {
                    try
                    {
                        this.debounceTimer?.Stop();
                        try { this.surface?.Dispose(); } catch { }
                        this.surface = null;
                        this.Close();
                    }
                    catch { }
                }
                if (this.Dispatcher.CheckAccess()) DoClose();
                else this.Dispatcher.Invoke((Action)DoClose);
            }
            catch { }
        }

        /// <summary>
        /// Computes a letterboxed destination rectangle inside <paramref name="dest"/> that preserves the aspect ratio of <paramref name="src"/>.
        /// </summary>
        private static RectPx Letterbox(RectPx dest, SizePx? src)
        {
            if (src is null || src.Value.W <= 0 || src.Value.H <= 0) return dest;

            double dw = dest.W, dh = dest.H;
            double sw = src.Value.W, sh = src.Value.H;

            double scale = Math.Min(dw / sw, dh / sh);
            int w = (int)Math.Round(sw * scale);
            int h = (int)Math.Round(sh * scale);

            int x = dest.X + (dest.W - w) / 2;
            int y = dest.Y + (dest.H - h) / 2;

            return new RectPx(x, y, w, h);
        }

        /// <summary>
        /// Returns the intersection of two rectangles in pixel coordinates.
        /// </summary>
        private static RectPx Intersect(RectPx a, RectPx b)
        {
            int x1 = Math.Max(a.X, b.X);
            int y1 = Math.Max(a.Y, b.Y);
            int x2 = Math.Min(a.X + a.W, b.X + b.W);
            int y2 = Math.Min(a.Y + a.H, b.Y + b.H);
            int w = Math.Max(0, x2 - x1);
            int h = Math.Max(0, y2 - y1);
            return new RectPx(x1, y1, w, h);
        }

        /// <summary>
        /// Gets the rectangle of a slot relative to the window, converted to pixel coordinates (accounting for DPI).
        /// </summary>
        private RectPx GetSlotRectInPixels(FrameworkElement slot)
        {
            var topLeft = slot.TranslatePoint(new Point(0, 0), this);
            double w = slot.ActualWidth;
            double h = slot.ActualHeight;

            int x = (int)Math.Round(topLeft.X * this.dpiX);
            int y = (int)Math.Round(topLeft.Y * this.dpiY);
            int pw = (int)Math.Round(w * this.dpiX);
            int ph = (int)Math.Round(h * this.dpiY);

            return new RectPx(x, y, pw, ph);
        }

        /// <summary>
        /// Gets the rectangle of a slot relative to this host, clamped to the host's bounds and converted to surface pixel coordinates.
        /// </summary>
        private RectPx GetSlotRectInSurfacePixels(FrameworkElement slot)
        {
            if (slot == null || !slot.IsVisible ||
                double.IsNaN(slot.ActualWidth) || double.IsNaN(slot.ActualHeight) ||
                slot.ActualWidth <= 0 || slot.ActualHeight <= 0)
                return new RectPx(0, 0, 0, 0);

            var host = this as FrameworkElement;
            if (host == null || !host.IsVisible) return new RectPx(0, 0, 0, 0);

            Point tl;
            try
            {
                var tx = slot.TransformToVisual(host);
                tl = tx.Transform(new Point(0, 0));
            }
            catch { return new RectPx(0, 0, 0, 0); }

            double lw = slot.ActualWidth;
            double lh = slot.ActualHeight;

            var dpi = VisualTreeHelper.GetDpi(host);
            double sx = dpi.DpiScaleX;
            double sy = dpi.DpiScaleY;

            int x = (int)Math.Round(tl.X * sx);
            int y = (int)Math.Round(tl.Y * sy);
            int w = (int)Math.Round(lw * sx);
            int h = (int)Math.Round(lh * sy);

            int hostW = (int)Math.Max(0, Math.Round(host.ActualWidth * sx));
            int hostH = (int)Math.Max(0, Math.Round(host.ActualHeight * sy));

            int x2 = Math.Clamp(x + w, 0, hostW);
            int y2 = Math.Clamp(y + h, 0, hostH);
            x = Math.Clamp(x, 0, hostW);
            y = Math.Clamp(y, 0, hostH);
            w = Math.Max(0, x2 - x);
            h = Math.Max(0, y2 - y);

            return new RectPx(x, y, w, h);
        }

        /// <summary>
        /// Called when the window content is first rendered. Kicks off a deferred auto-fit.
        /// </summary>
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _ = this.Dispatcher.BeginInvoke((Action)this.FitOverlayToCards, DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Computes and applies an overlay size that fits the current cards layout,
        /// first by growing the window up to the work area, then—if capped—by shrinking
        /// card thumbnails. Uses the simulator to get close, then validates against
        /// realized (measured) layout and, if needed, runs a measured fit pass.
        /// Finally, if there is headroom left after fitting, it grows thumbnails to utilize it.
        /// </summary>
        private void FitOverlayToCards()
        {
            if (this.inSizing) return;
            this.inSizing = true;
            try
            {
                var cards = this.Cards;
                if (cards is null || cards.Count == 0) return;

                var work = SystemParameters.WorkArea;
                double maxWinW = Math.Max(200, work.Width - 2 * WindowPadding);
                double maxWinH = Math.Max(200, work.Height - 2 * WindowPadding);

                // Use actual header height + its bottom margin (what the sim compares against).
                double headerBlock = (this.Header?.ActualHeight ?? 0) + (this.Header?.Margin.Bottom ?? 0);

                // --- Pass 1: Simulated binary search (original flow) ---
                double baseThumbH = cards[0].ThumbHeight;
                var sim0 = this.SimulateLayout(baseThumbH);

                if (sim0.ContentW <= maxWinW && sim0.ContentH + headerBlock <= maxWinH)
                {
                    // Window can grow to fit without shrinking thumbs.
                    var targetWidth = Math.Min(maxWinW, sim0.ContentW) + 2 * WindowPadding;
                    var targetHeight = Math.Min(maxWinH, sim0.ContentH + headerBlock + SafetyPad) + 2 * WindowPadding + 32;
                    this.ApplySizeAndCenterAtomic(targetWidth, targetHeight);

                    // Diagnostics: compare sim vs realized.
                    this.InvalidateMeasure();
                    this.UpdateLayout();
                    var cardsBounds = this.GetCardsBoundingRectDips();
                    this.LogLayoutDiagnostics(sim0, headerBlock, maxWinW, maxWinH, cardsBounds, "Original Flow");

                    // Fully utilize headroom (once) after first open.
                    if (!this.hasGrownThisSession)
                    {
                        this.GrowThumbsToUseHeadroom(headerBlock, maxWinW, maxWinH);
                        this.hasGrownThisSession = true;
                    }

                    return;
                }

                // Simulated shrink search. (reduced iterations for faster convergence)
                double lo = Math.Max(60.0, baseThumbH * 0.35);
                double hi = baseThumbH;
                double bestSimH = lo;
                for (int i = 0; i < 6; i++)
                {
                    double mid = (lo + hi) * 0.5;
                    var sim = this.SimulateLayout(mid);
                    bool fits = sim.ContentW <= maxWinW && (sim.ContentH + headerBlock) <= maxWinH;
                    if (fits) { bestSimH = mid; lo = mid; } else { hi = mid; }
                }

                // Apply the simulated best height to visuals.
                foreach (var card in cards)
                {
                    card.ThumbHeight = bestSimH;
                    var src = card.Entry.SourceClientSize ?? new SizePx(1600, 900);
                    card.SetSourceSize(src);
                }

                // Force measure/arrange and compute realized bounds.
                this.InvalidateMeasure();
                this.UpdateLayout();

                var realizedBounds = this.GetCardsBoundingRectDips();
                double realizedW = realizedBounds.Width;
                double realizedH = realizedBounds.Height;

                // Size and center the window on those realized bounds (clamped to work area).
                double finalW = Math.Min(maxWinW, realizedW) + 2 * WindowPadding;
                double finalH = Math.Min(maxWinH, realizedH + headerBlock + SafetyPad) + 2 * WindowPadding;
                this.ApplySizeAndCenterAtomic(finalW, finalH);

                // Diagnostics after simulated pass
                this.LogLayoutDiagnostics(this.SimulateLayout(bestSimH), headerBlock, maxWinW, maxWinH, realizedBounds, "Simulation");

                // --- Pass 2: Measured fit pass (only if still too tall/wide) ---
                const double tol = 0.75;
                bool heightOverflow = (realizedH + headerBlock) - maxWinH > tol;
                bool widthOverflow = realizedW - maxWinW > tol;

                if (!(heightOverflow || widthOverflow))
                {
                    // Fits inside the work area with current thumbs — now use headroom (once).
                    if (!this.hasGrownThisSession)
                    {
                        this.GrowThumbsToUseHeadroom(headerBlock, maxWinW, maxWinH);
                        this.hasGrownThisSession = true;
                    }
                    return;
                }

                // Measured tighten using realized layout as oracle. (reduced iterations)
                double hiMeasured = bestSimH;
                double loMeasured = Math.Max(48.0, hiMeasured * 0.35);
                double bestMeasuredFit = loMeasured;

                for (int i = 0; i < 4; i++)
                {
                    double mid = (loMeasured + hiMeasured) * 0.5;

                    foreach (var card in cards)
                    {
                        card.ThumbHeight = mid;
                        var src = card.Entry.SourceClientSize ?? new SizePx(1600, 900);
                        card.SetSourceSize(src);
                    }

                    this.InvalidateMeasure();
                    this.UpdateLayout();
                    var rb = this.GetCardsBoundingRectDips();

                    bool measuredFits =
                        rb.Width <= maxWinW + tol &&
                        rb.Height + headerBlock <= maxWinH + tol;

                    if (measuredFits)
                    {
                        bestMeasuredFit = mid;
                        loMeasured = mid; // grow until we hit the largest that still fits
                    }
                    else
                    {
                        hiMeasured = mid; // too big; shrink
                    }
                }

                // Apply the best measured fit height and finalize window size on measured bounds.
                foreach (var card in cards)
                {
                    card.ThumbHeight = bestMeasuredFit;
                    var src = card.Entry.SourceClientSize ?? new SizePx(1600, 900);
                    card.SetSourceSize(src);
                }

                this.InvalidateMeasure();
                this.UpdateLayout();

                realizedBounds = this.GetCardsBoundingRectDips();
                realizedW = realizedBounds.Width;
                realizedH = realizedBounds.Height;

                finalW = Math.Min(maxWinW, realizedW) + 2 * WindowPadding;
                finalH = Math.Min(maxWinH, realizedH + headerBlock + SafetyPad) + 2 * WindowPadding;
                this.ApplySizeAndCenterAtomic(finalW, finalH);

                // Final diagnostics after measured pass.
                this.LogLayoutDiagnostics(this.SimulateLayout(bestMeasuredFit), headerBlock, maxWinW, maxWinH, realizedBounds, "Best Measured");

                // After tightening, if there’s still headroom, use it (once).
                if (!this.hasGrownThisSession)
                {
                    this.GrowThumbsToUseHeadroom(headerBlock, maxWinW, maxWinH);
                    this.hasGrownThisSession = true;
                }
            }
            finally
            {
                this.inSizing = false;

                // First clean reveal after we converged once.
                if (this.firstRevealPending)
                {
                    this.firstRevealPending = false;
                    if (this.Opacity == 0) this.Opacity = 1;
                    Debug.WriteLine("[REVEAL] In FitOverlayToCards");
                }

                // Ensure thumbnails reposition after size changes.
                this.DebouncedRefresh();
            }
        }

        /// <summary>
        /// If the realized content fits under caps with headroom, increase ThumbHeight to
        /// utilize more of the work area while keeping both width and height within limits.
        /// Uses realized (measured) layout as the oracle, with hysteresis and a row-count guard
        /// to avoid oscillating between wrap boundaries.
        /// </summary>
        private void GrowThumbsToUseHeadroom(double headerBlock, double maxWinW, double maxWinH)
        {
            var cards = this.Cards;
            if (cards is null || cards.Count == 0) return;

            // Measure current realized bounds and base row count.
            this.InvalidateMeasure();
            this.UpdateLayout();
            var baseBounds = this.GetCardsBoundingRectDips();
            var baseRows = this.MeasureWrapRows().Count;

            const double tol = 0.75;

            // Require meaningful headroom to attempt growth (hysteresis).
            double headroom = (maxWinH - (baseBounds.Height + headerBlock));
            const double growHysteresis = 24.0; // DIPs
            if (headroom <= growHysteresis)
                return;

            double current = cards[0].ThumbHeight;

            // Define a modest growth window; we’ll tighten via bisection against measured layout.
            double lo = current;
            double hi = Math.Max(lo + 120.0, lo * 1.6);

            // Ensure 'hi' eventually does NOT fit (to bound the search). Up to 2 gentle expansions.
            for (int k = 0; k < 2; k++)
            {
                foreach (var c in cards)
                {
                    c.ThumbHeight = hi;
                    var src = c.Entry.SourceClientSize ?? new SizePx(1600, 900);
                    c.SetSourceSize(src);
                }

                this.InvalidateMeasure();
                this.UpdateLayout();
                var rb = this.GetCardsBoundingRectDips();
                var rowsNow = this.MeasureWrapRows().Count;

                bool fits =
                    rb.Width <= maxWinW + tol &&
                    rb.Height + headerBlock <= maxWinH + tol &&
                    rowsNow <= Math.Max(1, baseRows); // do not accept an extra row

                if (fits)
                {
                    lo = hi;          // accept and try higher
                    hi = lo + 160.0;  // gentle step up
                }
                else
                {
                    break;            // 'hi' is above feasible range
                }
            }

            // Bisection to pick the largest height that still fits without adding rows. (reduced steps)
            for (int i = 0; i < 4; i++)
            {
                double mid = (lo + hi) * 0.5;

                foreach (var c in cards)
                {
                    c.ThumbHeight = mid;
                    var src = c.Entry.SourceClientSize ?? new SizePx(1600, 900);
                    c.SetSourceSize(src);
                }

                this.InvalidateMeasure();
                this.UpdateLayout();
                var rb = this.GetCardsBoundingRectDips();
                var rowsNow = this.MeasureWrapRows().Count;

                bool fits =
                    rb.Width <= maxWinW + tol &&
                    rb.Height + headerBlock <= maxWinH + tol &&
                    rowsNow <= Math.Max(1, baseRows);

                if (fits) lo = mid; else hi = mid;
            }

            // Apply the chosen grown height (lo) and size the window to the realized content.
            foreach (var c in cards)
            {
                c.ThumbHeight = lo;
                var src = c.Entry.SourceClientSize ?? new SizePx(1600, 900);
                c.SetSourceSize(src);
            }

            this.InvalidateMeasure();
            this.UpdateLayout();
            var finalBounds = this.GetCardsBoundingRectDips();

            double finalW = Math.Min(maxWinW, finalBounds.Width) + 2 * WindowPadding;
            double finalH = Math.Min(maxWinH, finalBounds.Height + headerBlock + SafetyPad) + 2 * WindowPadding;
            this.ApplySizeAndCenterAtomic(finalW, finalH);

            // Optional: diagnostics to confirm stability and utilization.
            this.LogLayoutDiagnostics(this.SimulateLayout(lo), headerBlock, maxWinW, maxWinH, finalBounds, "GrowThumbsToUseHeadroom");
        }

        /// <summary>
        /// Logs a side-by-side comparison between simulated and realized layout, and prints
        /// WrapPanel row diagnostics (row count and max heights) to help pinpoint optimism gaps.
        /// </summary>
        private void LogLayoutDiagnostics(
            (double ContentW, double ContentH, int Rows) sim,
            double headerBlock,
            double maxWinW,
            double maxWinH,
            Rect realizedBounds,
            string entryPoint)
        {
            try
            {
                double realizedW = realizedBounds.Width;
                double realizedH = realizedBounds.Height;

                Debug.WriteLine($"=== PowerGrabOverlay Diagnostics: {entryPoint} ===");
                Debug.WriteLine($"WorkArea Caps (content): W<= {maxWinW:0.##}, H<= {maxWinH:0.##}  (header+pad handled separately)");
                Debug.WriteLine($"Simulated  : W= {sim.ContentW:0.##}, H= {sim.ContentH:0.##}, Rows= {sim.Rows}");
                Debug.WriteLine($"Realized   : W= {realizedW:0.##}, H= {realizedH:0.##}, Rows= (see below)");
                Debug.WriteLine($"HeaderBlk  : {headerBlock:0.##}");
                Debug.WriteLine($"Sim Fits?  : W {(sim.ContentW <= maxWinW ? "OK" : "OVER")}  |  H {(sim.ContentH + headerBlock <= maxWinH ? "OK" : "OVER")}");
                Debug.WriteLine($"Real Fits? : W {(realizedW <= maxWinW ? "OK" : "OVER")}  |  H {(realizedH + headerBlock <= maxWinH ? "OK" : "OVER")}");

                var rows = this.MeasureWrapRows();
                if (rows.Count == 0)
                {
                    Debug.WriteLine("Rows: (no containers realized yet)");
                }
                else
                {
                    Debug.WriteLine($"Rows (realized): {rows.Count}");
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        Debug.WriteLine($"  Row {i + 1}: items {r.startIndex}-{r.endIndex}, top={r.rowTop:0.##}, bottom={r.rowBottom:0.##}, maxH={r.maxHeight:0.##}");
                    }
                }
                Debug.WriteLine("====================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Diagnostics] Failed to log layout diagnostics: " + ex.Message);
            }
        }

        /// <summary>
        /// Heuristically groups item containers into WrapPanel rows by their top Y (relative to Root),
        /// using a small tolerance to account for fractional DIP rounding.
        /// </summary>
        private List<(int startIndex, int endIndex, double rowTop, double rowBottom, double maxHeight)> MeasureWrapRows()
        {
            var result = new List<(int, int, double, double, double)>();
            if (this.CardsItems.Items.Count == 0) return result;

            // Collect realized containers with their top/bottom coordinates in DIPs.
            var items = new List<(int index, double top, double bottom, double height)>();
            for (int i = 0; i < this.CardsItems.Items.Count; i++)
            {
                if (this.CardsItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement cont &&
                    cont.IsVisible &&
                    cont.ActualWidth > 0 &&
                    cont.ActualHeight > 0)
                {
                    var local = new Rect(0, 0, cont.ActualWidth, cont.ActualHeight);
                    var bounds = cont.TransformToAncestor(this.Root).TransformBounds(local);
                    items.Add((i, bounds.Top, bounds.Bottom, cont.ActualHeight));
                }
            }

            if (items.Count == 0) return result;

            // Sort by Y then X implicitly (Top first). We just need stable top grouping.
            items.Sort((a, b) =>
            {
                int c = a.top.CompareTo(b.top);
                if (c != 0) return c;
                return a.index.CompareTo(b.index);
            });

            // Group into rows by near-equal "top" with a tolerance.
            const double rowTol = 1.0; // DIP tolerance for grouping (accounts for rounding)
            var current = new List<(int index, double top, double bottom, double height)>();
            double currentTop = items[0].top;

            foreach (var it in items)
            {
                if (Math.Abs(it.top - currentTop) <= rowTol)
                {
                    current.Add(it);
                }
                else
                {
                    // finalize previous row
                    current.Sort((a, b) => a.index.CompareTo(b.index));
                    var rowTop = current.Min(x => x.top);
                    var rowBottom = current.Max(x => x.bottom);
                    var maxH = current.Max(x => x.height);
                    result.Add((current.First().index, current.Last().index, rowTop, rowBottom, maxH));

                    // start new row
                    current.Clear();
                    currentTop = it.top;
                    current.Add(it);
                }
            }

            // finalize last row
            if (current.Count > 0)
            {
                current.Sort((a, b) => a.index.CompareTo(b.index));
                var rowTop = current.Min(x => x.top);
                var rowBottom = current.Max(x => x.bottom);
                var maxH = current.Max(x => x.height);
                result.Add((current.First().index, current.Last().index, rowTop, rowBottom, maxH));
            }

            return result;
        }

        /// <summary>
        /// Compute a fast layout by searching over column counts (few tries),
        /// deriving the thumb height from available height (rows = ceil(n/cols)),
        /// and validating width with a single simulation. Returns null if nothing fits.
        /// </summary>
        private (double thumbH, double contentW, double contentH, int rows)? FastFit(
            double maxWinW, double maxWinH, double headerBlock)
        {
            var cards = this.Cards!;
            int n = cards.Count;
            if (n == 0) return null;

            // Precompute per-card aspect ratios and clamp logic we already use in SimulateLayout.
            static double CardWidthFromH(double thumbH, SizePx src, double inner, double outer)
            {
                double w = thumbH * (double)src.W / Math.Max(1, src.H);
                if (w < MinCardWidth) w = MinCardWidth;
                if (w > MaxCardWidth) w = MaxCardWidth;
                return w + inner + outer;
            }

            // We’ll try between maxColumns..1, where maxColumns is what fits with min widths.
            double minCardW = MinCardWidth + CardInnerMargin + CardOuterMargin;
            int maxColumns = (int)Math.Max(1, Math.Floor(maxWinW / minCardW));
            maxColumns = Math.Min(maxColumns, n);

            // Small search set: try [maxColumns .. max(1, maxColumns-7)] to keep it quick but robust.
            int minColumns = Math.Max(1, maxColumns - 7);

            // Cache source sizes (or fallback) to avoid TryGetSourceSize calls here.
            var sources = cards
                .Select(c => c.Entry.SourceClientSize ?? new SizePx(1600, 900))
                .ToArray();

            (double thumbH, double contentW, double contentH, int rows)? best = null;

            for (int cols = maxColumns; cols >= minColumns; cols--)
            {
                // Rows is height-driven: rows = ceil(n / cols)
                int rows = (n + cols - 1) / cols;

                // From height cap, compute how tall each card can be:
                // total content height = rows * (thumbH + vertical paddings)
                // => thumbH = (maxWinH - headerBlock - SafetyPad) / rows - (CardVertInner + CardVertOuter)
                double perRowAvailable = maxWinH - headerBlock - SafetyPad;
                double thumbH = (perRowAvailable / rows) - (CardVertInner + CardVertOuter);

                // Respect clamp
                thumbH = Math.Clamp(thumbH, 48.0, MaxCardWidth); // lower bound ~48; upper bound is generous (width clamp will stop growth)

                // Validate width with a single, fast simulation (no UpdateLayout calls).
                double rowW = 0.0, contentW = 0.0;
                int usedRows = 1;
                for (int i = 0; i < n; i++)
                {
                    double w = CardWidthFromH(thumbH, sources[i], CardInnerMargin, CardOuterMargin);

                    if (rowW == 0.0) { rowW = w; continue; }
                    if (rowW + w > maxWinW)
                    {
                        contentW = Math.Max(contentW, rowW);
                        usedRows++;
                        rowW = w;
                    }
                    else
                    {
                        rowW += w;
                    }
                }
                if (rowW > 0.0) contentW = Math.Max(contentW, rowW);

                // If width grew over cap, this column count is too optimistic; skip.
                if (contentW > maxWinW + 0.75) continue;

                // If width fits but rows wrapped more than we planned, skip (height formula was optimistic).
                if (usedRows > rows) continue;

                double contentH = rows * (thumbH + CardVertInner + CardVertOuter);

                var candidate = (thumbH, contentW, contentH, rows);

                // Pick the candidate which uses the most height (i.e., largest thumbH) while fitting
                if (best is null || candidate.thumbH > best.Value.thumbH)
                    best = candidate;
            }

            return best;
        }


        /// <summary>
        /// Simulates card wrapping for a given thumbnail height to determine total content size.
        /// </summary>
        private (double ContentW, double ContentH, int Rows) SimulateLayout(double thumbH)
        {
            var cards = this.Cards!;
            var widths = cards.Select(card =>
            {
                var src = card.Entry.SourceClientSize ?? new SizePx(1600, 900);
                double w = thumbH * (double)src.W / Math.Max(1, src.H);
                if (w < MinCardWidth) w = MinCardWidth;
                if (w > MaxCardWidth) w = MaxCardWidth;
                return w + CardInnerMargin + CardOuterMargin;
            }).ToList();

            double cardH = thumbH + CardVertInner + CardVertOuter + 50;
            double maxRowWidth = Math.Max(200, SystemParameters.WorkArea.Width - 2 * WindowPadding);

            double rowW = 0.0, contentW = 0.0;
            int rows = 1;
            foreach (var w in widths)
            {
                if (rowW == 0.0) { rowW = w; continue; }
                if (rowW + w > maxRowWidth)
                {
                    contentW = Math.Max(contentW, rowW);
                    rows++;
                    rowW = w;
                }
                else
                {
                    rowW += w;
                }
            }
            if (rowW > 0.0) contentW = Math.Max(contentW, rowW);

            double contentH = rows * cardH;
            return (contentW, contentH, rows);
        }

        /// <summary>
        /// Applies the specified window size (clamped to work area) and centers the overlay.
        /// </summary>
        private void ApplySizeAndCenterAtomic(double targetWidthDip, double targetHeightDip)
        {
            // Work area in DIPs (WPF)
            var wa = SystemParameters.WorkArea;

            targetWidthDip = Math.Min(targetWidthDip, wa.Width);
            targetHeightDip = Math.Min(targetHeightDip, wa.Height);

            double leftDip = wa.Left + (wa.Width - targetWidthDip) / 2.0;
            double topDip = wa.Top + (wa.Height - targetHeightDip) / 2.0;

            // Convert DIPs -> pixels for Win32
            var dpi = VisualTreeHelper.GetDpi(this);
            int x = (int)Math.Round(leftDip * dpi.DpiScaleX);
            int y = (int)Math.Round(topDip * dpi.DpiScaleY);
            int cx = (int)Math.Round(targetWidthDip * dpi.DpiScaleX);
            int cy = (int)Math.Round(targetHeightDip * dpi.DpiScaleY);

            var hwnd = new WindowInteropHelper(this).Handle;

            // Position + size in one call; don’t change Z-order, don’t activate, avoid churn.
            SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SWP.NOZORDER | SWP.NOACTIVATE | SWP.NOSENDCHANGING);
        }

        /// <summary>
        /// Enters preview mode by hiding grid thumbnails (except preview) and forcing a refresh.
        /// </summary>
        private void EnterPreviewMode()
        {
            if (this.previewMode) return;
            this.previewMode = true;

            this.HideGridThumbnailsExceptPreview();
            this.UpdateLayout();
            this.RefreshAllThumbnailRects();
        }

        /// <summary>
        /// Exits preview mode by showing the grid thumbnails again and repainting.
        /// </summary>
        private void ExitPreviewMode()
        {
            if (!this.previewMode) return;
            this.previewMode = false;

            this.ShowGridThumbnails();
            this.UpdateLayout();
            this.RefreshAllThumbnailRects();
        }

        /// <summary>
        /// Hides all DWM thumbnails in the grid except for the current preview target, if any.
        /// </summary>
        private void HideGridThumbnailsExceptPreview()
        {
            if (this.surface is null) return;
            if (this.DataContext is not PowerGrabOverlayViewModel vm) return;

            var previewHwnd = vm.PreviewTarget?.Entry.Id;

            // Iterate all item containers and set alpha=0 for non-preview thumbnails.
            int n = this.CardsItems.Items.Count;
            for (int i = 0; i < n; i++)
            {
                if (this.CardsItems.Items[i] is not WindowCardViewModel cardVm) continue;
                if (previewHwnd is not null && cardVm.Entry.Id.Equals(previewHwnd.Value)) continue;

                var cont = this.CardsItems.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                var slot = cont is null ? null : FindThumbnailSlot(cont);
                if (slot is null) continue;

                var rectPx = this.GetSlotRectInSurfacePixels(slot);
                if (rectPx.W <= 0 || rectPx.H <= 0) continue;

                try
                {
                    this.surface.EnsureRegistered(cardVm.Entry.Id);
                    this.surface.UpdateRect(cardVm.Entry.Id, rectPx, 0); // alpha=0 hides the thumbnail.
                }
                catch { }
            }
        }

        /// <summary>
        /// Shows (repaints) all DWM thumbnails in the grid after leaving preview mode.
        /// </summary>
        private void ShowGridThumbnails()
        {
            if (this.surface is null) return;

            this.RefreshAllThumbnailRects();
        }

        #endregion

        #region Interop

        [Flags]
        private enum SWP : uint
        {
            NOSIZE = 0x0001,
            NOMOVE = 0x0002,
            NOZORDER = 0x0004,
            NOREDRAW = 0x0008,
            NOACTIVATE = 0x0010,
            FRAMECHANGED = 0x0020,
            SHOWWINDOW = 0x0040,
            NOSENDCHANGING = 0x0400,
            ASYNCWINDOWPOS = 0x4000,
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, SWP uFlags);

        #endregion
    }
}

