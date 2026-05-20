using System;
using System.Windows;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    internal class VehicleDetailWindow
    {
        internal VehicleDetailWindow(Xe vehicle)
        {
            _vehicle = vehicle;
        }

        private readonly Xe _vehicle;

        public bool? ShowDialog()
        {
            var vao = _vehicle.ThoiGianVao.ToString("dd/MM/yyyy HH:mm:ss");
            var ra = _vehicle.ThoiGianRa?.ToString("dd/MM/yyyy HH:mm:ss") ?? "Xe đang ở trong bãi";
            var duration = (_vehicle.ThoiGianRa ?? DateTime.Now) - _vehicle.ThoiGianVao;
            var fee = _vehicle.ThoiGianRa.HasValue
                ? (Math.Ceiling(duration.TotalHours) * 5000).ToString("N0") + " VNĐ"
                : "Đang gửi...";

            MessageBox.Show(
                $"Biển số: {_vehicle.BienSo}\n" +
                $"Vào: {vao}\n" +
                $"Ra: {ra}\n" +
                $"Thời gian: {(int)duration.TotalHours} giờ {duration.Minutes} phút\n" +
                $"Phí: {fee}",
                "Chi tiết xe",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return true;
        }
    }
}
