using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;

namespace QuanLyGiuXe.Converters
{
    /// <summary>
    /// Converts log Level string to foreground, background, or icon for timeline event feed UI.
    /// Use ConverterParameter: "Foreground", "Background", "BackgroundLight", or "Icon".
    /// </summary>
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string level = (value?.ToString() ?? "").ToUpperInvariant();
            string mode = (parameter?.ToString() ?? "Foreground").ToUpperInvariant();

            return mode switch
            {
                "FOREGROUND" => level switch
                {
                    "SUCCESS" => new SolidColorBrush(WpfColor.FromRgb(0x27, 0xAE, 0x60)),   // #27AE60
                    "INFO" => new SolidColorBrush(WpfColor.FromRgb(0x29, 0x80, 0xB9)),       // #2980B9
                    "WARNING" => new SolidColorBrush(WpfColor.FromRgb(0xF3, 0x9C, 0x12)),    // #F39C12
                    "ERROR" => new SolidColorBrush(WpfColor.FromRgb(0xE7, 0x4C, 0x3C)),      // #E74C3C
                    "CRITICAL" => new SolidColorBrush(WpfColor.FromRgb(0xC0, 0x39, 0x2B)),   // #C0392B
                    _ => new SolidColorBrush(WpfColor.FromRgb(0x7F, 0x8C, 0x8D))             // #7F8C8D
                },
                "BACKGROUND" => level switch
                {
                    "SUCCESS" => new SolidColorBrush(WpfColor.FromRgb(0xE8, 0xF8, 0xF5)),    // #E8F8F5
                    "INFO" => new SolidColorBrush(WpfColor.FromRgb(0xEB, 0xF5, 0xFB)),       // #EBF5FB
                    "WARNING" => new SolidColorBrush(WpfColor.FromRgb(0xFE, 0xF9, 0xE7)),    // #FEF9E7
                    "ERROR" => new SolidColorBrush(WpfColor.FromRgb(0xFD, 0xED, 0xEC)),      // #FDEDEC
                    "CRITICAL" => new SolidColorBrush(WpfColor.FromRgb(0xF5, 0xB7, 0xB1)),   // #F5B7B1
                    _ => new SolidColorBrush(WpfColor.FromRgb(0xF8, 0xF9, 0xFA))             // #F8F9FA
                },
                "ICON" => level switch
                {
                    "SUCCESS" => "✅",
                    "INFO" => "ℹ️",
                    "WARNING" => "⚠️",
                    "ERROR" => "❌",
                    "CRITICAL" => "🔥",
                    _ => "📝"
                },
                _ => Brushes.Gray
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts nullable values (null, empty, 0, false) to Visibility.Collapsed, otherwise Visible.
    /// Handles int?, long?, bool?, and string types.
    /// </summary>
    public class NullableToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return System.Windows.Visibility.Collapsed;
            
            if (value is string s && string.IsNullOrWhiteSpace(s))
                return System.Windows.Visibility.Collapsed;
            
            if (value is bool b && !b)
                return System.Windows.Visibility.Collapsed;
            
            if (value is int i && i == 0)
                return System.Windows.Visibility.Collapsed;
            
            if (value is long l && l == 0)
                return System.Windows.Visibility.Collapsed;

            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
