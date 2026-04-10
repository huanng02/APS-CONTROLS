using System;
using System.Globalization;
using System.Windows.Data;

namespace QuanLyGiuXe.Converters
{
    public class IndexPlusOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return "";
                if (int.TryParse(value.ToString(), out int idx))
                {
                    return (idx + 1).ToString();
                }
            }
            catch { }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
