using VoilaTile.Snapper.Interop;
using VoilaTile.Snapper.Records;
using VoilaTile.Snapper.Services;

internal sealed class WindowEnumerator : IWindowEnumerator
{
    private static readonly HashSet<string> ExcludedClasses = new(StringComparer.Ordinal)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "DV2ControlHost",
    };

    public IReadOnlyList<WindowEntry> Snapshot(WindowQueryOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        var list = new List<WindowEntry>(64);

        Win32.EnumWindows((hWnd, lParam) =>
        {
            try
            {
                if (!Win32.IsWindow(hWnd))
                    return true;

                // root only
                var root = Win32.GetAncestor(hWnd, Win32.GA_ROOT);
                if (root != hWnd)
                    return true;

                // visibility
                bool isVisible = Win32.IsWindowVisible(hWnd);
                if (!isVisible)
                    return true;

                bool isMin = Win32.IsIconic(hWnd);
                if (isMin && !options.IncludeMinimized)
                    return true;

                // cloaked
                bool isCloaked = Win32.IsCloaked(hWnd);
                if (isCloaked && options.ExcludeCloaked)
                    return true;

                // class
                string className = Win32.GetClassNameSafe(hWnd);
                if (ExcludedClasses.Contains(className))
                    return true;

                // styles
                int exStyle = (int)Win32.GetWindowLongPtr(hWnd, Win32.GWL_EXSTYLE);
                bool isTool = (exStyle & Win32.WS_EX_TOOLWINDOW) != 0;
                bool isApp = (exStyle & Win32.WS_EX_APPWINDOW) != 0;

                // owner chain
                IntPtr owner = Win32.GetWindow(hWnd, Win32.GW_OWNER);

                // Alt-Tab semantics (owned window suppression & no tool windows)
                if (options.AltTabOnly)
                {
                    // In Alt-Tab: tool windows are out
                    if (isTool) return true;

                    // Owned windows are suppressed unless WS_EX_APPWINDOW is set
                    if (owner != IntPtr.Zero && !isApp)
                        return true;
                }
                else
                {
                    // Non-AltTab mode: still obey IncludeToolWindows
                    if (isTool && !options.IncludeToolWindows)
                        return true;
                }

                // bounds (extended frame)
                if (!Win32.TryGetExtendedFrameBounds(hWnd, out var r) || r.IsEmpty)
                    return true;

                int width = r.right - r.left;
                int height = r.bottom - r.top;
                if (width < options.MinWidth || height < options.MinHeight)
                    return true;

                // process
                _ = Win32.GetWindowThreadProcessId(hWnd, out uint pid);
                string processName = "unknown";
                try
                {
                    var path = Win32.TryGetProcessPath(pid);
                    if (!string.IsNullOrEmpty(path))
                        processName = System.IO.Path.GetFileNameWithoutExtension(path);
                }
                catch { /* ignore */ }

                string title = Win32.GetWindowTextSafe(hWnd) ?? string.Empty;

                var entry = new WindowEntry(
                    new WindowId(hWnd),
                    title,
                    processName,
                    className,
                    isTool,
                    isVisible,
                    isMin,
                    isCloaked,
                    owner,
                    new RectPx(r.left, r.top, width, height),
                    appIcon: null,
                    sourceClientSize: null
                );

                list.Add(entry);
            }
            catch
            {
                // skip problematic windows silently
            }

            return true;
        }, IntPtr.Zero);

        return list;
    }
}

