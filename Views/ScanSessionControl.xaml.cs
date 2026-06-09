using System;
using System.Windows.Controls;
using QuanLyGiuXe.Models;
using System.Windows;

namespace QuanLyGiuXe.Views
{
    public partial class ScanSessionControl : UserControl
    {
        public ScanSessionControl()
        {
            InitializeComponent();
        }

        // Raised when the user clicks the close button so the host can remove this control
        public event EventHandler? RequestClose;

        public void SetSession(LichSuXe session)
        {
            if (session == null) return;
            UidText.Text = session.CardId.ToString();
            PlateText.Text = session.BienSo ?? "-";
            TimeInText.Text = session.ThoiGianVao.ToString("yyyy-MM-dd HH:mm:ss");
            TimeOutText.Text = session.ThoiGianRa.HasValue ? session.ThoiGianRa.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
            FeeText.Text = session.Tien.HasValue ? session.Tien.Value.ToString("N0") + " VNĐ" : "-";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
