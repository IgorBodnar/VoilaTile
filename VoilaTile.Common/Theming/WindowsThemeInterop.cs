namespace VoilaTile.Common.Theming
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Win32;

    /// <summary>
    /// Interop helpers for reading Windows theme preferences and accent color.
    /// </summary>
    public static class WindowsThemeInterop
    {
        #region Fields

        /// <summary>
        /// The registry path for Windows personalization settings.
        /// </summary>
        private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        /// <summary>
        /// The registry value name for the AppsUseLightTheme flag.
        /// </summary>
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        /// <summary>
        /// The registry path for DWM settings.
        /// </summary>
        private const string DwmKeyPath = @"Software\Microsoft\Windows\DWM";

        /// <summary>
        /// The registry value name for the ColorizationColor value.
        /// </summary>
        private const string ColorizationColorValue = "ColorizationColor";

        #endregion

        #region Accent

        /// <summary>
        /// Gets the Windows accent color via DWM. Falls back to the DWM registry value (ABGR) and finally to the provided default.
        /// </summary>
        /// <param name="fallback">Optional fallback color if retrieval fails.</param>
        /// <returns>The Windows accent color.</returns>
        public static Color GetWindowsAccentOrDefault(Color? fallback = null)
        {
            // Try DWM API
            try
            {
                int hr = DwmGetColorizationColor(out uint argb, out _);
                if (hr == 0)
                {
                    byte a = (byte)((argb >> 24) & 0xFF);
                    byte r = (byte)((argb >> 16) & 0xFF);
                    byte g = (byte)((argb >> 8) & 0xFF);
                    byte b = (byte)(argb & 0xFF);
                    return Color.FromArgb(a == 0 ? (byte)255 : a, r, g, b);
                }
            }
            catch
            {
                // ignore, try registry fallback
            }

            // Try registry fallback (DWORD stored as ABGR)
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(DwmKeyPath);
                if (key?.GetValue(ColorizationColorValue) is int abgr)
                {
                    byte a = (byte)((abgr >> 24) & 0xFF);
                    byte b = (byte)((abgr >> 16) & 0xFF);
                    byte g = (byte)((abgr >> 8) & 0xFF);
                    byte r = (byte)(abgr & 0xFF);
                    return Color.FromArgb(a == 0 ? (byte)255 : a, r, g, b);
                }
            }
            catch
            {
                // ignore
            }

            // Final fallback
            return fallback ?? Color.FromRgb(0x4C, 0x8C, 0xFF);
        }

        #endregion

        #region Apps theme (Light/Dark)

        /// <summary>
        /// Determines whether Windows is configured to use the Light theme for apps.
        /// Defaults to true if the value can't be read.
        /// </summary>
        /// <returns>True if Light theme is active, false if Dark theme is active.</returns>
        public static bool IsWindowsAppsLightMode()
        {
            return TryGetWindowsAppsLightMode(out bool isLight)
                ? isLight
                : true;
        }

        /// <summary>
        /// Tries to read the AppsUseLightTheme flag from Windows. Returns false on failure but sets out param to a safe default.
        /// </summary>
        /// <param name="isLight">Outputs true if Light theme is active, false if Dark theme is active.</param>
        /// <returns>True if the value was read successfully, false otherwise.</returns>
        public static bool TryGetWindowsAppsLightMode(out bool isLight)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                if (key?.GetValue(AppsUseLightThemeValue) is int v)
                {
                    isLight = v != 0;
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            isLight = true; // safe default
            return false;
        }

        #endregion

        #region High Contrast (optional helper)

        /// <summary>
        /// Returns true when a Windows high-contrast theme is active.
        /// </summary>
        /// <returns>True if high-contrast mode is enabled, false otherwise.</returns>
        public static bool IsHighContrastEnabled()
        {
            try
            {
                return SystemParameters.HighContrast;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region P/Invoke

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

        #endregion
    }
}

