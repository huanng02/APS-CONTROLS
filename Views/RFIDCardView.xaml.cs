using System.Windows;
using System.Windows.Controls;

namespace QuanLyGiuXe.Views
{
    public partial class RFIDCardView : UserControl
    {
        public RFIDCardView()
        {
            InitializeComponent();
        }

        private void OpenImportExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RFIDImportWindow { Owner = Application.Current.MainWindow };
            try
            {
                if (this.DataContext is ViewModels.RFIDCardViewModel vm && vm.SelectedTab != null)
                {
                    // pass selected LoaiVe id to import window (0 or null means 'All')
                    dlg.ActiveLoaiVeId = vm.SelectedTab.Id > 0 ? vm.SelectedTab.Id : (int?)null;
                }
            }
            catch { }
            var res = dlg.ShowDialog();
            if (res == true)
            {
                if (this.DataContext is ViewModels.RFIDCardViewModel vm)
                {
                    vm.LoadCommand.Execute(null);
                }
            }
        }
    }
}
