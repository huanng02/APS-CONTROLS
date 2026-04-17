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
