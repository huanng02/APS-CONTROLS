using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class KhungGioView : UserControl
    {
        public KhungGioView()
        {
            InitializeComponent();
            // Initialize DataContext on UI thread asynchronously to avoid any heavy work during construction
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                try { this.DataContext = new KhungGioManagementViewModel(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Set DataContext failed: " + ex); }
            }));
        }
    }
}
