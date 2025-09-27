namespace VoilaTile.Snapper.Records
{
    /// <summary>
    /// Snapshot of a top-level window's metadata used by Power Mode.
    /// </summary>
    public sealed record WindowEntry(
        WindowId Id,
        string Title,
        string ProcessName,
        string ClassName,
        bool IsToolWindow,
        bool IsVisible,
        bool IsMinimized,
        bool IsCloaked,
        RectPx BoundsPx,
        IconSource? AppIcon,
        SizePx? SourceClientSize);
}
