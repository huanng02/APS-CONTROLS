using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace QuanLyGiuXe.Services
{
    /// <summary>
    /// Hiển thị nút Gia hạn dựa trên LoaiVeId của từng dòng.
    /// LoaiVeId = 2 (Vé tháng) -> Visible
    /// LoaiVeId = 1 (Vãng lai) -> Collapsed
    /// </summary>
    public class LoaiVeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int loaiVeId)
            {
                // Chỉ hiển thị cho Vé tháng (LoaiVeId = 2)
                return (loaiVeId == 2) ? Visibility.Visible : Visibility.Collapsed;
            }
            // Mặc định cho phép check nullable int
            if (value == null) return Visibility.Collapsed;
            
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
