namespace VoilaTile.Common.Interop
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Win32 monitor/display interop structs.
    /// </summary>
    public static class MonitorInteropStructs
    {
        #region Win32 Types

        /// <summary>
        /// Win32 <c>RECT</c> structure (left, top, right, bottom) in device pixels.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            /// <summary>
            /// Left edge (inclusive).
            /// </summary>
            public int left;

            /// <summary>
            /// Top edge (inclusive).
            /// </summary>
            public int top;

            /// <summary>
            /// Right edge (exclusive).
            /// </summary>
            public int right;

            /// <summary>
            /// Bottom edge (exclusive).
            /// </summary>
            public int bottom;
        }

        /// <summary>
        /// Win32 <c>MONITORINFOEX</c> structure describing monitor geometry and device name.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            /// <summary>
            /// Size of this structure (in bytes). Must be set by caller.
            /// </summary>
            public int cbSize;

            /// <summary>
            /// Monitor rectangle, in device pixels.
            /// </summary>
            public Rect rcMonitor;

            /// <summary>
            /// Work area rectangle (excludes taskbar/docked bars), in device pixels.
            /// </summary>
            public Rect rcWork;

            /// <summary>
            /// Flags (e.g., <c>MONITORINFOF_PRIMARY</c>).
            /// </summary>
            public int dwFlags;

            /// <summary>
            /// Device name (e.g., <c>"\\.\DISPLAY1"</c>).
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        /// <summary>
        /// Win32 <c>DISPLAY_DEVICE</c> structure returned by <see cref="EnumDisplayDevices"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            /// <summary>
            /// Size of this structure (in bytes). Must be set by caller.
            /// </summary>
            public int cb;

            /// <summary>
            /// Adapter or display name.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            /// <summary>
            /// Adapter or display string.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            /// <summary>
            /// State flags.
            /// </summary>
            public int StateFlags;

            /// <summary>
            /// Device ID.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            /// <summary>
            /// Device registry key.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        #endregion
    }
}

