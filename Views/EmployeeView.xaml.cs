using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class EmployeeView : UserControl
    {
        private readonly EmployeeImportService _importSvc = new EmployeeImportService();

        public EmployeeView()
        {
            InitializeComponent();
        }

        private void OpenImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new EmployeeImportWindow { Owner = Application.Current.MainWindow };
            var vm = DataContext as EmployeeViewModel;
            if (vm != null)
            {
                if (vm.SelectedCompanyFilter != null && vm.SelectedCompanyFilter.Id > 0)
                {
                    dlg.DefaultCompanyId = vm.SelectedCompanyFilter.Id;
                }
                if (vm.SelectedDepartmentFilter != null && vm.SelectedDepartmentFilter.Id > 0)
                {
                    dlg.DefaultDepartmentId = vm.SelectedDepartmentFilter.Id;
                }
            }

            if (dlg.ShowDialog() == true)
            {
                vm?.Load();
            }
        }

        private void DownloadTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var dlg = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"Mau_Import_Nhan_Vien_{timestamp}.xlsx",
                    Title = "Chọn nơi lưu file mẫu import nhân sự"
                };

                if (dlg.ShowDialog() == true)
                {
                    int? defaultCompanyId = null;
                    int? defaultDepartmentId = null;
                    var vm = DataContext as EmployeeViewModel;
                    if (vm != null)
                    {
                        if (vm.SelectedCompanyFilter != null && vm.SelectedCompanyFilter.Id > 0)
                        {
                            defaultCompanyId = vm.SelectedCompanyFilter.Id;
                        }
                        if (vm.SelectedDepartmentFilter != null && vm.SelectedDepartmentFilter.Id > 0)
                        {
                            defaultDepartmentId = vm.SelectedDepartmentFilter.Id;
                        }
                    }

                    _importSvc.GenerateTemplateFile(dlg.FileName, defaultCompanyId, defaultDepartmentId);
                    MessageBox.Show("Tải file mẫu import thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo file mẫu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
