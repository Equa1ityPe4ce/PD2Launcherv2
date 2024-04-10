using System.Globalization;
using System.Windows.Data;
using System.Windows;
using System.Diagnostics;

namespace PD2Launcherv2.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"BooleanToVisibilityConverter called with value: {value}");
            bool visible = value is bool && (bool)value;
            Debug.WriteLine($"Returning: {(visible ? "Visible" : "Collapsed")}");
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Debug.WriteLine($"ConvertBack called with value: {value}");
            return value is Visibility && (Visibility)value == Visibility.Visible;
        }
    }
}