namespace VoilaTile.Snapper.Records
{
    /// <summary>
    /// Snapshot of a top-level window's metadata used by Power Mode.
    /// </summary>
    public sealed record WindowEntry
    {
        public WindowId Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string ProcessName { get; init; } = string.Empty;
        public string ClassName { get; init; } = string.Empty;
        public bool IsToolWindow { get; init; }
        public bool IsVisible { get; init; }
        public bool IsMinimized { get; init; }
        public bool IsCloaked { get; init; }
        public IntPtr OwnerHwnd { get; init; }
        public RectPx BoundsPx { get; init; }
        public IconSource? AppIcon { get; init; }

        // Mutable: we fill this after DWM registration/query
        public SizePx? SourceClientSize { get; set; }

        public WindowEntry(
            WindowId id,
            string title,
            string processName,
            string className,
            bool isToolWindow,
            bool isVisible,
            bool isMinimized,
            bool isCloaked,
            IntPtr ownerHwnd,
            RectPx boundsPx,
            IconSource? appIcon,
            SizePx? sourceClientSize)
        {
            Id = id;
            Title = title ?? string.Empty;
            ProcessName = processName ?? string.Empty;
            ClassName = className ?? string.Empty;
            IsToolWindow = isToolWindow;
            IsVisible = isVisible;
            IsMinimized = isMinimized;
            IsCloaked = isCloaked;
            OwnerHwnd = ownerHwnd;
            BoundsPx = boundsPx;
            AppIcon = appIcon;
            SourceClientSize = sourceClientSize;
        }
    }
}

