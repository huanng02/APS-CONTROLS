using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ParkingView : UserControl
    {
        public ParkingView()
        {
            InitializeComponent();
        }

        private void OpenGateIn_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.OpenGateIn_Click(sender, e);
            }
        }

        private void OpenGateOut_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.OpenGateOut_Click(sender, e);
            }
        }
        
        // Methods to update cameras from MainWindow
        public void UpdateCamera(string key, System.Windows.Media.ImageSource source)
        {
            switch (key)
            {
                case "Vao1": CameraVao1.Source = source; break;
                case "Vao2": CameraVao2.Source = source; break;
                case "Ra1": CameraRa1.Source = source; break;
                case "Ra2": CameraRa2.Source = source; break;
            }
        }
    }
}
