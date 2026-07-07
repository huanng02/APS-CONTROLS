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
    public partial class EmployeeImportWindow : Window
    {
        private string _path = string.Empty;
        private readonly EmployeeImportService _svc = new EmployeeImportService();
        private List<EmployeeImportPreviewRow> _preview = new List<EmployeeImportPreviewRow>();

        public int? DefaultCompanyId { get; set; }
        public int? DefaultDepartmentId { get; set; }

        public EmployeeImportWindow()
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
                MessageBox.Show($"Không thể mở hộp thoại chọn file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_path) || !System.IO.File.Exists(_path))
            {
                MessageBox.Show("Vui lòng chọn file Excel trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PreviewGrid.ItemsSource = null;
            ImportProgressBar.Value = 0;
            TxtStats.Text = "Đang tải dữ liệu...";

            await Task.Run(() =>
            {
                _preview = _svc.PreviewFromExcel(_path, DefaultCompanyId, DefaultDepartmentId);
            });

            PreviewGrid.ItemsSource = _preview;
            int total = _preview.Count;
            int ok = _preview.Count(x => x.Status == "OK" || x.Status.Contains("Exists") || x.Status.Contains("AutoFix"));
            int err = _preview.Count(x => x.Status == "Error");
            int auto = _preview.Count(x => x.Status.Contains("AutoFix"));
            TxtStats.Text = $"Tổng số dòng={total} | Hợp lệ={ok} | Tự sửa={auto} | Lỗi={err}";
            ImportProgressBar.Value = 100;
        }

        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (_preview == null || !_preview.Any())
            {
                MessageBox.Show("Chưa có dữ liệu preview. Vui lòng Xem trước trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var okCount = _preview.Count(x => !x.Status.StartsWith("Error"));
            if (okCount == 0)
            {
                MessageBox.Show("Không có dòng nào hợp lệ để nhập vào hệ thống.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show($"Bắt đầu nhập {okCount} dòng hợp lệ vào hệ thống?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ImportProgressBar.Value = 0;
            TxtStats.Text = "Đang nhập dữ liệu...";

            int inserted = 0;
            int updated = 0;

            try
            {
                await Task.Run(() =>
                {
                    _svc.Import(_preview, out inserted, out updated, DefaultCompanyId, DefaultDepartmentId);
                });

                ImportProgressBar.Value = 100;
                TxtStats.Text = $"Nhập hoàn tất. Thêm mới={inserted} | Cập nhật={updated}";
                MessageBox.Show($"Nhập hoàn tất thành công!\n- Thêm mới: {inserted} nhân viên\n- Cập nhật: {updated} nhân viên", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ImportProgressBar.Value = 0;
                TxtStats.Text = "Lỗi nhập dữ liệu";
                MessageBox.Show($"Gặp lỗi khi lưu vào cơ sở dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
