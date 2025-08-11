namespace VoilaTile.Configurator.Records
{
    using System.Windows;

    public record CanvasMousePositionRecord(Point AbsoluteMousePosition, double CanvasWidth, double CanvasHeight)
    {
        public Point NormalizedMousePositon => new Point(AbsoluteMousePosition.X / CanvasWidth, AbsoluteMousePosition.Y / CanvasHeight);
    }
}
