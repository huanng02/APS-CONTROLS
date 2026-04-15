using System.Windows;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe
{
    public partial class MainWindow
    {
        private void MoRealtimeLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new RealtimeLogWindow { Owner = this };
                win.Show();
            }
            catch { }
        }

        private void MoDanhSachRFID(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new QuanLyGiuXe.Views.QuanLyTheWindow
                {
                    Owner = this
                };

                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Lỗi mở danh sách RFID:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
