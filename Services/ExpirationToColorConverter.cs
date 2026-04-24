using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuanLyGiuXe.Services
{
    public class ExpirationToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dt)
            {
                if (dt < DateTime.Now) return new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)); // Light Red
                return new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)); // Light Green
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
