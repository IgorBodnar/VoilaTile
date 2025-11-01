namespace VoilaTile.Settings.Theming
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows.Media;
    using Microsoft.Win32;

    /// <summary>
    /// Interop helpers for reading Windows theme preferences and accent color.
    /// </summary>
    public static class WindowsThemeInterop
    {
        #region Fields

        /// <summary>
        /// Registry path to Windows theme personalization settings.
        /// </summary>
        private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        /// <summary>
        /// Registry value indicating if apps should use the light theme (1 = Light, 0 = Dark).
        /// </summary>
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        #endregion Fields

        #region Methods

        /// <summary>
        /// Gets the Windows accent color via DWM. Returns the specified fallback if unavailable.
        /// </summary>
        /// <param name="fallback">Optional fallback color if retrieval fails.</param>
        /// <returns>The Windows accent color, or the fallback if unavailable.</returns>
        public static Color GetWindowsAccentOrDefault(Color? fallback = null)
        {
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
                // Swallow and use fallback below.
            }

            return fallback ?? Color.FromRgb(0x4C, 0x8C, 0xFF);
        }

        /// <summary>
        /// Determines whether Windows is configured to use the Light theme for apps.
        /// </summary>
        /// <returns><see langword="true"/> if Light theme is enabled for apps; otherwise <see langword="false"/> (Dark).</returns>
        public static bool IsWindowsAppsLightMode()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
                if (key?.GetValue(AppsUseLightThemeValue) is int v)
                {
                    return v != 0;
                }
            }
            catch
            {
                // Ignore and fall through to default below.
            }

            // Default to Light if unknown (safer for contrast).
            return true;
        }

        /// <summary>
        /// P/Invoke to retrieve DWM colorization color.
        /// </summary>
        /// <param name="pcrColorization">Receives the ARGB colorization value.</param>
        /// <param name="pfOpaqueBlend">Receives whether colorization is opaque.</param>
        /// <returns>HRESULT (0 on success).</returns>
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmGetColorizationColor(out uint pcrColorization, out bool pfOpaqueBlend);

        #endregion Methods
    }
}

