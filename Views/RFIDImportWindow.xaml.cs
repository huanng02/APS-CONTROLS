using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class RFIDImportWindow : Window
    {
        private string _path;
        private readonly ImportExportService _svc = new ImportExportService();
        private List<ImportPreviewRow> _preview = new List<ImportPreviewRow>();
        // If set (>0), import will be treated as coming from a specific LoaiVe tab
        public int? ActiveLoaiVeId { get; set; }

        public RFIDImportWindow()
        {
            InitializeComponent();
        }

        private void BtnChoose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog();
                dlg.Filter = "Excel Files|*.xlsx;*.xls";
                bool? ok = dlg.ShowDialog();
                if (ok == true)
                {
                    _path = dlg.FileName;
                    TxtPath.Text = _path;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Không thể mở dialog: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_path) || !System.IO.File.Exists(_path))
            {
                System.Windows.MessageBox.Show("Vui lòng chọn file Excel trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PreviewGrid.ItemsSource = null;
            ProgressBar.Value = 0;
            TxtStats.Text = "Loading...";

            await Task.Run(() => { _preview = _svc.PreviewFromExcel(_path, ActiveLoaiVeId); });

            PreviewGrid.ItemsSource = _preview;
            int total = _preview.Count;
            int ok = _preview.Count(x => x.Status == "OK");
            int err = _preview.Count(x => x.Status.StartsWith("Error"));
            int auto = _preview.Count(x => x.Status.Contains("AutoFix"));
            TxtStats.Text = $"Total={total} | OK={ok} | AutoFix={auto} | Error={err}";
            ProgressBar.Value = 100;
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (_preview == null || !_preview.Any())
            {
                System.Windows.MessageBox.Show("Chưa có dữ liệu preview. Vui lòng Preview trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = System.Windows.MessageBox.Show("Bắt đầu import những dòng hợp lệ?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ProgressBar.Value = 0;
            TxtStats.Text = "Importing...";

            (int Inserted, int Updated) result = (0, 0);
            int toImport = 0;
            await Task.Run(() =>
            {
                var dt = _svc.BuildDataTableForBulk(_preview, updateExisting: true);
                toImport = dt.Rows.Count;
                if (dt.Rows.Count > 0)
                {
                    result = _svc.BulkImport(dt, updateExisting: true);
                    System.Diagnostics.Debug.WriteLine($"Bulk import result: Inserted={result.Inserted}, Updated={result.Updated}");
                }
            });

            ProgressBar.Value = 100;
            TxtStats.Text = $"Import completed. ToImport={toImport} | Inserted={result.Inserted} | Updated={result.Updated}";
            System.Windows.MessageBox.Show($"Import completed. Inserted={result.Inserted}, Updated={result.Updated}, ToImport={toImport}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

            // keep window open if nothing imported so user can inspect preview errors
            if (result.Inserted == 0 && result.Updated == 0 && toImport > 0)
            {
                // show possible reasons
                System.Windows.MessageBox.Show("Không có hàng nào được chèn/ cập nhật. Kiểm tra cột LoaiXe/LoaiVe mapping và thông báo lỗi trong preview.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}
