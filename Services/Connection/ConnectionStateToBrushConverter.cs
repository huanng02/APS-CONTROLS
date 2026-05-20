using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuanLyGiuXe.Services.Connection
{
    public class ConnectionStateToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ConnectionState state)
            {
                return state switch
                {
                    ConnectionState.Connected => new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113)),    // Green
                    ConnectionState.Reconnecting => new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 196, 15)), // Yellow
                    ConnectionState.Disconnected => new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)),  // Red
                    ConnectionState.Failed => new SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 57, 43)),        // Dark Red
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
