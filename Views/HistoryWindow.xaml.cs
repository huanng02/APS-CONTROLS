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
    }
}