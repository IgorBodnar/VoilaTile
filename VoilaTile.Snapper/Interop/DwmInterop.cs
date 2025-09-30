// File: Interop/DwmInterop.cs
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.Interop
{
    using System;
    using System.Runtime.InteropServices;

    internal static class DwmInterop
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int left, top, right, bottom; }

        [Flags]
        internal enum DwmTnpFlags : uint
        {
            RectDestination = 0x00000001,
            RectSource      = 0x00000002,
            Opacity         = 0x00000004,
            Visible         = 0x00000008,
            SourceClientAreaOnly = 0x00000010,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DWM_THUMBNAIL_PROPERTIES
        {
            public DwmTnpFlags dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)] public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)] public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HTHUMBNAIL { public IntPtr Value; public static readonly HTHUMBNAIL Null = new() { Value = IntPtr.Zero }; }

        [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out HTHUMBNAIL thumb);
        [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(HTHUMBNAIL thumb);
        [DllImport("dwmapi.dll")] internal static extern int DwmQueryThumbnailSourceSize(HTHUMBNAIL thumb, out SIZE size);
        [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(HTHUMBNAIL thumb, ref DWM_THUMBNAIL_PROPERTIES props);

        internal static void CheckHr(int hr)
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        }
    }
}
