using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe.ViewModels
{
    public class EmployeeViewModel : BaseViewModel
    {
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();

        public ObservableCollection<Employee> Items { get; } = new ObservableCollection<Employee>();

        public ObservableCollection<Company> Companies { get; } = new ObservableCollection<Company>();
        public ObservableCollection<Department> Departments { get; } = new ObservableCollection<Department>();
        public ObservableCollection<Position> Positions { get; } = new ObservableCollection<Position>();

        private Employee? _selectedItem;
        public Employee? SelectedItem
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

        private Company? _selectedCompanyFilter;
        public Company? SelectedCompanyFilter
        {
            get => _selectedCompanyFilter;
            set
            {
                _selectedCompanyFilter = value;
                OnPropertyChanged();
                CurrentPage = 0;
                LoadFilterDepartments();
                Load();
            }
        }

        private Department? _selectedDepartmentFilter;
        public Department? SelectedDepartmentFilter
        {
            get => _selectedDepartmentFilter;
            set
            {
                _selectedDepartmentFilter = value;
                OnPropertyChanged();
                CurrentPage = 0;
                Load();
            }
        }

        private Position? _selectedPositionFilter;
        public Position? SelectedPositionFilter
        {
            get => _selectedPositionFilter;
            set
            {
                _selectedPositionFilter = value;
                OnPropertyChanged();
                CurrentPage = 0;
                Load();
            }
        }

        // Paging properties
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
        public ICommand AddCardCommand { get; }

        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public EmployeeViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            AddCommand = new RelayCommand(_ => Add());
            EditCommand = new RelayCommand(param => Edit(param));
            DeleteCommand = new RelayCommand(param => Delete(param));
            ViewDetailCommand = new RelayCommand(param => ViewDetail(param));
            AddCardCommand = new RelayCommand(param => AddCard(param));

            FirstPageCommand = new RelayCommand(_ => { CurrentPage = 0; Load(); }, _ => CurrentPage > 0);
            PrevPageCommand = new RelayCommand(_ => { CurrentPage--; Load(); }, _ => CurrentPage > 0);
            NextPageCommand = new RelayCommand(_ => { CurrentPage++; Load(); }, _ => CurrentPage < TotalPages - 1);
            LastPageCommand = new RelayCommand(_ => { CurrentPage = TotalPages - 1; Load(); }, _ => CurrentPage < TotalPages - 1);

            LoadStaticFilterData();
            Load();
        }

        private void LoadStaticFilterData()
        {
            try
            {
                Companies.Clear();
                Companies.Add(new Company { Id = 0, Name = "All Companies" });
                var comps = _service.GetAllCompanies();
                foreach (var c in comps) Companies.Add(c);

                Positions.Clear();
                Positions.Add(new Position { Id = 0, PositionName = "All Positions" });
                var poss = _service.GetPositions();
                foreach (var p in poss) Positions.Add(p);

                LoadFilterDepartments();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading static filter data in EmpVM: " + ex.Message);
            }
        }

        private void LoadFilterDepartments()
        {
            try
            {
                Departments.Clear();
                Departments.Add(new Department { Id = 0, DepartmentName = "All Departments" });

                int? companyId = SelectedCompanyFilter?.Id;
                if (companyId == 0) companyId = null;

                var depts = _service.GetDepartments(companyId);
                foreach (var d in depts) Departments.Add(d);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading departments in EmpVM: " + ex.Message);
            }
        }

        public void Load()
        {
            try
            {
                int? companyId = SelectedCompanyFilter?.Id == 0 ? null : SelectedCompanyFilter?.Id;
                int? departmentId = SelectedDepartmentFilter?.Id == 0 ? null : SelectedDepartmentFilter?.Id;
                int? positionId = SelectedPositionFilter?.Id == 0 ? null : SelectedPositionFilter?.Id;

                var list = _service.GetEmployeesPaged(SearchText, companyId, departmentId, positionId, StatusFilter, CurrentPage, PageSize, out int total);
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
                MessageBox.Show($"Lỗi tải danh sách nhân viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Add()
        {
            var dialog = new EmployeeDialog(new Employee())
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
            var emp = param as Employee ?? SelectedItem;
            if (emp == null)
            {
                MessageBox.Show("Vui lòng chọn nhân viên cần chỉnh sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var clone = new Employee
            {
                Id = emp.Id,
                EmployeeCode = emp.EmployeeCode,
                FullName = emp.FullName,
                CompanyId = emp.CompanyId,
                PositionId = emp.PositionId,
                Phone = emp.Phone,
                Email = emp.Email,
                CCCD = emp.CCCD,
                Avatar = emp.Avatar,
                DepartmentId = emp.DepartmentId,
                Status = emp.Status,
                IsDeleted = emp.IsDeleted
            };

            var dialog = new EmployeeDialog(clone)
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
            var emp = param as Employee ?? SelectedItem;
            if (emp == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa nhân viên '{emp.FullName}' và vô hiệu hóa thẻ RFID liên kết?", 
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    _service.DeleteEmployee(emp.Id);
                    MessageBox.Show("Xóa nhân viên thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Load();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xóa nhân viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewDetail(object? param)
        {
            var emp = param as Employee ?? SelectedItem;
            if (emp == null) return;

            // Fetch complete employee details from service
            var detailed = _service.GetEmployeeById(emp.Id);
            if (detailed == null) return;

            var dialog = new EmployeeDetailDialog(detailed)
            {
                Owner = Application.Current.MainWindow
            };
            dialog.ShowDialog();
            
            // Reload list in case cards were updated/assigned during detail view
            Load();
        }

        private void AddCard(object? param)
        {
            var emp = param as Employee ?? SelectedItem;
            if (emp == null) return;

            var vm = new RFIDCardWizardViewModel();
            vm.InitForAdd(emp.Id);
            // Pre-select monthly tab (Index 1) for employees
            vm.ActiveTabIndex = 1;

            var dlg = new RFIDCardAddEditWindow(null)
            {
                Owner = Application.Current.MainWindow
            };
            dlg.DataContext = vm;
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var toAdd = new Models.RFIDCards
                    {
                        CardUID = vm.CardUID,
                        CardName = vm.CardName,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId ?? 0,
                        LoaiVeId = vm.LoaiVeId ?? 0,
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai,
                        EmployeeId = vm.EmployeeId
                    };
                    new RFIDCardService().Add(toAdd);
                    MessageBox.Show("Đăng ký thẻ nhân viên thành công", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    Load();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Thêm thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
