namespace VoilaTile.Snapper.Records
{
    using System;

    /// <summary>
    /// Strongly-typed window identifier wrapping a native HWND.
    /// </summary>
    public readonly record struct WindowId(IntPtr Hwnd);
}
