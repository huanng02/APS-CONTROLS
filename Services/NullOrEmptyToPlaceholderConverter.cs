using System;
using System.Globalization;
using System.Windows.Data;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Converter that returns a placeholder string when input is null or whitespace.
    /// </summary>
    public class NullOrEmptyToPlaceholderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var s = value as string;
                if (string.IsNullOrWhiteSpace(s))
                {
                    if (parameter is string p && !string.IsNullOrEmpty(p)) return p;
                    return "Coming soon";
                }
                return s;
            }
            catch
            {
                return parameter is string p ? p : "Coming soon";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
