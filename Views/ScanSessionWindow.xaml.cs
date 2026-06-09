using System;
using System.Windows;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public partial class ScanSessionWindow : Window
    {
        public ScanSessionWindow()
        {
            InitializeComponent();
        }

        public void SetSession(LichSuXe session)
        {
            if (session == null) return;
            UidText.Text = session.CardId.ToString();
            PlateText.Text = session.BienSo ?? "-";
            TimeInText.Text = session.ThoiGianVao.ToString("yyyy-MM-dd HH:mm:ss");
            TimeOutText.Text = session.ThoiGianRa.HasValue ? session.ThoiGianRa.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            FeeText.Text = session.Tien.HasValue ? session.Tien.Value.ToString("N0") + " VNĐ" : "-";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
