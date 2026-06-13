using System;
using System.Globalization;
using System.Linq;
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

            bool coTheGiaHan = false;
            try
            {
                var db = new DatabaseService();
                var loaiVeList = db.GetLoaiVe();
                var lv = loaiVeList.FirstOrDefault(x => x.Id == tabId);
                if (lv != null)
                {
                    coTheGiaHan = lv.CoTheGiaHan;
                }
            }
            catch { }

            if (mode == "MonthlyOnly")
            {
                return coTheGiaHan ? Visibility.Visible : Visibility.Collapsed;
            }
            
            if (mode == "TransientOnly")
            {
                return !coTheGiaHan ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
