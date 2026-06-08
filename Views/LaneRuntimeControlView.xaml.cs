using System.Windows.Controls;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class LaneRuntimeControlView : UserControl
    {
        private LaneRuntimeControlViewModel _vm;

        public LaneRuntimeControlView()
        {
            InitializeComponent();
            _vm = new LaneRuntimeControlViewModel();
            this.DataContext = _vm;
            
            // Clean up when unloaded
            this.Unloaded += (s, e) => {
                _vm?.Cleanup();
            };
        }
    }
}
