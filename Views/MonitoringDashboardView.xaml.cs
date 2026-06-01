using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for MonitoringDashboardView.xaml
    /// </summary>
    public partial class MonitoringDashboardView : UserControl
    {
        public MonitoringDashboardView()
        {
            InitializeComponent();
            DataContext = new MonitoringDashboardViewModel();
        }
    }
}
