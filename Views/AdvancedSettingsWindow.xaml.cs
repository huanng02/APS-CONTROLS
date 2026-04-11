using System.Windows;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class AdvancedSettingsWindow : Window
    {
        public AdvancedSettingsWindow()
        {
            InitializeComponent();
            DataContext = Application.Current?.MainWindow?.DataContext; // reuse MainViewModel
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private async void OpenGateOut_Click(object sender, RoutedEventArgs e)
        {
            await C3200Service.Instance.OpenBarrierAsync(2);
        }
    }
}
