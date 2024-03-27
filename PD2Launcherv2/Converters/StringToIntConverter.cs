using System;
using System.Globalization;
using System.Windows.Data;

namespace PD2Launcherv2.Converters
{
    public class StringToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Convert from int to string
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Convert from string to int
            if (int.TryParse(value as string, out int result))
            {
                return result;
            }
            return 0; // Or your default value
        }
    }
}