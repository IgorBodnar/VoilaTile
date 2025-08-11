namespace VoilaTile.Configurator.Helpers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Helpers for detecting and starting the Snapper process.
    /// </summary>
    public static class SnapperProcessHelper
    {
        /// <summary>
        /// Gets the executable file name for the Snapper process (without extension for GetProcessesByName).
        /// </summary>
        public const string SnapperProcessName = "VoilaTile.Snapper";

        /// <summary>
        /// Determines whether the Snapper process is currently running.
        /// </summary>
        /// <returns>True if running; otherwise false.</returns>
        public static bool IsSnapperRunning()
        {
            try
            {
                return Process.GetProcessesByName(SnapperProcessName).Any();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to find a plausible path for the Snapper executable.
        /// Prefers the current app directory (installer places both EXEs together).
        /// </summary>
        /// <returns>The full path if found; otherwise null.</returns>
        public static string? TryResolveSnapperPath()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var candidate = Path.Combine(baseDir, $"{SnapperProcessName}.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to start Snapper using a resolved path or by shelling to the process name.
        /// </summary>
        /// <returns>True if a start was attempted successfully; otherwise false.</returns>
        public static bool TryStartSnapper()
        {
            try
            {
                var path = TryResolveSnapperPath();
                if (!string.IsNullOrEmpty(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory,
                    });
                    return true;
                }

                // As a last resort, try letting the shell resolve it (PATH, app alias, etc.)
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"{SnapperProcessName}.exe",
                    UseShellExecute = true,
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

