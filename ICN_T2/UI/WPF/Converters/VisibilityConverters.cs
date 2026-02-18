using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ICN_T2.UI.WPF.Converters
{
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverted = string.Equals(parameter as string, "Inverted", StringComparison.OrdinalIgnoreCase);
            bool isVisible = value != null;
            if (inverted) isVisible = !isVisible;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public sealed class StringEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverted = string.Equals(parameter as string, "Inverted", StringComparison.OrdinalIgnoreCase);
            bool isVisible = string.IsNullOrEmpty(value as string);
            if (inverted) isVisible = !isVisible;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
