using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for PersonnelExplorerView.xaml
    /// </summary>
    public partial class PersonnelExplorerView : UserControl
    {
        private readonly EmployeeImportService _importSvc = new EmployeeImportService();

        public PersonnelExplorerView()
        {
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is PersonnelExplorerViewModel viewModel)
            {
                viewModel.SelectedNode = e.NewValue as PersonnelTreeNode;
            }
        }

        private void OpenImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new EmployeeImportWindow { Owner = Application.Current.MainWindow };
            var vm = DataContext as PersonnelExplorerViewModel;
            if (vm != null && vm.SelectedNode != null)
            {
                if (vm.SelectedNode.NodeType == "Department" && vm.SelectedNode.DataItem is Department dept)
                {
                    dlg.DefaultCompanyId = dept.CompanyId;
                    dlg.DefaultDepartmentId = dept.Id;
                }
                else if (vm.SelectedNode.NodeType == "Company" && vm.SelectedNode.DataItem is Company comp)
                {
                    dlg.DefaultCompanyId = comp.Id;
                }
            }

            if (dlg.ShowDialog() == true)
            {
                if (vm != null && vm.RefreshCommand.CanExecute(null))
                {
                    vm.RefreshCommand.Execute(null);
                }
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
                    var vm = DataContext as PersonnelExplorerViewModel;
                    if (vm != null && vm.SelectedNode != null)
                    {
                        if (vm.SelectedNode.NodeType == "Department" && vm.SelectedNode.DataItem is Department dept)
                        {
                            defaultCompanyId = dept.CompanyId;
                            defaultDepartmentId = dept.Id;
                        }
                        else if (vm.SelectedNode.NodeType == "Company" && vm.SelectedNode.DataItem is Company comp)
                        {
                            defaultCompanyId = comp.Id;
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
