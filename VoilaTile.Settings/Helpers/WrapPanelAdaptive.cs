namespace VoilaTile.Settings.Helpers
{
    using System;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Adapts a WrapPanel's ItemWidth to fit an integer number of columns,
    /// based on the available width and a target (ideal) card width.
    /// </summary>
    public static class WrapPanelAdaptive
    {
        /// <summary>
        /// The ideal width for each item in the WrapPanel.
        /// </summary>
        public static readonly DependencyProperty IdealItemWidthProperty =
            DependencyProperty.RegisterAttached(
                "IdealItemWidth",
                typeof(double),
                typeof(WrapPanelAdaptive),
                new PropertyMetadata(260.0, OnParamsChanged));

        /// <summary>
        /// The minimum width for each item in the WrapPanel.
        /// </summary>
        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.RegisterAttached(
                "MinItemWidth",
                typeof(double),
                typeof(WrapPanelAdaptive),
                new PropertyMetadata(180.0, OnParamsChanged));

        /// <summary>
        /// The spacing between items in the WrapPanel.
        /// </summary>
        public static readonly DependencyProperty ItemSpacingProperty =
            DependencyProperty.RegisterAttached(
                "ItemSpacing",
                typeof(double),
                typeof(WrapPanelAdaptive),
                new PropertyMetadata(10.0, OnParamsChanged));

        /// <summary>
        /// Gets the ideal width for each item in the WrapPanel.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <returns>The value representing the ideal width for each item in the WrapPanel.</returns>
        public static double GetIdealItemWidth(DependencyObject d) => (double)d.GetValue(IdealItemWidthProperty);

        /// <summary>
        /// Sets the ideal width for each item in the WrapPanel.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <param name="value">The ideal width for each item in the WrapPanel.</param>
        public static void SetIdealItemWidth(DependencyObject d, double value) => d.SetValue(IdealItemWidthProperty, value);

        /// <summary>
        /// Gets the minimum width for each item in the WrapPanel.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <returns>The value representing the minimum width for each item in the WrapPanel.</returns>
        public static double GetMinItemWidth(DependencyObject d) => (double)d.GetValue(MinItemWidthProperty);

        /// <summary>
        /// Sets the minimum width for each item in the WrapPanel.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <param name="value">The minimum width for each item in the WrapPanel.</param>
        public static void SetMinItemWidth(DependencyObject d, double value) => d.SetValue(MinItemWidthProperty, value);

        /// <summary>
        /// Gets the spacing between items in the WrapPanel.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <returns>The value representing the item spacing in the WrapPanel.</returns>
        public static double GetItemSpacing(DependencyObject d) => (double)d.GetValue(ItemSpacingProperty);

        /// <summary>
        /// Sets the spacing between items in the WrapPanel.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <param name="value">The spacing between items in the WrapPanel.</param>
        public static void SetItemSpacing(DependencyObject d, double value) => d.SetValue(ItemSpacingProperty, value);

        private static void OnParamsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ItemsControl ic)
            {
                ic.Loaded -= HandleLoaded;
                ic.Loaded += HandleLoaded;

                ic.SizeChanged -= HandleSizeChanged;
                ic.SizeChanged += HandleSizeChanged;
            }
        }

        private static void HandleLoaded(object sender, RoutedEventArgs e) => Recalc(sender as ItemsControl);
        private static void HandleSizeChanged(object sender, SizeChangedEventArgs e) => Recalc(sender as ItemsControl);

        private static void Recalc(ItemsControl? ic)
        {
            if (ic == null) return;

            // Find the WrapPanel inside the ItemsControl
            if (VisualTreeHelperEx.FindDescendant<WrapPanel>(ic) is not WrapPanel panel)
                return;

            // Available width = ItemsControl's actual width minus vertical scrollbar gutter if present
            double available = ic.ActualWidth;
            if (available <= 0) return;

            double ideal = GetIdealItemWidth(ic);
            double min = GetMinItemWidth(ic);
            double gap = GetItemSpacing(ic);

            // Compute how many columns we can fit at ~ideal width.
            // Add gap per column except the last.
            int cols = Math.Max(1, (int)Math.Floor((available + gap) / (ideal + gap)));

            // Recompute exact width to perfectly fill the row
            double width = (available - (cols - 1) * gap) / cols;
            if (width < min)
            {
                cols = Math.Max(1, (int)Math.Floor((available + gap) / (min + gap)));
                width = (available - (cols - 1) * gap) / cols;
                width = Math.Max(width, min);
            }

            panel.ItemWidth = width;
            panel.ItemHeight = double.NaN; // let height flow
        }

        private static class VisualTreeHelperEx
        {
            public static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
            {
                if (root is T t) return t;
                int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
                for (int i = 0; i < count; i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                    var result = FindDescendant<T>(child);
                    if (result != null) return result;
                }
                return null;
            }
        }
    }
}

