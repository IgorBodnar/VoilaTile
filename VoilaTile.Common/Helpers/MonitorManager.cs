namespace VoilaTile.Common.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using VoilaTile.Common.Models;

    public static class MonitorManager
    {
        #region Records

        public record MonitorGeometryInfo(string DeviceName, Rect MonitorBounds, Rect WorkBounds, IntPtr HMonitor);
        public record MonitorDeviceInfo(string DeviceName, string DeviceString, string DeviceID);
        public record MonitorDpiInfo(string DeviceName, uint DpiX, uint DpiY);
        public record MonitorEdidInfo(string ManufacturerId, string ProductCode, string SerialNumber);


        #endregion Records

        #region Methods

        public static List<MonitorInfo> GetMonitors()
        {
            List<MonitorInfo> monitors = new List<MonitorInfo>();

            // Geometry and DPI
            Dictionary<string, MonitorGeometryInfo> geometryMap = new();
            Dictionary<string, MonitorDpiInfo> dpiMap = new();

            bool EnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
            {
                MONITORINFOEX info = new MONITORINFOEX();
                info.cbSize = Marshal.SizeOf<MONITORINFOEX>();
                GetMonitorInfo(hMonitor, ref info);

                string deviceName = info.szDevice.TrimEnd('\0');
                geometryMap[deviceName] = new MonitorGeometryInfo(deviceName, info.rcMonitor, info.rcWork, hMonitor);

                uint dpiX = Defaults.StandardDpi, dpiY = Defaults.StandardDpi;
                int hr = GetDpiForMonitor(hMonitor, MonitorDpiType.EffectiveDpi, out dpiX, out dpiY);
                if (hr != 0) 
                { 
                    dpiX = Defaults.StandardDpi;
                    dpiY = Defaults.StandardDpi;
                }
                dpiMap[deviceName] = new MonitorDpiInfo(deviceName, dpiX, dpiY);

                return true;
            }

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumProc, IntPtr.Zero);

            // Device and EDID info
            Dictionary<string, MonitorDeviceInfo> deviceMap = new();
            Dictionary<string, MonitorEdidInfo> edidMap = new();

            for (int i = 0; ; i++)
            {
                DISPLAY_DEVICE gpuDevice = new DISPLAY_DEVICE();
                gpuDevice.cb = Marshal.SizeOf<DISPLAY_DEVICE>();

                if (!EnumDisplayDevices(null, i, ref gpuDevice, 0))
                    break;

                if ((gpuDevice.StateFlags & 0x00000001) == 0)
                    continue;

                string displayName = gpuDevice.DeviceName;

                for (int j = 0; ; j++)
                {
                    DISPLAY_DEVICE monitorDevice = new DISPLAY_DEVICE();
                    monitorDevice.cb = Marshal.SizeOf<DISPLAY_DEVICE>();

                    if (!EnumDisplayDevices(displayName, j, ref monitorDevice, 0))
                        break;

                    if ((monitorDevice.StateFlags & 0x00000001) == 0)
                        continue;

                    // Expected: DeviceID = "MONITOR\\DEL404C\\..."
                    deviceMap[displayName] = new MonitorDeviceInfo(
                        displayName,
                        monitorDevice.DeviceString,
                        monitorDevice.DeviceID);
                }
            }

            // Parse EDID from registry
            using var displayKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (displayKey != null)
            {
                foreach (var vendorKey in displayKey.GetSubKeyNames())
                {
                    using var vendorSubKey = displayKey.OpenSubKey(vendorKey);
                    if (vendorSubKey == null) continue;

                    foreach (var instanceKey in vendorSubKey.GetSubKeyNames())
                    {
                        using var monitorKey = vendorSubKey.OpenSubKey(instanceKey);
                        if (monitorKey == null) continue;

                        using var parameters = monitorKey.OpenSubKey("Device Parameters");
                        var edidBytes = parameters?.GetValue("EDID") as byte[];
                        if (edidBytes == null || edidBytes.Length < 128) continue;

                        string manufacturer = Encoding.ASCII.GetString(new byte[]
                        {
                    (byte)(((edidBytes[8] >> 2) & 0x1F) + 64),
                    (byte)((((edidBytes[8] & 0x3) << 3) | ((edidBytes[9] >> 5) & 0x7)) + 64),
                    (byte)((edidBytes[9] & 0x1F) + 64)
                        });

                        ushort productCode = BitConverter.ToUInt16(edidBytes, 10);
                        uint serial = BitConverter.ToUInt32(edidBytes, 12);

                        string edidKey = $"MONITOR\\{vendorKey}";
                        edidMap[edidKey] = new MonitorEdidInfo(
                            manufacturer,
                            productCode.ToString("X4"),
                            serial.ToString("X8"));
                    }
                }
            }

            // Merge all info
            int monitorNumber = 0;
            foreach (var key in geometryMap.Keys)
            {
                geometryMap.TryGetValue(key, out var geo);
                deviceMap.TryGetValue(key, out var dev);
                dpiMap.TryGetValue(key, out var dpi);

                MonitorEdidInfo? edid = null;
                if (dev != null)
                {
                    var parts = dev.DeviceID?.Split('\\');
                    if (parts?.Length >= 2)
                    {
                        string edidKey = $"{parts[0]}\\{parts[1]}";
                        edidMap.TryGetValue(edidKey, out edid);
                    }
                }

                if (geo != null && dev != null && dpi != null)
                {
                    monitors.Add(new MonitorInfo
                    {
                        MonitorNumber = ++monitorNumber,
                        DeviceName = geo.DeviceName,
                        DeviceString = dev.DeviceString,
                        DeviceID = dev.DeviceID,
                        MonitorX = geo.MonitorBounds.left,
                        MonitorY = geo.MonitorBounds.top,
                        MonitorWidth = geo.MonitorBounds.right - geo.MonitorBounds.left,
                        MonitorHeight = geo.MonitorBounds.bottom - geo.MonitorBounds.top,
                        WorkX = geo.WorkBounds.left,
                        WorkY = geo.WorkBounds.top,
                        WorkWidth = geo.WorkBounds.right - geo.WorkBounds.left,
                        WorkHeight = geo.WorkBounds.bottom - geo.WorkBounds.top,
                        DpiX = dpi.DpiX,
                        DpiY = dpi.DpiY,
                        EdidManufacturer = edid?.ManufacturerId,
                        EdidProductCode = edid?.ProductCode,
                        EdidSerial = edid?.SerialNumber
                    });
                }
            }

            return monitors;
        }


        public static MonitorInfo MergeMonitorData(
            MonitorGeometryInfo geo,
            MonitorDeviceInfo dev,
            MonitorDpiInfo dpi,
            int monitorNumber)
        {
            return new MonitorInfo
            {
                MonitorNumber = monitorNumber,
                DeviceName = geo.DeviceName,
                DeviceString = dev.DeviceString,
                DeviceID = dev.DeviceID,
                MonitorX = geo.MonitorBounds.left,
                MonitorY = geo.MonitorBounds.top,
                MonitorWidth = geo.MonitorBounds.right - geo.MonitorBounds.left,
                MonitorHeight = geo.MonitorBounds.bottom - geo.MonitorBounds.top,
                WorkX = geo.WorkBounds.left,
                WorkY = geo.WorkBounds.top,
                WorkWidth = geo.WorkBounds.right - geo.WorkBounds.left,
                WorkHeight = geo.WorkBounds.bottom - geo.WorkBounds.top,
                DpiX = dpi.DpiX,
                DpiY = dpi.DpiY,
            };
        }


        #endregion Methods

        #region Win32

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public Rect rcMonitor;
            public Rect rcWork;
            public int dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            public int cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public int StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevices(
            string lpDevice,          // can be null or e.g. "\\.\DISPLAY1"
            int iDevNum,              // device index
            ref DISPLAY_DEVICE lpDisplayDevice,
            int dwFlags               // set to 0
        );

        delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [DllImport("shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hMonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        enum MonitorDpiType
        {
            EffectiveDpi = 0,
            AngularDpi = 1,
            RawDpi = 2,
        }

        #endregion Wind32

    }
}