using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class BangGiaView : UserControl
    {
        public BangGiaView()
        {
            InitializeComponent();
            // quick debug to confirm this view is loaded
            // MessageBox.Show("BangGiaAdminView LOADED");

            // attach auto-save on cell edit
            dgBangGia.CellEditEnding += DgBangGia_CellEditEnding;
            // also listen to row edit ending to mark dirty without saving immediately
            dgBangGia.RowEditEnding += DgBangGia_RowEditEnding;
        }

        private void DgBangGia_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (DataContext is BangGiaAdminViewModel vm)
            {
                // the row being edited is the DataContext of the row
                if (e.Row.Item is Models.BangGia bg)
                {
                    // mark edited and do not auto-save immediately; user can Save all
                    vm.MarkRowEdited(bg);
                    // optionally auto-save single cell: comment/uncomment as needed
                    // vm.SaveCommand.Execute(bg);
                }
            }
        }

        private void DgBangGia_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (DataContext is BangGiaAdminViewModel vm)
            {
                if (e.Row.Item is Models.BangGia bg)
                {
                    vm.MarkRowEdited(bg);
                }
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is BangGiaAdminViewModel vm)
            {
                vm.SaveCommand.Execute(null);
            }
        }
    }
}
