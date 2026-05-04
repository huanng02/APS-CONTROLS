using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            this.DataContext = new DashboardViewModel();
        }
    }
}
