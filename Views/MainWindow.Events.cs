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
    }
}
