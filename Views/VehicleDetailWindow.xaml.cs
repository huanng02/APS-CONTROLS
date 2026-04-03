using System;
using System.Windows;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public partial class VehicleDetailWindow : Window
    {
        public VehicleDetailWindow(Xe vehicle)
        {
            InitializeComponent();
            DisplayVehicleDetails(vehicle);
        }

        private void DisplayVehicleDetails(Xe vehicle)
        {
            TxtBienSo.Text = vehicle.BienSo;
            TxtThoiGianVao.Text = vehicle.ThoiGianVao.ToString("dd/MM/yyyy HH:mm:ss");

            if (vehicle.ThoiGianRa.HasValue)
            {
                TxtThoiGianRa.Text = vehicle.ThoiGianRa.Value.ToString("dd/MM/yyyy HH:mm:ss");
                
                var thoiGianGui = vehicle.ThoiGianRa.Value - vehicle.ThoiGianVao;
                TxtThoiGianGui.Text = FormatTimeSpan(thoiGianGui);

                double tien = Math.Ceiling(thoiGianGui.TotalHours) * 5000;
                TxtTien.Text = tien.ToString("N0") + " VNĐ";
            }
            else
            {
                TxtThoiGianRa.Text = "Xe đang ở trong bãi";
                
                var thoiGianGui = DateTime.Now - vehicle.ThoiGianVao;
                TxtThoiGianGui.Text = FormatTimeSpan(thoiGianGui) + " (chưa tính phí)";
                
                TxtTien.Text = "Đang gửi...";
            }
        }

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours < 1)
                return $"{(int)ts.TotalMinutes} phút";
            
            if (ts.TotalDays < 1)
                return $"{(int)ts.TotalHours} giờ {ts.Minutes} phút";
            
            return $"{(int)ts.TotalDays} ngày {ts.Hours} giờ";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
