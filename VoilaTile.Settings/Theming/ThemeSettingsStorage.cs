namespace VoilaTile.Settings.Theming
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Windows.Media;
    using VoilaTile.Common.DTO;
    using VoilaTile.Common.Helpers;
    using VoilaTile.Common.Theming;

    /// <summary>
    /// Loads and saves theme settings to a JSON file, and maps them to/from <see cref="ThemeManager"/>.
    /// </summary>
    public static class ThemeSettingsStorage
    {
        /// <summary>
        /// Loads a <see cref="ThemeSettingsDTO"/> from disk or returns a default instance if the file does not exist or is invalid.
        /// </summary>
        /// <returns>A DTO representing stored theme settings.</returns>
        public static ThemeSettingsDTO LoadOrDefault()
        {
            var path = AppPaths.ThemeSettingsPath;

            try
            {
                if (!File.Exists(path))
                {
                    return new ThemeSettingsDTO();
                }

                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<ThemeSettingsDTO>(json, JsonOptions());
                return dto ?? new ThemeSettingsDTO();
            }
            catch
            {
                // Corrupt or unreadable file -> use defaults.
                return new ThemeSettingsDTO();
            }
        }

        /// <summary>
        /// Saves a <see cref="ThemeSettingsDTO"/> to disk, creating the directory if needed.
        /// </summary>
        /// <param name="dto">The DTO to serialize.</param>
        public static void Save(ThemeSettingsDTO dto)
        {
            var path = AppPaths.ThemeSettingsPath;

            // Validate before writing to disk
            if (!Enum.TryParse<ThemeMode>(dto.ThemeMode, true, out _))
                dto.ThemeMode = ThemeMode.System.ToString();

            if (!Enum.TryParse<AccentMode>(dto.AccentMode, true, out _))
                dto.AccentMode = AccentMode.Windows.ToString();

            if (!TryParseHex(dto.CustomAccentHex, out _))
                dto.CustomAccentHex = Defaults.DefaultAccentHex;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(dto, JsonOptions());
            File.WriteAllText(path, json);
        }


        /// <summary>
        /// Applies stored settings to a <see cref="ThemeManager"/> without calling <see cref="ThemeManager.Initialize"/>.
        /// </summary>
        /// <param name="manager">The theme manager.</param>
        /// <param name="dto">The stored settings.</param>
        public static void ApplyToManager(ThemeManager manager, ThemeSettingsDTO dto)
        {
            // --- Validate & parse theme mode ---
            var parsedTheme = ParseThemeMode(dto.ThemeMode);
            if (!Enum.IsDefined(typeof(ThemeMode), parsedTheme))
            {
                parsedTheme = ThemeMode.System;
            }

            // --- Validate & parse accent mode ---
            var parsedAccent = ParseAccentMode(dto.AccentMode);
            if (!Enum.IsDefined(typeof(AccentMode), parsedAccent))
            {
                parsedAccent = AccentMode.Windows;
            }

            manager.ThemeMode = parsedTheme;
            manager.AccentMode = parsedAccent;

            // --- Validate custom accent ---
            // If AccentMode == Custom but color string invalid, fallback to the default blue.
            if (manager.AccentMode == AccentMode.Custom)
            {
                if (!TryParseHex(dto.CustomAccentHex, out var color))
                {
                    // fallback to default VoilaTile accent
                    color = Defaults.DefaultAccentColor;
                }

                manager.CustomAccent = color;
            }
        }


        /// <summary>
        /// Builds a DTO from the current state of <see cref="ThemeManager"/>.
        /// </summary>
        /// <param name="manager">The theme manager.</param>
        /// <returns>A DTO capturing the current theme selection.</returns>
        public static ThemeSettingsDTO FromManager(ThemeManager manager)
        {
            return new ThemeSettingsDTO
            {
                ThemeMode = manager.ThemeMode.ToString(),
                AccentMode = manager.AccentMode.ToString(),
                CustomAccentHex = ToHex(manager.CustomAccent),
            };
        }

        /// <summary>
        /// Parses a #RRGGBB hex string into a <see cref="Color"/>.
        /// </summary>
        /// <param name="hex">The hex string.</param>
        /// <param name="color">The parsed color.</param>
        /// <returns>True on success; otherwise false.</returns>
        public static bool TryParseHex(string? hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var m = Regex.Match(hex.Trim(), "^#?(?<r>[0-9A-Fa-f]{2})(?<g>[0-9A-Fa-f]{2})(?<b>[0-9A-Fa-f]{2})$");
            if (!m.Success)
            {
                return false;
            }

            byte r = Convert.ToByte(m.Groups["r"].Value, 16);
            byte g = Convert.ToByte(m.Groups["g"].Value, 16);
            byte b = Convert.ToByte(m.Groups["b"].Value, 16);
            color = Color.FromRgb(r, g, b);
            return true;
        }

        /// <summary>
        /// Formats a <see cref="Color"/> as #RRGGBB.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>A hex string.</returns>
        public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        private static JsonSerializerOptions JsonOptions() => new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private static ThemeMode ParseThemeMode(string? value)
        {
            return Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var parsed) ? parsed : ThemeMode.System;
        }

        private static AccentMode ParseAccentMode(string? value)
        {
            return Enum.TryParse<AccentMode>(value, ignoreCase: true, out var parsed) ? parsed : AccentMode.Windows;
        }
    }
}

