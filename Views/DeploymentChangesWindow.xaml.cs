using System;
using System.Windows;
using System.Windows.Media;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class DeploymentChangesWindow : Window
    {
        private readonly DeploymentRecord _record;

        public DeploymentChangesWindow(DeploymentRecord record)
        {
            InitializeComponent();
            _record = record;
            
            // Set Owner to MainWindow safely
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                this.Owner = Application.Current.MainWindow;
            }

            Loaded += DeploymentChangesWindow_Loaded;
        }

        private async void DeploymentChangesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate basic info
            txtTitle.Text = $"CHI TIẾT THAY ĐỔI PHIÊN BẢN V{_record.Version}";
            txtDeployer.Text = string.IsNullOrEmpty(_record.DeployBy) ? "N/A" : _record.DeployBy;
            txtDeployTime.Text = _record.DeployTime.ToString("dd/MM/yyyy HH:mm:ss");
            txtNotes.Text = string.IsNullOrEmpty(_record.Notes) ? "Không có ghi chú" : _record.Notes;
            txtStatus.Text = _record.Status;

            // Style status badge
            if (string.Equals(_record.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                brdStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(27, 51, 34)); // #1B3322
                txtStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113)); // #2ECC71 (green)
            }
            else
            {
                brdStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 27, 27)); // #331B1B
                txtStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // #E74C3C (red)
            }

            try
            {
                // Fetch changes
                var changes = await DeploymentService.Instance.GetChangesForDeploymentAsync(_record);
                
                if (changes == null || changes.Count == 0)
                {
                    txtEmptyState.Visibility = Visibility.Visible;
                    lstChanges.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtEmptyState.Visibility = Visibility.Collapsed;
                    lstChanges.Visibility = Visibility.Visible;
                    lstChanges.ItemsSource = changes;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DeploymentChangesWindow", "Loaded", "Error loading changes for deployment history detail", ex);
                txtEmptyState.Text = $"Lỗi khi tải dữ liệu: {ex.Message}";
                txtEmptyState.Visibility = Visibility.Visible;
                lstChanges.Visibility = Visibility.Collapsed;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
