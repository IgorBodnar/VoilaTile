namespace VoilaTile.Configurator.Converters
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    /// <summary>
    /// Converts a boolean to Visibility. True becomes Visible, false becomes Collapsed.
    /// If parameter is "invert", then True becomes Collapsed.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                bool invert = parameter?.ToString()?.ToLowerInvariant() == "invert";
                return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool invert = parameter?.ToString()?.ToLowerInvariant() == "invert";
                return (v == Visibility.Visible) ^ invert;
            }

            return false;
        }
    }
}

