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

        // ======================
        // PAGING BUTTONS
        // ======================

        private void TrangTruoc_Click(object sender, RoutedEventArgs e)
        {
            VM.TrangTruoc();
        }

        private void TrangSau_Click(object sender, RoutedEventArgs e)
        {
            VM.TrangSau();
        }

        private void TrangDau_Click(object sender, RoutedEventArgs e)
        {
            VM.TrangDau();
        }

        private void TrangCuoi_Click(object sender, RoutedEventArgs e)
        {
            VM.TrangCuoi();
        }

        // ======================
        // FILTER ACTIONS
        // ======================

        private void Loc_Click(object sender, RoutedEventArgs e)
        {
            VM.LoadTrang();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            VM.ResetFilter();
        }
    }
}