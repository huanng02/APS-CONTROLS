using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe.ViewModels
{
    public class DepartmentViewModel : BaseViewModel
    {
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public ObservableCollection<Department> Items { get; } = new ObservableCollection<Department>();
        public ObservableCollection<Company> Companies { get; } = new ObservableCollection<Company>();

        private Department? _selectedItem;
        public Department? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        private Company? _selectedCompanyFilter;
        public Company? SelectedCompanyFilter
        {
            get => _selectedCompanyFilter;
            set
            {
                _selectedCompanyFilter = value;
                OnPropertyChanged();
                Load();
            }
        }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }

        public DepartmentViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(param => Edit(param));
            DeleteCommand = new RelayCommand(param => Delete(param));

            LoadCompanies();
            Load();
        }

        private void LoadCompanies()
        {
            try
            {
                Companies.Clear();
                var list = _service.GetAllCompanies();
                foreach (var c in list)
                {
                    Companies.Add(c);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading companies in DeptVM: " + ex.Message);
            }
        }

        public void Load()
        {
            try
            {
                // Reload companies list in dropdown to keep it synchronized
                LoadCompanies();

                int? filterId = SelectedCompanyFilter?.Id;
                if (filterId == 0) filterId = null; // "All" fallback

                var list = _service.GetDepartments(filterId);
                Items.Clear();
                foreach (var item in list)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách phòng ban: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Add()
        {
            var dept = new Department();
            // Autofill company if filter is active
            if (SelectedCompanyFilter != null && SelectedCompanyFilter.Id > 0)
            {
                dept.CompanyId = SelectedCompanyFilter.Id;
            }

            var dialog = new DepartmentDialog(dept)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                Load();
            }
        }

        private void Edit(object? param)
        {
            var dept = param as Department ?? SelectedItem;
            if (dept == null)
            {
                MessageBox.Show("Vui lòng chọn phòng ban cần chỉnh sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var clone = new Department
            {
                Id = dept.Id,
                CompanyId = dept.CompanyId,
                DepartmentName = dept.DepartmentName,
                Status = dept.Status,
                IsDeleted = dept.IsDeleted,
                CompanyName = dept.CompanyName
            };

            var dialog = new DepartmentDialog(clone)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                Load();
            }
        }

        private void Delete(object? param)
        {
            var dept = param as Department ?? SelectedItem;
            if (dept == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa phòng ban '{dept.DepartmentName}'?", 
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                if (_service.DeleteDepartment(dept.Id, out string error))
                {
                    MessageBox.Show("Xóa phòng ban thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Load();
                }
                else
                {
                    MessageBox.Show(error, "Lỗi xóa dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
