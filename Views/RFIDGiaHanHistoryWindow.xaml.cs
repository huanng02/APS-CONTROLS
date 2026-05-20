using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for RFIDGiaHanHistoryWindow.xaml
    /// </summary>
    public partial class RFIDGiaHanHistoryWindow : Window
    {
        public RFIDGiaHanHistoryWindow()
        {
            InitializeComponent();
            this.DataContext = new RFIDGiaHanHistoryViewModel();
        }
    }
}
