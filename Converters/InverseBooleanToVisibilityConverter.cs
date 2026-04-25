using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuanLyGiuXe.Converters
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Visible;
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            try
            {
                var v = System.Convert.ToBoolean(value);
                return v ? Visibility.Collapsed : Visibility.Visible;
            }
            catch
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
