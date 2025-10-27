namespace VoilaTile.Snapper.Views
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using VoilaTile.Snapper.Records;
    using VoilaTile.Snapper.Services;
    using VoilaTile.Snapper.ViewModels;

    /// <summary>
    /// Full-work-area overlay with keyboard-only interaction and DWM thumbnails.
    /// </summary>
    public partial class PowerGrabOverlayView : Window
    {
        #region Fields

        /// <summary>
        /// Outer margin of the overlay window in device-independent pixels (DIPs).
        /// </summary>
        private const double WindowMarginDip = 16.0;

        /// <summary>
        /// Gap between header and content in DIPs.
        /// </summary>
        private const double HeaderBottomGapDip = 12.0;

        /// <summary>
        /// Gap between footer and content in DIPs.
        /// </summary>
        private const double FooterTopGapDip = 12.0;

        /// <summary>
        /// Vertical non-thumbnail overhead per card (padding, header, gaps) in DIPs.
        /// </summary>
        private const double StaticCardOverheadDip = 16 + 12 + 6 + 2;

        /// <summary>
        /// Per-row safety pad in DIPs to prevent crowding.
        /// </summary>
        private const double RowSafetyPadDip = 4.0;

        /// <summary>
        /// Estimated internal horizontal margins per card (grid padding) in DIPs.
        /// </summary>
        private const double EstCardInnerMarginDip = 24.0;

        /// <summary>
        /// Estimated external horizontal margins per card (item/border) in DIPs.
        /// </summary>
        private const double EstCardOuterMarginDip = 16.0;

        /// <summary>
        /// Estimated minimum card width in DIPs.
        /// </summary>
        private const double EstMinCardWidthDip = 80.0;

        /// <summary>
        /// Estimated maximum card width in DIPs.
        /// </summary>
        private const double EstMaxCardWidthDip = 420.0;

        /// <summary>
        /// Minimum number of rows to target for layout.
        /// </summary>
        private const int MinRows = 3;

        /// <summary>
        /// Maximum number of rows to target for layout.
        /// </summary>
        private const int MaxRows = 4;

        /// <summary>
        /// Factory that creates <see cref="IThumbnailSurface"/> instances for the overlay HWND.
        /// </summary>
        private readonly IDwmThumbnailSurfaceFactory factory;

        /// <summary>
        /// The DWM thumbnail surface used to register and update thumbnails.
        /// </summary>
        private IThumbnailSurface? surface;

        /// <summary>
        /// Effective X DPI scale of the host window.
        /// </summary>
        private double dpiX = 1.0;

        /// <summary>
        /// Effective Y DPI scale of the host window.
        /// </summary>
        private double dpiY = 1.0;

        /// <summary>
        /// Whether the overlay is currently showing a single-item preview.
        /// </summary>
        private bool previewMode;

        /// <summary>
        /// Debounce timer to coalesce frequent layout/scroll updates.
        /// </summary>
        private readonly DispatcherTimer debounceTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };

        /// <summary>
        /// Currently hooked cards collection for change notifications.
        /// </summary>
        private ObservableCollection<WindowCardViewModel>? cardsHooked;

        /// <summary>
        /// Set of window keys (HWND) currently painted with alpha &gt; 0.
        /// </summary>
        private readonly HashSet<long> _alphaOn = new();

        /// <summary>
        /// Cache of last painted rectangles per window key, in surface pixels.
        /// </summary>
        private readonly Dictionary<long, RectPx> _lastRectByKey = new();

        /// <summary>
        /// Indicates that initial reveal is pending until a stable viewport is detected.
        /// </summary>
        private bool _initialRevealPending = true;

        /// <summary>
        /// Suppresses painting until the first stable layout is observed.
        /// </summary>
        private bool suppressUntilStableLayout = true;

        /// <summary>
        /// Target row count chosen by the dynamic estimator.
        /// </summary>
        private int _targetRows = 4;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerGrabOverlayView"/> class.
        /// Sets up window placement, disables mouse interaction, wires events,
        /// initializes DPI, and prepares the DWM surface.
        /// </summary>
        /// <param name="factory">Factory used to create the DWM thumbnail surface.</param>
        public PowerGrabOverlayView(IDwmThumbnailSurfaceFactory factory)
        {
            this.InitializeComponent();
            this.factory = factory;

            this.Opacity = 0;
            this.WindowStartupLocation = WindowStartupLocation.Manual;

            var dpi = VisualTreeHelper.GetDpi(this);
            this.dpiX = dpi.DpiScaleX; this.dpiY = dpi.DpiScaleY;

            // Disable all mouse interactivity.
            this.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler((s, e) => e.Handled = true), true);
            this.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler((s, e) => e.Handled = true), true);
            this.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler((s, e) => e.Handled = true), true);
            this.AddHandler(Mouse.MouseWheelEvent, new MouseWheelEventHandler((s, e) => e.Handled = true), true);
            this.AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler((s, e) => e.Handled = true), true);
            this.Cursor = Cursors.None;

            this.SourceInitialized += (_, __) =>
            {
                this.ApplyPrimaryWorkAreaPlacement();

                var hwnd = new WindowInteropHelper(this).Handle;
                try { this.surface = factory.Create(hwnd); }
                catch (Exception ex) { Debug.WriteLine("[PowerGrabOverlayView] Create surface FAILED: " + ex.Message); }

                // Start fully hidden to avoid mini-thumbnails flash.
                this.HideAllThumbnails();
            };

            this.Loaded += (_, __) =>
            {
                this.HookCardsCollection();
                this.AttachTabEventHandlers();

                this.UpdateLayout();
                this.ReestimateRowsAndApply();   // choose rows and set heights
                this.HideAllThumbnails();        // keep hidden until first stable reveal

                this.Root.Focus();
            };

            this.SizeChanged += (_, __) => this.DebouncedRefresh();
            this.CardsItems.LayoutUpdated += this.OnAnyLayoutUpdated;

            this.DataContextChanged += (_, __) =>
            {
                this.HookCardsCollection();
                this.AttachTabEventHandlers();
                this.UpdateLayout();
                this.ReestimateRowsAndApply();
                if (this.suppressUntilStableLayout) this.HideAllThumbnails();
                else this.RefreshAllThumbnailRects(isScrollPass: false);
            };

            // Scroll: run at Render priority and use strict "turn-off-first" logic.
            this.CardsScroll.ScrollChanged += (_, __) =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    this.RefreshAllThumbnailRects(isScrollPass: true);
                }), DispatcherPriority.Render);
            };

            this.Closed += (_, __) =>
            {
                try { this.debounceTimer.Stop(); } catch { }
                try { this.surface?.Dispose(); } catch { }
                this.surface = null;
                this.DetachTabEventHandlers();
                this._alphaOn.Clear();
                this._lastRectByKey.Clear();
            };

            // Keep preview rects fresh.
            this.CardsItems.LayoutUpdated += (_, __) =>
            {
                if (this.DataContext is PowerGrabOverlayViewModel vm && vm.IsPreviewVisible)
                {
                    this.DebouncedRefresh();
                }
            };
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handles any layout update and triggers the initial reveal once the viewport is stable.
        /// </summary>
        /// <param name="sender">Layout source.</param>
        /// <param name="e">Event args.</param>
        private void OnAnyLayoutUpdated(object? sender, EventArgs e)
        {
            if (!this._initialRevealPending) return;

            if (this.HasStableViewport())
            {
                this._initialRevealPending = false;

                this.ReestimateRowsAndApply(); // rows based on actual viewport now

                this.Dispatcher.BeginInvoke(() =>
                {
                    this.suppressUntilStableLayout = false;
                    this.RefreshAllThumbnailRects(isScrollPass: false);
                    this.Opacity = 1;
                }, DispatcherPriority.Render);
            }
        }

        /// <summary>
        /// Determines whether the viewport has measurable size and at least one visible slot has a valid size.
        /// </summary>
        /// <returns><see langword="true"/> if viewport is stable; otherwise, <see langword="false"/>.</returns>
        private bool HasStableViewport()
        {
            if (this.CardsScroll.ActualHeight <= 0 || this.CardsScroll.ActualWidth <= 0) return false;

            int n = this.CardsItems.Items.Count;
            for (int i = 0; i < n; i++)
            {
                if (this.CardsItems.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement cont)
                {
                    var slot = FindThumbnailSlot(cont);
                    if (slot is { IsVisible: true } && slot.ActualWidth > 0 && slot.ActualHeight > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Recomputes the desired row count and applies thumbnail height changes accordingly.
        /// </summary>
        private void ReestimateRowsAndApply()
        {
            if (this.DataContext is not PowerGrabOverlayViewModel vm || vm.Cards.Count == 0)
            {
                return;
            }

            // Compute viewport dimensions.
            double viewportW = this.CardsScroll.ActualWidth;
            double viewportH = this.CardsScroll.ActualHeight;

            // Fallback before first measurement.
            if (viewportW <= 0 || viewportH <= 0)
            {
                double headerH = (this.Header?.ActualHeight ?? 0) + HeaderBottomGapDip;
                double footerH = (this.Footer?.ActualHeight ?? 0) + FooterTopGapDip;
                viewportW = Math.Max(200, this.ActualWidth - 2 * WindowMarginDip);
                viewportH = Math.Max(100, this.ActualHeight - 2 * WindowMarginDip - headerH - footerH);
            }

            // Header height sample.
            double headerHeight = this.GetSampleHeaderHeightDip();
            if (headerHeight <= 0) headerHeight = 30;

            // Choose rows via quick shelf-packing simulation.
            this._targetRows = this.EstimateBestRowCount(vm, viewportW, viewportH, headerHeight);

            // Apply sizes using chosen rows.
            this.ApplyThumbHeights(vm, viewportH, headerHeight, this._targetRows);
        }

        /// <summary>
        /// Estimates an appropriate row count given viewport dimensions and a sample header height.
        /// </summary>
        /// <param name="vm">The overlay view model.</param>
        /// <param name="viewportW">Viewport width in DIPs.</param>
        /// <param name="viewportH">Viewport height in DIPs.</param>
        /// <param name="headerHeight">Measured header height in DIPs.</param>
        /// <returns>The estimated row count in the inclusive range [<see cref="MinRows"/>, <see cref="MaxRows"/>].</returns>
        private int EstimateBestRowCount(PowerGrabOverlayViewModel vm, double viewportW, double viewportH, double headerHeight)
        {
            int n = vm.Cards.Count;
            if (n <= 10) return MinRows;

            // Gather aspect ratios (w/h). Use source sizes if known; else default 16:9.
            var aspects = vm.Cards.Select(c =>
            {
                var s = c.Entry.SourceClientSize;
                return (s is not null && s.Value.H > 0) ? (double)s.Value.W / s.Value.H : 16.0 / 9.0;
            }).ToArray();

            // Try pick the smallest R where the greedy packing uses <= R rows.
            for (int rows = MinRows; rows <= MaxRows; rows++)
            {
                double thumbH = Math.Max(48.0, (viewportH / rows) - (StaticCardOverheadDip + headerHeight + RowSafetyPadDip));
                if (thumbH < 48.0)
                {
                    continue; // too cramped; try more rows
                }

                int usedRows = this.SimulateShelfPacking(aspects, thumbH, viewportW);
                if (usedRows <= rows)
                {
                    return rows;
                }
            }

            return MaxRows;
        }

        /// <summary>
        /// Greedy shelf-packing: compute per-card width from thumb height (with clamps and margins)
        /// and wrap into rows against viewport width.
        /// </summary>
        /// <param name="aspects">Array of width/height aspect ratios for cards.</param>
        /// <param name="thumbH">Proposed thumbnail height in DIPs.</param>
        /// <param name="viewportW">Viewport width in DIPs.</param>
        /// <returns>Number of rows used by the packing.</returns>
        private int SimulateShelfPacking(double[] aspects, double thumbH, double viewportW)
        {
            double rowW = 0.0;
            int rows = 1;

            double cardPadX = EstCardInnerMarginDip + EstCardOuterMarginDip;
            double maxRowWidth = Math.Max(200.0, viewportW);

            foreach (var a in aspects)
            {
                double w = thumbH * a;
                if (w < EstMinCardWidthDip) w = EstMinCardWidthDip;
                if (w > EstMaxCardWidthDip) w = EstMaxCardWidthDip;

                w += cardPadX;

                if (rowW == 0.0) { rowW = w; continue; }

                // Small tolerance to avoid over-wrapping due to rounding.
                if (rowW + w > maxRowWidth + 0.75)
                {
                    rows++;
                    rowW = w;
                }
                else
                {
                    rowW += w;
                }
            }

            return rows;
        }

        /// <summary>
        /// Applies thumbnail height to all cards based on viewport budget and chosen rows.
        /// </summary>
        /// <param name="vm">The overlay view model.</param>
        /// <param name="viewportH">Viewport height in DIPs.</param>
        /// <param name="headerHeight">Measured header height in DIPs.</param>
        /// <param name="rows">Row count to target.</param>
        private void ApplyThumbHeights(PowerGrabOverlayViewModel vm, double viewportH, double headerHeight, int rows)
        {
            double perCardOverhead = StaticCardOverheadDip + headerHeight + RowSafetyPadDip;
            double rowBudget = viewportH / Math.Max(MinRows, Math.Min(MaxRows, rows));
            double thumbH = Math.Max(48.0, rowBudget - perCardOverhead);

            foreach (var c in vm.Cards)
            {
                c.ThumbHeight = thumbH;
                var src = c.Entry.SourceClientSize ?? new SizePx(1600, 900);
                c.SetSourceSize(src);
            }

            this.InvalidateMeasure();
            this.UpdateLayout();
        }

        /// <summary>
        /// Scrolls the list by an integral number of visual rows (keyboard action).
        /// </summary>
        /// <param name="deltaRows">Number of rows to scroll; positive for down, negative for up.</param>
        private void ScrollOneRow(int deltaRows)
        {
            if (deltaRows == 0) return;

            double headerH = this.GetSampleHeaderHeightDip(); if (headerH <= 0) headerH = 30;
            double thumbH = (this.DataContext is PowerGrabOverlayViewModel vm && vm.Cards.Count > 0) ? vm.Cards[0].ThumbHeight : 0;

            // Use current row stride (header + chrome + thumb).
            double stride = headerH + StaticCardOverheadDip + thumbH;

            double target = this.CardsScroll.VerticalOffset + (deltaRows * stride);
            if (target < 0) target = 0;

            this.CardsScroll.ScrollToVerticalOffset(target);

            this.Dispatcher.BeginInvoke((() =>
            {
                this.RefreshAllThumbnailRects(isScrollPass: true);
            }), DispatcherPriority.Render);
        }

        /// <summary>
        /// Samples a card header element to determine a representative header height (in DIPs).
        /// </summary>
        /// <returns>The header height in DIPs, or 0 if not measurable.</returns>
        private double GetSampleHeaderHeightDip()
        {
            int count = this.CardsItems.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (this.CardsItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement cont)
                {
                    continue;
                }

                var header = FindHeaderBlock(cont);
                if (header is null) continue;

                if (header.IsVisible && header.ActualHeight > 0)
                {
                    return header.ActualHeight;
                }
            }
            return 0;
        }

        /// <summary>
        /// Hooks the cards collection to receive change notifications for layout refresh.
        /// </summary>
        private void HookCardsCollection()
        {
            if (this.DataContext is PowerGrabOverlayViewModel vm &&
                vm.Cards is ObservableCollection<WindowCardViewModel> coll)
            {
                if (this.cardsHooked is not null)
                {
                    this.cardsHooked.CollectionChanged -= this.OnCardsChanged;
                }

                this.cardsHooked = coll;
                this.cardsHooked.CollectionChanged += this.OnCardsChanged;
            }
        }

        /// <summary>
        /// Handles changes in the cards collection by re-estimating rows and refreshing layout.
        /// </summary>
        /// <param name="s">The sender.</param>
        /// <param name="e">Collection change data.</param>
        private void OnCardsChanged(object? s, NotifyCollectionChangedEventArgs e)
        {
            this.ReestimateRowsAndApply();

            if (this.suppressUntilStableLayout)
            {
                this.HideAllThumbnails();
            }
            else
            {
                this.DebouncedRefresh();
            }
        }

        /// <summary>
        /// Subscribes to view-model events (preview and row scroll).
        /// </summary>
        private void AttachTabEventHandlers()
        {
            if (this.DataContext is PowerGrabOverlayViewModel vm)
            {
                vm.OnShowPreview += this.EnterPreviewMode;
                vm.OnHidePreview += this.ExitPreviewMode;
                vm.OnScrollRows += this.ScrollOneRow;
            }
        }

        /// <summary>
        /// Unsubscribes from view-model events (preview and row scroll).
        /// </summary>
        private void DetachTabEventHandlers()
        {
            if (this.DataContext is PowerGrabOverlayViewModel vm)
            {
                vm.OnShowPreview -= this.EnterPreviewMode;
                vm.OnHidePreview -= this.ExitPreviewMode;
                vm.OnScrollRows -= this.ScrollOneRow;
            }
        }

        /// <summary>
        /// Restarts the short debounce timer so size/layout recalculation is batched.
        /// </summary>
        private void DebouncedRefresh()
        {
            this.debounceTimer.Stop();
            this.debounceTimer.Tick -= this.OnDebounce;
            this.debounceTimer.Tick += this.OnDebounce;
            this.debounceTimer.Start();
        }

        /// <summary>
        /// Executes the debounced refresh: re-estimates rows and updates thumbnail rectangles.
        /// </summary>
        /// <param name="s">Timer source.</param>
        /// <param name="e">Event args.</param>
        private void OnDebounce(object? s, EventArgs e)
        {
            this.debounceTimer.Stop();

            // Re-estimate rows on window resize (viewport change).
            this.ReestimateRowsAndApply();

            if (this.suppressUntilStableLayout)
            {
                this.HideAllThumbnails();
            }
            else
            {
                this.RefreshAllThumbnailRects(isScrollPass: false);
            }
        }

        /// <summary>
        /// Differentially updates DWM thumbnail rectangles with strict scroll turn-off logic.
        /// </summary>
        /// <param name="isScrollPass">If <see langword="true"/>, applies strict turn-off-first behavior.</param>
        /// <param name="defaultOpacity">The alpha to apply to visible thumbnails.</param>
        private void RefreshAllThumbnailRects(bool isScrollPass, byte defaultOpacity = 255)
        {
            if (this.surface is null) return;
            if (this.CardsItems.Items.Count == 0) return;

            if (this.suppressUntilStableLayout)
            {
                this.HideAllThumbnails();
                return;
            }

            // Preview mode: only paint preview target.
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
                            var finalDest = Intersect(destPx, rectPx);
                            this.surface.UpdateRect(entry.Id, finalDest, defaultOpacity);

                            var key = GetHwndKey(entry);
                            this._alphaOn.Add(key);
                            this._lastRectByKey[key] = finalDest;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Overlay] DWM preview update failed: {ex.Message}");
                        }
                    }
                }
                return;
            }

            // Grid mode.
            var (viewportInContent, contentPresenter) = this.GetViewportRectInContentDips();
            if (contentPresenter is null || viewportInContent.IsEmpty) return;

            var newlyVisible = new HashSet<long>();
            var toPaintVisible = new List<(WindowCardViewModel vm, RectPx rectPx, RectPx destPx, long key)>();

            int count = this.CardsItems.Items.Count;

            for (int i = 0; i < count; i++)
            {
                var item = this.CardsItems.Items[i];
                if (this.CardsItems.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container) continue;

                var slot = FindThumbnailSlot(container);
                if (slot is null) continue;
                if (item is not WindowCardViewModel cardVm) continue;

                Rect slotContentRect = this.GetElementRectInContentDips(slot, contentPresenter);
                if (slotContentRect.IsEmpty) continue;

                if (!IsFullyVisibleInViewport(slotContentRect, viewportInContent)) continue;

                long key = GetHwndKey(cardVm.Entry);
                newlyVisible.Add(key);

                try
                {
                    this.surface.EnsureRegistered(cardVm.Entry.Id);

                    if (cardVm.Entry.SourceClientSize is null &&
                        this.surface.TryGetSourceSize(cardVm.Entry.Id, out var src))
                    {
                        cardVm.Entry.SourceClientSize = src;
                        cardVm.SetSourceSize(src);
                    }

                    var rectPx = this.GetSlotRectInSurfacePixels(slot);
                    if (rectPx.W <= 0 || rectPx.H <= 0) continue;

                    var fullDestPx = Letterbox(rectPx, cardVm.Entry.SourceClientSize);
                    var finalDestPx = Intersect(fullDestPx, rectPx);

                    if (!PassesCoverageGuard(fullDestPx, finalDestPx)) continue;

                    toPaintVisible.Add((cardVm, rectPx, finalDestPx, key));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Overlay] Visible collect failed hwnd={cardVm.Entry.Id.Hwnd}: {ex.Message}");
                }
            }

            if (isScrollPass)
            {
                // Strict: turn OFF previously-on but now invisible, using cached rects.
                if (this._alphaOn.Count > 0)
                {
                    var toTurnOff = this._alphaOn.Where(k => !newlyVisible.Contains(k)).ToList();
                    foreach (var key in toTurnOff)
                    {
                        try
                        {
                            RectPx rect = this._lastRectByKey.TryGetValue(key, out var cached)
                                ? cached
                                : new RectPx(0, 0, 1, 1);

                            if (this.TryFindVmByKey(key, out var offVm))
                            {
                                this.surface.EnsureRegistered(offVm.Entry.Id);
                                this.surface.UpdateRect(offVm.Entry.Id, rect, 0);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Overlay] Turn-off(scroll) failed key={key}: {ex.Message}");
                        }
                        finally
                        {
                            this._alphaOn.Remove(key);
                            this._lastRectByKey.Remove(key);
                        }
                    }
                }

                // Paint newly visible.
                foreach (var it in toPaintVisible)
                {
                    try
                    {
                        this.surface.UpdateRect(it.vm.Entry.Id, it.destPx, defaultOpacity);
                        this._alphaOn.Add(it.key);
                        this._lastRectByKey[it.key] = it.destPx;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Overlay] Paint-visible(scroll) failed hwnd={it.vm.Entry.Id.Hwnd}: {ex.Message}");
                    }
                }
            }
            else
            {
                // Normal differential painting.
                foreach (var it in toPaintVisible)
                {
                    try
                    {
                        this.surface.UpdateRect(it.vm.Entry.Id, it.destPx, defaultOpacity);
                        this._alphaOn.Add(it.key);
                        this._lastRectByKey[it.key] = it.destPx;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Overlay] Paint-visible(normal) failed hwnd={it.vm.Entry.Id.Hwnd}: {ex.Message}");
                    }
                }

                if (this._alphaOn.Count > 0)
                {
                    var toTurnOff = this._alphaOn.Where(k => !newlyVisible.Contains(k)).ToList();
                    foreach (var key in toTurnOff)
                    {
                        try
                        {
                            if (this.TryFindVmByKey(key, out var offVm))
                            {
                                this.surface.EnsureRegistered(offVm.Entry.Id);
                                RectPx rect;
                                if (this.TryGetCurrentRectPx(offVm, out rect))
                                {
                                    this.surface.UpdateRect(offVm.Entry.Id, rect, 0);
                                }
                                else if (this._lastRectByKey.TryGetValue(key, out var cached))
                                {
                                    this.surface.UpdateRect(offVm.Entry.Id, cached, 0);
                                }
                                else
                                {
                                    this.surface.UpdateRect(offVm.Entry.Id, new RectPx(0, 0, 1, 1), 0);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Overlay] Turn-off(normal) failed key={key}: {ex.Message}");
                        }
                        finally
                        {
                            this._alphaOn.Remove(key);
                            this._lastRectByKey.Remove(key);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets all thumbnails' alpha to 0 to hide them.
        /// </summary>
        private void HideAllThumbnails()
        {
            if (this.surface is null) return;

            int count = this.CardsItems.Items.Count;
            for (int i = 0; i < count; i++)
            {
                var item = this.CardsItems.Items[i];
                if (this.CardsItems.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container) continue;
                if (item is not WindowCardViewModel vm) continue;

                var slot = FindThumbnailSlot(container);
                if (slot is null) continue;

                try
                {
                    this.surface.EnsureRegistered(vm.Entry.Id);

                    var rectPx = this.GetSlotRectInSurfacePixels(slot);
                    if (rectPx.W <= 0 || rectPx.H <= 0) rectPx = new RectPx(0, 0, 1, 1);

                    this.surface.UpdateRect(vm.Entry.Id, rectPx, 0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Overlay] HideAll failed hwnd={vm.Entry.Id.Hwnd}: {ex.Message}");
                }
            }

            this._alphaOn.Clear();
            this._lastRectByKey.Clear();
        }

        /// <summary>
        /// Returns whether the element's rectangle is fully contained by the viewport (with a small epsilon).
        /// </summary>
        /// <param name="elementInContent">Element bounds in content coordinates.</param>
        /// <param name="viewportInContent">Viewport bounds in content coordinates.</param>
        /// <returns><see langword="true"/> if fully visible; otherwise, <see langword="false"/>.</returns>
        private static bool IsFullyVisibleInViewport(Rect elementInContent, Rect viewportInContent)
        {
            const double eps = 0.75; // DIP tolerance
            var tightened = new Rect(
                viewportInContent.X + eps,
                viewportInContent.Y + eps,
                Math.Max(0, viewportInContent.Width - 2 * eps),
                Math.Max(0, viewportInContent.Height - 2 * eps));
            return tightened.Contains(elementInContent);
        }

        /// <summary>
        /// Ensures the actually painted destination closely matches the expected size to avoid tiny slivers.
        /// </summary>
        /// <param name="expectedFullDest">Expected fully letterboxed destination rectangle.</param>
        /// <param name="actualDest">Actual destination rectangle used.</param>
        /// <returns><see langword="true"/> if coverage is sufficient; otherwise, <see langword="false"/>.</returns>
        private static bool PassesCoverageGuard(RectPx expectedFullDest, RectPx actualDest)
        {
            if (expectedFullDest.W <= 0 || expectedFullDest.H <= 0) return true;
            const double minRatio = 0.92; // Require >=92% of expected size
            bool okW = actualDest.W >= expectedFullDest.W * minRatio;
            bool okH = actualDest.H >= expectedFullDest.H * minRatio;
            return okW && okH;
        }

        /// <summary>
        /// Attempts to find a view model by its HWND key.
        /// </summary>
        /// <param name="key">The HWND key.</param>
        /// <param name="vmOut">The found view model, if any.</param>
        /// <returns><see langword="true"/> if found; otherwise, <see langword="false"/>.</returns>
        private bool TryFindVmByKey(long key, out WindowCardViewModel vmOut)
        {
            int count = this.CardsItems.Items.Count;
            for (int i = 0; i < count; i++)
            {
                var item = this.CardsItems.Items[i];
                if (item is WindowCardViewModel vm)
                {
                    if (GetHwndKey(vm.Entry) == key) { vmOut = vm; return true; }
                }
            }
            vmOut = null!;
            return false;
        }

        /// <summary>
        /// Attempts to compute the current surface-pixel rectangle for the given view model's slot.
        /// </summary>
        /// <param name="vm">The view model.</param>
        /// <param name="rectPx">Resulting rectangle if successful.</param>
        /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
        private bool TryGetCurrentRectPx(WindowCardViewModel vm, out RectPx rectPx)
        {
            rectPx = default;
            if (this.CardsItems.ItemContainerGenerator.ContainerFromItem(vm) is not FrameworkElement container) return false;
            var slot = FindThumbnailSlot(container);
            if (slot is null) return false;

            rectPx = this.GetSlotRectInSurfacePixels(slot);
            if (rectPx.W <= 0 || rectPx.H <= 0) return false;
            return true;
        }

        /// <summary>
        /// Computes the viewport rectangle in content coordinates and returns the content presenter.
        /// </summary>
        /// <returns>Tuple of viewport rectangle and its presenter; rectangle may be empty.</returns>
        private (Rect viewport, FrameworkElement? presenter) GetViewportRectInContentDips()
        {
            var presenter = FindScrollContentPresenter(this.CardsScroll) as FrameworkElement;
            if (presenter is null || !presenter.IsVisible) return (Rect.Empty, null);

            double w = this.CardsScroll.ViewportWidth > 0 ? this.CardsScroll.ViewportWidth : presenter.ActualWidth;
            double h = this.CardsScroll.ViewportHeight > 0 ? this.CardsScroll.ViewportHeight : presenter.ActualHeight;

            const double pad = 0.0; // set to 6-8 to add hysteresis if desired
            return (new Rect(0, -pad, w, h + 2 * pad), presenter);
        }

        /// <summary>
        /// Finds the <see cref="ScrollContentPresenter"/> under a given visual tree node.
        /// </summary>
        /// <param name="start">Starting node.</param>
        /// <returns>The found presenter, or <see langword="null"/>.</returns>
        private static DependencyObject? FindScrollContentPresenter(DependencyObject start)
        {
            int n = VisualTreeHelper.GetChildrenCount(start);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                if (child is ScrollContentPresenter) return child;
                var deeper = FindScrollContentPresenter(child);
                if (deeper is not null) return deeper;
            }
            return null;
        }

        /// <summary>
        /// Transforms an element's rectangle into the content presenter's coordinate space.
        /// </summary>
        /// <param name="elt">Element to measure.</param>
        /// <param name="contentPresenter">Content presenter ancestor.</param>
        /// <returns>Element bounds in content coordinates, or <see cref="Rect.Empty"/> on failure.</returns>
        private Rect GetElementRectInContentDips(FrameworkElement elt, FrameworkElement contentPresenter)
        {
            if (elt is null || !elt.IsVisible || elt.ActualWidth <= 0 || elt.ActualHeight <= 0)
            {
                return Rect.Empty;
            }

            var local = new Rect(0, 0, elt.ActualWidth, elt.ActualHeight);
            try
            {
                return elt.TransformToAncestor(contentPresenter).TransformBounds(local);
            }
            catch
            {
                return Rect.Empty;
            }
        }

        /// <summary>
        /// Finds the thumbnail slot inside an item container by the known element name.
        /// </summary>
        /// <param name="container">Item container.</param>
        /// <returns>The slot element, or <see langword="null"/>.</returns>
        private static FrameworkElement? FindThumbnailSlot(FrameworkElement container)
        {
            if (container is ContentPresenter cp)
            {
                cp.ApplyTemplate();
                if (cp.ContentTemplate is not null)
                {
                    if (cp.ContentTemplate.FindName("ThumbnailSlot", cp) is FrameworkElement elt) return elt;
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
        /// Finds the header block inside an item container by the known element name.
        /// </summary>
        /// <param name="container">Item container.</param>
        /// <returns>The header element, or <see langword="null"/>.</returns>
        private static FrameworkElement? FindHeaderBlock(FrameworkElement container)
        {
            if (container is ContentPresenter cp)
            {
                cp.ApplyTemplate();
                if (cp.ContentTemplate is not null)
                {
                    if (cp.ContentTemplate.FindName("HeaderBlock", cp) is FrameworkElement elt) return elt;
                }
                if (VisualTreeHelper.GetChildrenCount(cp) > 0 &&
                    VisualTreeHelper.GetChild(cp, 0) is FrameworkElement root)
                {
                    var deep = FindDescendantByName(root, "HeaderBlock");
                    if (deep is not null) return deep;
                }
            }
            return FindDescendantByName(container, "HeaderBlock");
        }

        /// <summary>
        /// Performs a depth-first search for a descendant element by name.
        /// </summary>
        /// <param name="start">Starting node.</param>
        /// <param name="name">Element name to find.</param>
        /// <returns>The found element, or <see langword="null"/>.</returns>
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
        /// <returns>The preview slot element if found; otherwise, <see langword="null"/>.</returns>
        private FrameworkElement? FindPreviewSlot() => FindDescendantByName(this.Root, "PreviewThumbnailSlot");

        /// <summary>
        /// Gets the rectangle of a slot relative to this host, clamped to the host bounds and converted to surface pixels.
        /// </summary>
        /// <param name="slot">The slot element.</param>
        /// <returns>The clamped rectangle in surface pixels; empty if invalid.</returns>
        private RectPx GetSlotRectInSurfacePixels(FrameworkElement slot)
        {
            if (slot == null || !slot.IsVisible ||
                double.IsNaN(slot.ActualWidth) || double.IsNaN(slot.ActualHeight) ||
                slot.ActualWidth <= 0 || slot.ActualHeight <= 0)
            {
                return new RectPx(0, 0, 0, 0);
            }

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
        /// Computes a letterboxed destination rectangle inside <paramref name="dest"/> that preserves the aspect ratio of <paramref name="src"/>.
        /// </summary>
        /// <param name="dest">The destination rectangle.</param>
        /// <param name="src">The source size in pixels.</param>
        /// <returns>The letterboxed destination rectangle.</returns>
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
        /// <param name="a">The first rectangle.</param>
        /// <param name="b">The second rectangle.</param>
        /// <returns>The intersection rectangle. If disjoint, width/height will be zero.</returns>
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
        /// Enters preview mode by hiding grid thumbnails (except preview) and forcing a refresh.
        /// </summary>
        private void EnterPreviewMode()
        {
            if (this.previewMode) return;
            this.previewMode = true;
            this.HideGridThumbnailsExceptPreview();
            this.UpdateLayout();
            this.RefreshAllThumbnailRects(isScrollPass: false);
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
            this.RefreshAllThumbnailRects(isScrollPass: false);
        }

        /// <summary>
        /// Hides all DWM thumbnails in the grid except for the current preview target, if any.
        /// </summary>
        private void HideGridThumbnailsExceptPreview()
        {
            if (this.surface is null) return;
            if (this.DataContext is not PowerGrabOverlayViewModel vm) return;

            var previewHwnd = vm.PreviewTarget?.Entry.Id;

            int n = this.CardsItems.Items.Count;
            for (int i = 0; i < n; i++)
            {
                if (this.CardsItems.Items[i] is not WindowCardViewModel cardVm) continue;
                if (previewHwnd is not null && cardVm.Entry.Id.Equals(previewHwnd.Value)) continue;

                if (this.CardsItems.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement cont) continue;
                var slot = FindThumbnailSlot(cont);
                if (slot is null) continue;

                try
                {
                    this.surface.EnsureRegistered(cardVm.Entry.Id);
                    var rectPx = this.GetSlotRectInSurfacePixels(slot);
                    if (rectPx.W <= 0 || rectPx.H <= 0) rectPx = new RectPx(0, 0, 1, 1);
                    this.surface.UpdateRect(cardVm.Entry.Id, rectPx, 0);

                    var key = GetHwndKey(cardVm.Entry);
                    this._alphaOn.Remove(key);
                    this._lastRectByKey.Remove(key);
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
            this.RefreshAllThumbnailRects(isScrollPass: false);
        }

        /// <summary>
        /// Gets a stable long key from a window entry's HWND.
        /// </summary>
        /// <param name="entry">The window entry.</param>
        /// <returns>HWND as a 64-bit integer.</returns>
        private static long GetHwndKey(WindowEntry entry) => entry.Id.Hwnd.ToInt64();

        /// <summary>
        /// Retrieves the primary monitor's work area in pixels.
        /// </summary>
        /// <returns>A <see cref="RECT"/> describing the primary work area.</returns>
        private static RECT GetPrimaryWorkAreaPx()
        {
            var hMon = MonitorFromPoint(new POINT(0, 0), MONITOR_DEFAULTTOPRIMARY);
            var mi = new MONITORINFO();
            if (!GetMonitorInfo(hMon, mi)) throw new System.ComponentModel.Win32Exception();
            return mi.rcWork;
        }

        /// <summary>
        /// Places this window to cover the primary work area.
        /// </summary>
        private void ApplyPrimaryWorkAreaPlacement()
        {
            var wa = GetPrimaryWorkAreaPx(); // pixels
            var hwnd = new WindowInteropHelper(this).Handle;
            int x = wa.Left, y = wa.Top, cx = wa.Right - wa.Left, cy = wa.Bottom - wa.Top;
            SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SWP.NOZORDER | SWP.NOACTIVATE | SWP.NOSENDCHANGING | SWP.SHOWWINDOW);
        }

        #region Interop

        /// <summary>
        /// SetWindowPos flags used when positioning the overlay window.
        /// </summary>
        [Flags]
        private enum SWP : uint
        {
            /// <summary>Retains the current size (ignores cx and cy).</summary>
            NOSIZE = 0x0001,

            /// <summary>Retains the current position (ignores X and Y).</summary>
            NOMOVE = 0x0002,

            /// <summary>Retains the current Z order.</summary>
            NOZORDER = 0x0004,

            /// <summary>Does not redraw changes.</summary>
            NOREDRAW = 0x0008,

            /// <summary>Does not activate the window.</summary>
            NOACTIVATE = 0x0010,

            /// <summary>Applies a new frame style.</summary>
            FRAMECHANGED = 0x0020,

            /// <summary>Displays the window.</summary>
            SHOWWINDOW = 0x0040,

            /// <summary>Does not send the WM_WINDOWPOSCHANGING message.</summary>
            NOSENDCHANGING = 0x0400,

            /// <summary>Applies the operation asynchronously.</summary>
            ASYNCWINDOWPOS = 0x4000,
        }

        /// <summary>
        /// Win32 POINT structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            /// <summary>X coordinate.</summary>
            public int X;

            /// <summary>Y coordinate.</summary>
            public int Y;

            /// <summary>
            /// Initializes a new instance of the <see cref="POINT"/> struct.
            /// </summary>
            /// <param name="x">X coordinate.</param>
            /// <param name="y">Y coordinate.</param>
            public POINT(int x, int y) { this.X = x; this.Y = y; }
        }

        /// <summary>
        /// Win32 RECT structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            /// <summary>Left.</summary>
            public int Left;

            /// <summary>Top.</summary>
            public int Top;

            /// <summary>Right.</summary>
            public int Right;

            /// <summary>Bottom.</summary>
            public int Bottom;
        }

        /// <summary>
        /// Win32 MONITORINFO structure for retrieving monitor details.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            /// <summary>Structure size in bytes.</summary>
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));

            /// <summary>Monitor rectangle in pixels.</summary>
            public RECT rcMonitor;

            /// <summary>Work area rectangle in pixels.</summary>
            public RECT rcWork;

            /// <summary>Flags (e.g., primary monitor).</summary>
            public int dwFlags;
        }

        /// <summary>
        /// MonitorFromPoint default: return primary monitor.
        /// </summary>
        private const int MONITOR_DEFAULTTOPRIMARY = 1;

        /// <summary>
        /// MONITORINFO flag indicating primary monitor.
        /// </summary>
        private const int MONITORINFOF_PRIMARY = 1;

        /// <summary>
        /// Sets the size, position, and Z order of a window.
        /// </summary>
        /// <param name="hWnd">Window handle.</param>
        /// <param name="hWndInsertAfter">Placement handle.</param>
        /// <param name="X">Left position.</param>
        /// <param name="Y">Top position.</param>
        /// <param name="cx">Width.</param>
        /// <param name="cy">Height.</param>
        /// <param name="uFlags">Operation flags.</param>
        /// <returns><see langword="true"/> on success.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SWP uFlags);

        /// <summary>
        /// Retrieves a handle to the display monitor that contains a specified point.
        /// </summary>
        /// <param name="pt">A <see cref="POINT"/> in virtual-screen coordinates.</param>
        /// <param name="dwFlags">Behavior flags.</param>
        /// <returns>Monitor handle.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

        /// <summary>
        /// Retrieves information about a display monitor.
        /// </summary>
        /// <param name="hMonitor">Monitor handle.</param>
        /// <param name="lpmi">Receives the monitor info.</param>
        /// <returns><see langword="true"/> on success.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        #endregion

        #endregion
    }
}

