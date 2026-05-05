using System;
using System.Globalization;
using System.Windows.Data;

namespace QuanLyGiuXe
{
    // Converts between nullable decimal and string for TextBox bindings.
    public class NullableDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DBNull.Value) return string.Empty;
            if (value is decimal d)
            {
                // use current culture for display
                return d.ToString(culture ?? CultureInfo.CurrentCulture);
            }
            if (value is decimal?)
            {
                var nd = (decimal?)value;
                return nd.HasValue ? nd.Value.ToString(culture ?? CultureInfo.CurrentCulture) : string.Empty;
            }
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value as string) ?? string.Empty;
            s = s.Trim();
            if (string.IsNullOrEmpty(s)) return null;

            if (decimal.TryParse(s, NumberStyles.Number, culture ?? CultureInfo.CurrentCulture, out var d))
                return d;

            // return UnsetValue so binding ignores invalid input (user can continue typing)
            return Binding.DoNothing;
        }
    }
}
