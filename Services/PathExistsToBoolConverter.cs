using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace QuanLyGiuXe.Services
{
    public class PathExistsToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var s = value as string;
                return !string.IsNullOrEmpty(s) && File.Exists(s);
            }
            catch { return false; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
