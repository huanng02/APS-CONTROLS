#if DEBUG
using System.Windows;
using QuanLyGiuXe.DebugTools.ViewModels;

namespace QuanLyGiuXe.DebugTools.Views
{
    public partial class DebugToolsView : Window
    {
        public DebugToolsView()
        {
            InitializeComponent();
            DataContext = new DebugToolsViewModel();
        }
    }
}
#endif
