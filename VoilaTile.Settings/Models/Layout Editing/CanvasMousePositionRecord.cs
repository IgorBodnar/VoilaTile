namespace VoilaTile.Settings.Models
{
    using System.Windows;

    /// <summary>
    /// The position of mouse on the canvas.
    /// </summary>
    /// <param name="AbsoluteMousePosition">The absolute mouse position on the canvas.</param>
    /// <param name="CanvasWidth">The canvas width for relative calculations.</param>
    /// <param name="CanvasHeight">The cancas height for relative calculations.</param>
    public record CanvasMousePositionRecord(Point AbsoluteMousePosition, double CanvasWidth, double CanvasHeight)
    {
        /// <summary>
        /// Gets the normalized (relative) mouse position.
        /// </summary>
        public Point NormalizedMousePositon => new Point(AbsoluteMousePosition.X / CanvasWidth, AbsoluteMousePosition.Y / CanvasHeight);
    }
}
