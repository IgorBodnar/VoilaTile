namespace VoilaTile.Common.Theming
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Media;

    /// <summary>
    /// Generates and applies an accent color scale (050..900) from a single base color.
    /// Light/Dark theme awareness gently nudges lightness/saturation for better legibility.
    /// Also exposes an <c>OnAccent</c> foreground brush chosen by contrast.
    /// </summary>
    public static class AccentPaletteGenerator
    {
        #region Methods

        /// <summary>
        /// Generates the accent scale (050..900) from a base RGB color with small theme-aware nudges.
        /// In Dark theme, higher keys are darker (050 lightest → 900 darkest).
        /// In Light theme, the scale is index-inverted so higher keys are lighter (050 darkest → 900 lightest),
        /// while preserving your existing "600 is primary" convention.
        /// </summary>
        /// <param name="baseColor">The base accent color.</param>
        /// <param name="effectiveTheme">
        /// The theme being rendered (use Light/Dark; if app is in System mode resolve via Windows setting first).
        /// </param>
        /// <returns>A read-only dictionary mapping shade keys (050..900) to colors.</returns>
        public static IReadOnlyDictionary<string, Color> GenerateScale(Color baseColor, ThemeMode effectiveTheme)
        {
            HslColor hsl = HslColor.FromRgb(baseColor);

            // Theme-agnostic targets (seed curve): higher = darker by design.
            var targets = new Dictionary<string, (double S, double L)>(StringComparer.Ordinal)
            {
                ["050"] = (hsl.S * 0.35, 0.95),
                ["100"] = (hsl.S * 0.40, 0.90),
                ["200"] = (hsl.S * 0.55, 0.80),
                ["300"] = (hsl.S * 0.70, 0.70),
                ["400"] = (hsl.S * 0.85, 0.60),
                ["500"] = (hsl.S * 1.00, 0.52),
                ["600"] = (hsl.S * 1.00, 0.44),
                ["700"] = (hsl.S * 0.90, 0.36),
                ["800"] = (hsl.S * 0.80, 0.28),
                ["900"] = (hsl.S * 0.70, 0.22),
            };

            // Nudge magnitudes
            const double Dark_L_Mid = 0.06;
            const double Dark_S_Mid = 0.05;

            const double Light_L_Low = 0.05;  // keep light tints airy on Light UI
            const double Light_S_Low = 0.02;  // small boost to avoid washing out

            foreach (var key in targets.Keys.ToList())
            {
                var (s, l) = targets[key];

                if (effectiveTheme == ThemeMode.Dark)
                {
                    // Lift mid tones and add a bit of saturation for pop.
                    if (key is "300" or "400" or "500" or "600" or "700")
                    {
                        l = Clamp01(l + Dark_L_Mid);
                        if (key is "400" or "500" or "600" or "700")
                        {
                            s = Clamp01(s + Dark_S_Mid);
                        }
                    }
                }
                else // Light
                {
                    // Make the light range a touch lighter & slightly more saturated.
                    if (key is "050" or "100" or "200")
                    {
                        l = Clamp01(l + Light_L_Low);
                        s = Clamp01(s + Light_S_Low);
                    }
                }

                targets[key] = (s, l);
            }

            // Build base (higher = darker).
            var baseDict = new Dictionary<string, Color>(targets.Count, StringComparer.Ordinal);
            foreach (var (k, (s, l)) in targets)
            {
                baseDict[k] = new HslColor(hsl.H, s, l).ToRgb();
            }

            // For Light theme, invert the shade indices so higher = lighter,
            // preserving your "use 600 as main" pattern (600 maps to the visual of 400).
            if (effectiveTheme == ThemeMode.Light)
            {
                static string InvertKey(string k) => k switch
                {
                    "050" => "900",
                    "100" => "800",
                    "200" => "700",
                    "300" => "600",
                    "400" => "500",
                    "500" => "400",
                    "600" => "300",
                    "700" => "200",
                    "800" => "100",
                    "900" => "050",
                    _ => k,
                };

                var flipped = new Dictionary<string, Color>(baseDict.Count, StringComparer.Ordinal);
                foreach (var (k, color) in baseDict)
                {
                    flipped[InvertKey(k)] = color;
                }

                return flipped;
            }

            return baseDict;
        }


        /// <summary>
        /// Applies the generated accent scale to the current application's resources as
        /// <c>Brush.Accent.050</c> .. <c>Brush.Accent.900</c> and updates <c>Brush.OnAccent</c>.
        /// </summary>
        /// <param name="baseColor">The base accent color.</param>
        /// <param name="effectiveTheme">The effective theme (Light/Dark).</param>
        public static void ApplyToResources(Color baseColor, ThemeMode effectiveTheme)
        {
            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            // 1) Build theme-aware accent scale
            var scale = GenerateScale(baseColor, effectiveTheme);

            // 2) Replace each accent brush outright
            foreach (var kv in scale)
            {
                var key = $"Brush.Accent.{kv.Key}";
                var brush = new SolidColorBrush(kv.Value);
                brush.Freeze();
                app.Resources[key] = brush;
            }

            // 3) On-accent (foreground over accent)
            var onAccentColor = ChooseOnAccent(scale["600"], effectiveTheme);
            var onAccentBrush = new SolidColorBrush(onAccentColor);
            onAccentBrush.Freeze();
            app.Resources["Brush.OnAccent"] = onAccentBrush;

            // 4) Translucent overlays for primary accent (600)
            var primary = scale["600"];
            var translucentSteps = new (string Suffix, byte A)[]
            {
                ("T04", (byte)Math.Round(255 * 0.04)),
                ("T08", (byte)Math.Round(255 * 0.08)),
                ("T12", (byte)Math.Round(255 * 0.12)),
                ("T16", (byte)Math.Round(255 * 0.16)),
                ("T24", (byte)Math.Round(255 * 0.24)),
                ("T32", (byte)Math.Round(255 * 0.32)),
                ("T48", (byte)Math.Round(255 * 0.48)),
            };

            foreach (var step in translucentSteps)
            {
                var c = Color.FromArgb(step.A, primary.R, primary.G, primary.B);
                var b = new SolidColorBrush(c);
                b.Freeze();
                app.Resources[$"Brush.Accent.600.{step.Suffix}"] = b;
            }
        }


        /// <summary>
        /// Picks black or white text over the given accent color based on WCAG contrast,
        /// theme bias, and luminance thresholds for more natural results.
        /// </summary>
        /// <param name="accent">The accent surface color (e.g., your 600 swatch).</param>
        /// <param name="theme">The effective theme (Light or Dark).</param>
        /// <returns>Black or white depending on contrast and theme context.</returns>
        public static Color ChooseOnAccent(Color accent, ThemeMode theme)
        {
            double L = RelativeLuminance(accent);

            // Tunable thresholds:
            const double DarkPreferWhiteUpTo = 0.62; // In Dark theme, use white until ~62% luminance
            const double LightPreferBlackFrom = 0.38; // In Light theme, use black from ~38% luminance upward

            if (theme == ThemeMode.Dark && L <= DarkPreferWhiteUpTo)
            {
                return Colors.White;
            }

            if (theme == ThemeMode.Light && L >= LightPreferBlackFrom)
            {
                return Colors.Black;
            }

            // WCAG-AA contrast baseline: 4.5:1 for normal text
            const double TargetContrast = 4.5;

            var black = Colors.Black;
            var white = Colors.White;

            double cBlack = ContrastRatio(accent, black);
            double cWhite = ContrastRatio(accent, white);

            bool blackPass = cBlack >= TargetContrast;
            bool whitePass = cWhite >= TargetContrast;

            // If only one passes, prefer the compliant one.
            if (blackPass ^ whitePass)
            {
                return blackPass ? black : white;
            }

            // Both pass or both fail → apply theme bias with margin/boost.
            const double ClearMargin = 0.45;  // difference needed to override theme bias
            const double BiasBoost = 1.20;    // favor theme-appropriate color when both fail

            if (blackPass && whitePass)
            {
                // Both pass: pick theme-favored unless other exceeds by margin.
                if (theme == ThemeMode.Dark)
                {
                    return (cBlack - cWhite) > ClearMargin ? black : white;
                }
                else
                {
                    return (cWhite - cBlack) > ClearMargin ? white : black;
                }
            }

            // Both fail: boost theme-favored slightly.
            if (theme == ThemeMode.Dark)
            {
                return (cWhite * BiasBoost) >= cBlack ? white : black;
            }
            else
            {
                return (cBlack * BiasBoost) >= cWhite ? black : white;
            }
        }

        /// <summary>
        /// Computes the WCAG contrast ratio between two sRGB colors.
        /// </summary>
        /// <param name="a">The first color.</param>
        /// <param name="b">The second color.</param>
        /// <returns>The contrast ratio (≥ 1.0).</returns>
        public static double ContrastRatio(Color a, Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double l1 = Math.Max(la, lb);
            double l2 = Math.Min(la, lb);
            return (l1 + 0.05) / (l2 + 0.05);
        }

        /// <summary>
        /// Computes relative luminance per WCAG for an sRGB color.
        /// </summary>
        /// <param name="c">The color.</param>
        /// <returns>The relative luminance (0.0 .. 1.0).</returns>
        public static double RelativeLuminance(Color c)
        {
            static double Linearize(byte ch)
            {
                double s = ch / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            double r = Linearize(c.R);
            double g = Linearize(c.G);
            double b = Linearize(c.B);

            return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        }

        /// <summary>
        /// Clamps a double value to the [0,1] range.
        /// </summary>
        /// <param name="v">The input value.</param>
        /// <returns>The value clamped to [0,1] range.</returns>
        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        #endregion Methods
    }
}

