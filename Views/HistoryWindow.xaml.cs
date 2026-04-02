using System.Windows;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe
{
    /// <summary>
    /// Interaction logic for HistoryWindow.xaml
    /// </summary>
    public partial class HistoryWindow : Window
    {
        DatabaseService db = new DatabaseService();
        public HistoryWindow()
        {
            InitializeComponent();
            DataContext = new LichSuViewModel();
        }
    }
}
