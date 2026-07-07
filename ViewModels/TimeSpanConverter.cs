using System;
using System.Globalization;
using System.Windows.Data;

namespace QuanLyGiuXe.ViewModels
{
    public class TimeSpanConverter : IValueConverter
    {
        // Converts TimeSpan -> string "HH:mm" and back
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan ts)
            {
                return ts.ToString("hh\\:mm");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s)) return TimeSpan.Zero;
            if (TimeSpan.TryParseExact(s.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var ts))
                return ts;
            // try parse flexible
            if (TimeSpan.TryParse(s.Trim(), out ts)) return ts;
            return TimeSpan.Zero;
        }
    }
}
