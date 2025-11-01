namespace VoilaTile.Settings.Enumerations
{
    /// <summary>
    /// Represents the current interaction state of the layout editor.
    /// </summary>
    public enum LayoutEditorState
    {
        /// <summary>
        /// The user is currently dragging an existing divider.
        /// </summary>
        DraggingDivider,

        /// <summary>
        /// The user is in the process of placing a new divider.
        /// </summary>
        PlacingDivider,

        /// <summary>
        /// The user is hovering over a handle.
        /// </summary>
        HoveringHandle,

        /// <summary>
        /// The user is hovering over a card.
        /// </summary>
        HoveringCard,
    }
}
