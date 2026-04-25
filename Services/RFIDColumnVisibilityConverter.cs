using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuanLyGiuXe.Services
{
    public class RFIDColumnVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value: SelectedTab.Id (int)
            // parameter: "MonthlyOnly", "TransientOnly"
            
            if (!(value is int tabId)) return Visibility.Visible;
            string mode = parameter as string;

            if (tabId == 0) return Visibility.Visible; // Show all in 'Tất cả' tab

            if (mode == "MonthlyOnly")
            {
                // tabId == 2 (Monthly)
                return (tabId == 2) ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (mode == "TransientOnly")
            {
                // tabId == 1 (Transient) or others
                return (tabId == 1) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
