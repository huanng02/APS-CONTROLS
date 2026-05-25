using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ParkingTopologyView : UserControl
    {
        public ParkingTopologyView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ParkingTopologyViewModel viewModel)
            {
                viewModel.SelectedNode = e.NewValue as TopologyTreeNode;
            }
        }
    }
}
