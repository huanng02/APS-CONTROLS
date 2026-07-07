using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class BangGiaView : UserControl
    {
        public BangGiaView()
        {
            InitializeComponent();
            // attach handler for Manage KhungGio button
            try
            {
                var btn = this.FindName("BtnManageKhungGio") as System.Windows.Controls.Button;
                if (btn != null) btn.Click += OpenKhungGioManager_Click;
            }
            catch { }
        }

        // Open KhungGio management window
        private void OpenKhungGioManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wnd = new Window
                {
                    Title = "Quản lý khung giờ",

                    Content = new KhungGioView(),

                    Width = 1200,
                    Height = 700,

                    MinWidth = 1000,
                    MinHeight = 600,

                    ResizeMode = ResizeMode.CanResize,

                    WindowStartupLocation = WindowStartupLocation.CenterScreen,

                    // QUAN TRỌNG
                    Owner = null,

                    Background = System.Windows.Media.Brushes.White
                };

                wnd.ShowDialog();

                // refresh pricing VM so updated KhungGio are reflected
                if (this.DataContext is BangGiaManagementViewModel vm)
                {
                    try
                    {
                        vm.Load();
                    }
                    catch { }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // Allow only numeric input (digits and optional decimal separator)
        private void NumberOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // allow digits and comma/dot
            var text = e.Text;
            if (string.IsNullOrEmpty(text)) { e.Handled = true; return; }
            foreach (var ch in text)
            {
                if (!char.IsDigit(ch) && ch != ',' && ch != '.') { e.Handled = true; return; }
            }
            e.Handled = false;
        }
    }
}
