using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe
{
    public partial class HistoryWindow : Window
    {
        private readonly LichSuViewModel VM;

        public HistoryWindow()
        {
            InitializeComponent();
            VM = new LichSuViewModel();
            DataContext = VM;
        }

        private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.DataGridRow row && row.Item is Models.LichSuXe)
            {
                var dialog = new Views.LichSuXeDetailWindow
                {
                    Owner = this,
                    DataContext = this.DataContext
                };
                dialog.ShowDialog();
            }
        }
    }
}