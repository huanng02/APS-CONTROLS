using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class TopologyValidationView : UserControl
    {
        public TopologyValidationView()
        {
            InitializeComponent();
            DataContext = new TopologyValidationViewModel();
        }
    }
}
