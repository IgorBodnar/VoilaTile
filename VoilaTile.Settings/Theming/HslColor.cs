namespace VoilaTile.Settings.Theming
{
    using System;
    using System.Windows.Media;

    /// <summary>
    /// Represents an immutable color in the HSL (Hue, Saturation, Lightness) color space.
    /// Provides conversion methods between HSL and RGB.
    /// </summary>
    public readonly record struct HslColor(double H, double S, double L)
    {
        #region Methods

        /// <summary>
        /// Converts this HSL color to an RGB <see cref="Color"/>.
        /// </summary>
        /// <param name="a">The alpha channel (0–255).</param>
        /// <returns>The equivalent RGB color.</returns>
        public Color ToRgb(byte a = 255)
        {
            static double HueToRgb(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
                if (t < 1.0 / 2.0) return q;
                if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
                return p;
            }

            double h = Math.Clamp(this.H, 0, 1);
            double s = Math.Clamp(this.S, 0, 1);
            double l = Math.Clamp(this.L, 0, 1);

            double r, g, b;
            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return Color.FromArgb(
                a,
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255));
        }

        /// <summary>
        /// Creates an <see cref="HslColor"/> from an RGB <see cref="Color"/>.
        /// </summary>
        /// <param name="c">The RGB color to convert.</param>
        /// <returns>The equivalent HSL color.</returns>
        public static HslColor FromRgb(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double h, s, l = (max + min) / 2.0;

            if (Math.Abs(max - min) < 1e-9)
            {
                h = s = 0; // achromatic
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                if (Math.Abs(max - r) < 1e-9)
                {
                    h = (g - b) / d + (g < b ? 6 : 0);
                }
                else if (Math.Abs(max - g) < 1e-9)
                {
                    h = (b - r) / d + 2;
                }
                else
                {
                    h = (r - g) / d + 4;
                }

                h /= 6.0;
            }

            return new HslColor(h, s, l);
        }

        #endregion Methods
    }
}

