using System.Windows.Controls;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views.Components
{
    public partial class ConnectivityStatusChip : UserControl
    {
        public ConnectivityStatusChip()
        {
            InitializeComponent();
            // Bind trực tiếp tới Singleton service để realtime update mà không cần thông qua ViewModel trung gian
            this.DataContext = ConnectivityStateService.Instance;
        }
    }
}
