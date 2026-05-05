using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Hiển thị nút Gia hạn dựa trên LoaiVeId của từng dòng.
    /// Monthly ticket (tên chứa "tháng/thang/month") -> Visible
    /// Others (Vé lượt, Vãng lai, etc.) -> Collapsed
    /// </summary>
    public class LoaiVeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int loaiVeId && loaiVeId > 0)
            {
                // Name-based check: show GiaHan button only for monthly tickets
                try
                {
                    var db = new DatabaseService();
                    var loaiVeList = db.GetLoaiVe();
                    var lv = loaiVeList.FirstOrDefault(x => x.Id == loaiVeId);
                    if (lv != null)
                    {
                        bool isMonthly = lv.CoTheGiaHan;
                        return isMonthly ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                catch { }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
