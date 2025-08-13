namespace VoilaTile.Snapper.Layout
{
    /// <summary>
    /// Represents a resolved tile in absolute screen coordinates.
    /// </summary>
    public class ResolvedTileModel
    {
        public string Hint { get; }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double ScreenX { get; }
        public double ScreenY { get; }
        public uint DpiX { get; }
        public uint DpiY { get; }

        public ResolvedTileModel(string hint, double x, double y, double width, double height, double screenX, double screenY, uint dpiX, uint dpiY)
        {
            Hint = hint;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ScreenX = screenX;
            ScreenY = screenY;
            DpiX = dpiX;
            DpiY = dpiY;
        }
    }
}

