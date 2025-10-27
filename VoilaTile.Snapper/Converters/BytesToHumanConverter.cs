// -------------------------------------------------------------------------------------
// <copyright file="BytesToHumanConverter.cs">
//   Copyright © VoilaTile.
// </copyright>
// -------------------------------------------------------------------------------------
namespace VoilaTile.Snapper.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// Converts a numeric byte value (int, long, ulong, double) into a human-readable
    /// string such as "1.23 GB" or "512 KB".
    /// </summary>
    [ValueConversion(typeof(long), typeof(string))]
    [ValueConversion(typeof(ulong), typeof(string))]
    [ValueConversion(typeof(double), typeof(string))]
    [ValueConversion(typeof(int), typeof(string))]
    public sealed class BytesToHumanConverter : IValueConverter
    {
        /// <inheritdoc/>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return "—";
            }

            double bytes;

            try
            {
                bytes = value switch
                {
                    long l => l,
                    int i => i,
                    ulong ul => ul > long.MaxValue ? long.MaxValue : (long)ul,
                    double d => d,
                    float f => f,
                    _ => System.Convert.ToDouble(value, CultureInfo.InvariantCulture),
                };
            }
            catch
            {
                return "—";
            }

            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            const double TB = GB * 1024.0;

            string suffix;
            double scaled;

            if (bytes >= TB)
            {
                scaled = bytes / TB;
                suffix = " TB";
            }
            else if (bytes >= GB)
            {
                scaled = bytes / GB;
                suffix = " GB";
            }
            else if (bytes >= MB)
            {
                scaled = bytes / MB;
                suffix = " MB";
            }
            else if (bytes >= KB)
            {
                scaled = bytes / KB;
                suffix = " KB";
            }
            else
            {
                scaled = bytes;
                suffix = " B";
            }

            return scaled.ToString("F2", CultureInfo.InvariantCulture) + suffix;
        }

        /// <inheritdoc/>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

