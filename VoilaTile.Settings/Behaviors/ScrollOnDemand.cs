namespace VoilaTile.Settings.Behaviors
{
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Scrolls a <see cref="ScrollViewer"/> on demand when the bound trigger changes.
    /// Bind a monotonically increasing integer from the ViewModel and increment it to request a scroll.
    /// </summary>
    public static class ScrollOnDemand
    {
        #region Attached Properties

        /// <summary>
        /// Identifies the Trigger attached property. Increment to request a scroll.
        /// </summary>
        public static readonly DependencyProperty TriggerProperty =
            DependencyProperty.RegisterAttached(
                "Trigger",
                typeof(long),
                typeof(ScrollOnDemand),
                new PropertyMetadata(0L, OnTriggerChanged));

        /// <summary>
        /// Identifies the Direction attached property. Controls which edge to scroll to.
        /// </summary>
        public static readonly DependencyProperty DirectionProperty =
            DependencyProperty.RegisterAttached(
                "Direction",
                typeof(ScrollDirection),
                typeof(ScrollOnDemand),
                new PropertyMetadata(ScrollDirection.Bottom));

        #endregion

        #region Get/Set

        /// <summary>
        /// Gets the trigger value.
        /// </summary>
        public static long GetTrigger(DependencyObject obj) => (long)obj.GetValue(TriggerProperty);

        /// <summary>
        /// Sets the trigger value.
        /// </summary>
        public static void SetTrigger(DependencyObject obj, long value) => obj.SetValue(TriggerProperty, value);

        /// <summary>
        /// Gets the scroll direction.
        /// </summary>
        public static ScrollDirection GetDirection(DependencyObject obj) => (ScrollDirection)obj.GetValue(DirectionProperty);

        /// <summary>
        /// Sets the scroll direction.
        /// </summary>
        public static void SetDirection(DependencyObject obj, ScrollDirection value) => obj.SetValue(DirectionProperty, value);

        #endregion

        #region Handlers

        /// <summary>
        /// Called when the trigger changes; performs the requested scroll.
        /// </summary>
        private static void OnTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer sv)
            {
                return;
            }

            // Force layout to ensure extent is current after items were added/refreshed.
            sv.UpdateLayout();

            switch (GetDirection(sv))
            {
                case ScrollDirection.Top:
                    sv.ScrollToHome();
                    break;
                case ScrollDirection.Bottom:
                    sv.ScrollToBottom();
                    break;
                case ScrollDirection.Left:
                    sv.ScrollToLeftEnd();
                    break;
                case ScrollDirection.Right:
                    sv.ScrollToRightEnd();
                    break;
                default:
                    sv.ScrollToBottom();
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Directions supported by <see cref="ScrollOnDemand"/>.
    /// </summary>
    public enum ScrollDirection
    {
        /// <summary>
        /// Scroll to top/left origin vertically.
        /// </summary>
        Top,

        /// <summary>
        /// Scroll to bottom (vertical end).
        /// </summary>
        Bottom,

        /// <summary>
        /// Scroll to left (horizontal origin).
        /// </summary>
        Left,

        /// <summary>
        /// Scroll to right (horizontal end).
        /// </summary>
        Right,
    }
}

