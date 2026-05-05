using System.Windows.Controls;
using System.Windows.Input;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class DatabaseExplorerView : UserControl
    {
        public DatabaseExplorerView()
        {
            InitializeComponent();
            this.DataContext = new DatabaseExplorerViewModel();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            // Hỗ trợ ấn F5 để chạy query
            if (e.Key == Key.F5)
            {
                var vm = this.DataContext as DatabaseExplorerViewModel;
                if (vm != null && vm.ExecuteQueryCommand.CanExecute(null))
                {
                    vm.ExecuteQueryCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}
