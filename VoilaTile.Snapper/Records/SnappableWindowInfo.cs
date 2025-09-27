namespace VoilaTile.Snapper.Records
{
    using System;

    /// <summary>
    /// Describes a foreground window that can be repositioned.
    /// </summary>
    public record SnappableWindowInfo(
        IntPtr Hwnd,
        int X,
        int Y,
        int Width,
        int Height);
}

