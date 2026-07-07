using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe.ViewModels
{
    public class CompanyViewModel : BaseViewModel
    {
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public ObservableCollection<Company> Items { get; } = new ObservableCollection<Company>();

        private Company? _selectedItem;
        public Company? SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                CurrentPage = 0;
                Load();
            }
        }

        private string _statusFilter = "Tất cả";
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                _statusFilter = value;
                OnPropertyChanged();
                CurrentPage = 0;
                Load();
            }
        }

        private int _currentPage = 0;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentPageDisplay));
            }
        }

        public int CurrentPageDisplay => CurrentPage + 1;

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                _totalPages = value;
                OnPropertyChanged();
            }
        }

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = value;
                OnPropertyChanged();
                CurrentPage = 0;
                Load();
            }
        }

        private int _totalRecords = 0;
        public int TotalRecords
        {
            get => _totalRecords;
            set
            {
                _totalRecords = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<int> PageSizeOptions { get; } = new ObservableCollection<int> { 5, 10, 20, 50 };

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ViewDetailCommand { get; }

        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public CompanyViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(param => Edit(param));
            DeleteCommand = new RelayCommand(param => Delete(param));
            ViewDetailCommand = new RelayCommand(param => ViewDetail(param));

            FirstPageCommand = new RelayCommand(_ => { CurrentPage = 0; Load(); }, _ => CurrentPage > 0);
            PrevPageCommand = new RelayCommand(_ => { CurrentPage--; Load(); }, _ => CurrentPage > 0);
            NextPageCommand = new RelayCommand(_ => { CurrentPage++; Load(); }, _ => CurrentPage < TotalPages - 1);
            LastPageCommand = new RelayCommand(_ => { CurrentPage = TotalPages - 1; Load(); }, _ => CurrentPage < TotalPages - 1);

            Load();
        }

        public void Load()
        {
            try
            {
                var list = _service.GetCompaniesPaged(SearchText, StatusFilter, CurrentPage, PageSize, out int total);
                TotalRecords = total;
                TotalPages = (int)Math.Ceiling((double)total / PageSize);
                if (TotalPages == 0) TotalPages = 1;

                if (CurrentPage >= TotalPages)
                {
                    CurrentPage = TotalPages - 1;
                    if (CurrentPage < 0) CurrentPage = 0;
                }

                Items.Clear();
                foreach (var item in list)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải danh sách công ty: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Add()
        {
            var dialog = new CompanyDialog(new Company())
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
            var company = param as Company ?? SelectedItem;
            if (company == null)
            {
                MessageBox.Show("Vui lòng chọn công ty cần chỉnh sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create a clone to edit
            var clone = new Company
            {
                Id = company.Id,
                Code = company.Code,
                Name = company.Name,
                Address = company.Address,
                Phone = company.Phone,
                Status = company.Status,
                IsDeleted = company.IsDeleted
            };

            var dialog = new CompanyDialog(clone)
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
            var company = param as Company ?? SelectedItem;
            if (company == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa công ty '{company.Name}'?", 
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                if (_service.DeleteCompany(company.Id, out string error))
                {
                    MessageBox.Show("Xóa công ty thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Load();
                }
                else
                {
                    MessageBox.Show(error, "Lỗi xóa dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ViewDetail(object? param)
        {
            var company = param as Company ?? SelectedItem;
            if (company == null) return;

            var detailed = _service.GetCompanyById(company.Id);
            if (detailed == null) return;

            var depts = _service.GetDepartments(company.Id);

            // Construct list details popup
            var deptsAndEmpsMsg = "";
            if (depts.Count == 0)
            {
                deptsAndEmpsMsg = "Không có phòng ban nào.";
            }
            else
            {
                deptsAndEmpsMsg = "Danh sách phòng ban:\n";
                foreach (var d in depts)
                {
                    deptsAndEmpsMsg += $"- {d.DepartmentName} ({d.Status})\n";
                }
            }

            MessageBox.Show($"[CHI TIẾT CÔNG TY]\n\nMã: {detailed.Code}\nTên: {detailed.Name}\nĐịa chỉ: {detailed.Address ?? "(N/A)"}\nĐiện thoại: {detailed.Phone ?? "(N/A)"}\nTrạng thái: {detailed.Status}\n\n{deptsAndEmpsMsg}",
                $"Chi tiết: {detailed.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
