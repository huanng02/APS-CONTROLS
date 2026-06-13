using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for PersonnelExplorerView.xaml
    /// </summary>
    public partial class PersonnelExplorerView : UserControl
    {
        public PersonnelExplorerView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is PersonnelExplorerViewModel viewModel)
            {
                viewModel.SelectedNode = e.NewValue as PersonnelTreeNode;
            }
        }
    }
}
