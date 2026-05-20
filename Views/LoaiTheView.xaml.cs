using QuanLyGiuXe.ViewModels;
using System.Windows.Controls;

namespace QuanLyGiuXe.Views
{
    public partial class LoaiTheView : UserControl
    {
        public LoaiTheView()
        {
            InitializeComponent();
            DataContext = new LoaiTheViewModel();
        }
    }
}
