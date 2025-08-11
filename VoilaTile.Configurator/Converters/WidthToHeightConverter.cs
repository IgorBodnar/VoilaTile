namespace VoilaTile.Configurator.Converters
{
    using System.Globalization;
    using System.Windows.Data;

    /// <summary>
    /// A converter for calculating the control height from its witdth and aspect ratio.
    /// </summary>
    public class WidthToHeightConverter : IValueConverter
    {
        /// <summary>
        /// Calculates the control height based on the provided with and aspect ratio.
        /// </summary>
        /// <param name="value">The value representing the width.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The parameter representing the aspect ratio.</param>
        /// <param name="culture">The culture info.</param>
        /// <returns>A control height value based on the provided width and aspect ratio.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && parameter is string ratioString && double.TryParse(ratioString, out double aspectRatio))
            {
                return width / aspectRatio;
            }

            return 100; // fallback
        }

        /// <summary>
        /// The <see cref="WidthToHeightConverter"/> is a uniderectional converter not implementing the <see cref="ConvertBack"/> method.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="parameter">The parameter.</param>
        /// <param name="culture">The culture info.</param>
        /// <returns>Does not return, throws <see cref="NotImplementedException"/>.</returns>
        /// <exception cref="NotImplementedException">Not implemented for a unidirectional converter.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
