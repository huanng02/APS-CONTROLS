using System.Windows;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class LaneRuntimeControlWindow : Window
    {
        public LaneRuntimeControlWindow()
        {
            InitializeComponent();
            this.DataContext = new LaneRuntimeControlViewModel();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (this.DataContext is LaneRuntimeControlViewModel vm)
            {
                vm.Cleanup();
            }
            base.OnClosed(e);
        }
    }
}
