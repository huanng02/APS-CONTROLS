using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace QuanLyGiuXe.Converters
{
    // Converts an event type string to a background brush for quick visual scanning.
    public class EventToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value ?? string.Empty).ToString();
            if (string.IsNullOrWhiteSpace(s)) return Brushes.White;

            var v = s.ToUpperInvariant();

            // Error-like events -> light red
            if (v.Contains("ERROR") || v.Contains("FAILED") || v.Contains("EXCEPTION"))
                return new SolidColorBrush(Color.FromRgb(249, 234, 234));

            // Button press / hardware events -> light blue
            if (v.Contains("BUTTON") || v.Contains("PRESS") || v.Contains("RT"))
                return new SolidColorBrush(Color.FromRgb(230, 245, 255));

            // RFID / scan events -> light green
            if (v.Contains("RFID") || v.Contains("SCAN") || v.Contains("CARD"))
                return new SolidColorBrush(Color.FromRgb(240, 255, 230));

            // Property/Info updates -> light yellow
            if (v.Contains("PROPERTY") || v.Contains("INFO") || v.Contains("CHANGED"))
                return new SolidColorBrush(Color.FromRgb(255, 250, 230));

            // default
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
