using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.Views;

namespace QuanLyGiuXe.ViewModels
{
    public class PersonnelExplorerViewModel : BaseViewModel
    {
        private readonly EnterpriseCrudService _enterpriseService = new();
        private readonly RFIDCardService _cardService = new();

        // ──────────────────────────────────────────────
        // Collections
        // ──────────────────────────────────────────────
        public ObservableCollection<Company> Companies { get; } = new();
        public ObservableCollection<Department> Departments { get; } = new();
        public ObservableCollection<Position> Positions { get; } = new();
        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<RFIDCards> Cards { get; } = new();

        public ObservableCollection<PersonnelTreeNode> TreeNodes { get; } = new();

        // ──────────────────────────────────────────────
        // Selection State
        // ──────────────────────────────────────────────
        private PersonnelTreeNode? _selectedNode;
        public PersonnelTreeNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsCompanySelected));
                OnPropertyChanged(nameof(IsDepartmentSelected));
                OnPropertyChanged(nameof(IsPositionSelected));
                OnPropertyChanged(nameof(IsEmployeeSelected));
                OnPropertyChanged(nameof(IsCardSelected));
                OnPropertyChanged(nameof(IsOrphanGroupSelected));
                OnPropertyChanged(nameof(IsEmployeeListVisible));
                OnPropertyChanged(nameof(SelectedNodeType));
                
                UpdateSelectedItemDetails();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set { _isEmpty = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterTree();
            }
        }

        // Selected Node Checkers
        public bool HasSelection => SelectedNode != null;
        public bool IsCompanySelected => SelectedNode?.NodeType == "Company";
        public bool IsDepartmentSelected => SelectedNode?.NodeType == "Department";
        public bool IsPositionSelected => SelectedNode?.NodeType == "Position";
        public bool IsEmployeeSelected => SelectedNode?.NodeType == "Employee";
        public bool IsCardSelected => SelectedNode?.NodeType == "Card";
        public bool IsOrphanGroupSelected => SelectedNode?.NodeType == "OrphanGroup";
        public bool IsEmployeeListVisible => IsDepartmentSelected || IsOrphanGroupSelected;
        public string SelectedNodeType => SelectedNode?.NodeType ?? string.Empty;

        // ──────────────────────────────────────────────
        // Detail panel properties
        // ──────────────────────────────────────────────

        // Company Details
        private string _detailCompanyName = string.Empty;
        public string DetailCompanyName { get => _detailCompanyName; set { _detailCompanyName = value; OnPropertyChanged(); } }
        private string _detailCompanyCode = string.Empty;
        public string DetailCompanyCode { get => _detailCompanyCode; set { _detailCompanyCode = value; OnPropertyChanged(); } }
        private string _detailCompanyAddress = string.Empty;
        public string DetailCompanyAddress { get => _detailCompanyAddress; set { _detailCompanyAddress = value; OnPropertyChanged(); } }
        private string _detailCompanyPhone = string.Empty;
        public string DetailCompanyPhone { get => _detailCompanyPhone; set { _detailCompanyPhone = value; OnPropertyChanged(); } }
        private string _detailCompanyStatus = string.Empty;
        public string DetailCompanyStatus { get => _detailCompanyStatus; set { _detailCompanyStatus = value; OnPropertyChanged(); } }
        private int _detailCompanyDeptsCount;
        public int DetailCompanyDeptsCount { get => _detailCompanyDeptsCount; set { _detailCompanyDeptsCount = value; OnPropertyChanged(); } }
        private int _detailCompanyEmpsCount;
        public int DetailCompanyEmpsCount { get => _detailCompanyEmpsCount; set { _detailCompanyEmpsCount = value; OnPropertyChanged(); } }

        // Department Details
        private string _detailDeptName = string.Empty;
        public string DetailDeptName { get => _detailDeptName; set { _detailDeptName = value; OnPropertyChanged(); } }
        private string _detailDeptCompanyName = string.Empty;
        public string DetailDeptCompanyName { get => _detailDeptCompanyName; set { _detailDeptCompanyName = value; OnPropertyChanged(); } }
        private string _detailDeptStatus = string.Empty;
        public string DetailDeptStatus { get => _detailDeptStatus; set { _detailDeptStatus = value; OnPropertyChanged(); } }
        private int _detailDeptEmpsCount;
        public int DetailDeptEmpsCount { get => _detailDeptEmpsCount; set { _detailDeptEmpsCount = value; OnPropertyChanged(); } }

        // Department Employee List Properties
        private string _deptEmpSearchText = string.Empty;
        public string DeptEmpSearchText
        {
            get => _deptEmpSearchText;
            set
            {
                _deptEmpSearchText = value;
                OnPropertyChanged();
                FilterDeptEmployees();
            }
        }

        private string _deptEmpStatusFilter = "All";
        public string DeptEmpStatusFilter
        {
            get => _deptEmpStatusFilter;
            set
            {
                _deptEmpStatusFilter = value;
                OnPropertyChanged();
                FilterDeptEmployees();
            }
        }

        private int _deptEmpPositionFilter;
        public int DeptEmpPositionFilter
        {
            get => _deptEmpPositionFilter;
            set
            {
                _deptEmpPositionFilter = value;
                OnPropertyChanged();
                FilterDeptEmployees();
            }
        }

        public ObservableCollection<Position> DeptEmpPositionFilterList { get; } = new();
        public ObservableCollection<Employee> FilteredDeptEmployees { get; } = new();

        // Position Details
        private string _detailPosName = string.Empty;
        public string DetailPosName { get => _detailPosName; set { _detailPosName = value; OnPropertyChanged(); } }
        private string _detailPosStatus = string.Empty;
        public string DetailPosStatus { get => _detailPosStatus; set { _detailPosStatus = value; OnPropertyChanged(); } }
        private int _detailPosEmpsCount;
        public int DetailPosEmpsCount { get => _detailPosEmpsCount; set { _detailPosEmpsCount = value; OnPropertyChanged(); } }

        // Employee Details
        private string _detailEmpCode = string.Empty;
        public string DetailEmpCode { get => _detailEmpCode; set { _detailEmpCode = value; OnPropertyChanged(); } }
        private string _detailEmpName = string.Empty;
        public string DetailEmpName { get => _detailEmpName; set { _detailEmpName = value; OnPropertyChanged(); } }
        private string _detailEmpPhone = string.Empty;
        public string DetailEmpPhone { get => _detailEmpPhone; set { _detailEmpPhone = value; OnPropertyChanged(); } }
        private string _detailEmpEmail = string.Empty;
        public string DetailEmpEmail { get => _detailEmpEmail; set { _detailEmpEmail = value; OnPropertyChanged(); } }
        private string _detailEmpCCCD = string.Empty;
        public string DetailEmpCCCD { get => _detailEmpCCCD; set { _detailEmpCCCD = value; OnPropertyChanged(); } }
        private string _detailEmpStatus = string.Empty;
        public string DetailEmpStatus { get => _detailEmpStatus; set { _detailEmpStatus = value; OnPropertyChanged(); } }
        private string _detailEmpAvatar = string.Empty;
        public string DetailEmpAvatar { get => _detailEmpAvatar; set { _detailEmpAvatar = value; OnPropertyChanged(); } }
        private string _detailEmpCompanyName = string.Empty;
        public string DetailEmpCompanyName { get => _detailEmpCompanyName; set { _detailEmpCompanyName = value; OnPropertyChanged(); } }
        private string _detailEmpDeptName = string.Empty;
        public string DetailEmpDeptName { get => _detailEmpDeptName; set { _detailEmpDeptName = value; OnPropertyChanged(); } }
        private string _detailEmpPositionName = string.Empty;
        public string DetailEmpPositionName { get => _detailEmpPositionName; set { _detailEmpPositionName = value; OnPropertyChanged(); } }
        private bool _detailEmpHasCard;
        public bool DetailEmpHasCard { get => _detailEmpHasCard; set { _detailEmpHasCard = value; OnPropertyChanged(); } }
        private string _detailEmpCardUID = string.Empty;
        public string DetailEmpCardUID { get => _detailEmpCardUID; set { _detailEmpCardUID = value; OnPropertyChanged(); } }
        private string _detailEmpCardStatus = string.Empty;
        public string DetailEmpCardStatus { get => _detailEmpCardStatus; set { _detailEmpCardStatus = value; OnPropertyChanged(); } }
        private string _detailEmpCardExp = string.Empty;
        public string DetailEmpCardExp { get => _detailEmpCardExp; set { _detailEmpCardExp = value; OnPropertyChanged(); } }

        // Card Details
        private string _detailCardUID = string.Empty;
        public string DetailCardUID { get => _detailCardUID; set { _detailCardUID = value; OnPropertyChanged(); } }
        private string _detailCardName = string.Empty;
        public string DetailCardName { get => _detailCardName; set { _detailCardName = value; OnPropertyChanged(); } }
        private string _detailCardPlate = string.Empty;
        public string DetailCardPlate { get => _detailCardPlate; set { _detailCardPlate = value; OnPropertyChanged(); } }
        private string _detailCardLoaiVe = string.Empty;
        public string DetailCardLoaiVe { get => _detailCardLoaiVe; set { _detailCardLoaiVe = value; OnPropertyChanged(); } }
        private string _detailCardLoaiXe = string.Empty;
        public string DetailCardLoaiXe { get => _detailCardLoaiXe; set { _detailCardLoaiXe = value; OnPropertyChanged(); } }
        private string _detailCardStatus = string.Empty;
        public string DetailCardStatus { get => _detailCardStatus; set { _detailCardStatus = value; OnPropertyChanged(); } }
        private string _detailCardCreated = string.Empty;
        public string DetailCardCreated { get => _detailCardCreated; set { _detailCardCreated = value; OnPropertyChanged(); } }
        private string _detailCardExp = string.Empty;
        public string DetailCardExp { get => _detailCardExp; set { _detailCardExp = value; OnPropertyChanged(); } }
        private string _detailCardEmpName = string.Empty;
        public string DetailCardEmpName { get => _detailCardEmpName; set { _detailCardEmpName = value; OnPropertyChanged(); } }
        private string _detailCardEmpCode = string.Empty;
        public string DetailCardEmpCode { get => _detailCardEmpCode; set { _detailCardEmpCode = value; OnPropertyChanged(); } }

        // ──────────────────────────────────────────────
        // Commands
        // ──────────────────────────────────────────────
        public ICommand RefreshCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }

        // Company actions
        public ICommand AddCompanyCommand { get; }
        public ICommand EditCompanyCommand { get; }
        public ICommand DeleteCompanyCommand { get; }

        // Dept actions
        public ICommand AddDeptCommand { get; }
        public ICommand EditDeptCommand { get; }
        public ICommand DeleteDeptCommand { get; }

        // Position actions
        public ICommand AddPosCommand { get; }
        public ICommand EditPosCommand { get; }
        public ICommand DeletePosCommand { get; }

        // Employee actions
        public ICommand AddEmpCommand { get; }
        public ICommand EditEmpCommand { get; }
        public ICommand DeleteEmpCommand { get; }
        public ICommand ViewEmpDetailCommand { get; }

        // Card actions
        public ICommand AddCardCommand { get; }
        public ICommand RenewCardCommand { get; }
        public ICommand UnassignCardCommand { get; }
        public ICommand DeleteCardCommand { get; }

        // Generic context-aware actions
        public ICommand EditSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }

        // ══════════════════════════════════════════════
        // CONSTRUCTOR
        // ══════════════════════════════════════════════
        public PersonnelExplorerViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());
            ExpandAllCommand = new RelayCommand(_ => SetAllExpanded(true));
            CollapseAllCommand = new RelayCommand(_ => SetAllExpanded(false));

            AddCompanyCommand = new RelayCommand(_ => AddCompany());
            EditCompanyCommand = new RelayCommand(p => EditCompany(p as Company), _ => IsCompanySelected || SelectedNode?.DataItem is Company);
            DeleteCompanyCommand = new RelayCommand(p => DeleteCompany(p as Company), _ => IsCompanySelected || SelectedNode?.DataItem is Company);

            AddDeptCommand = new RelayCommand(p => AddDept(p as Company));
            EditDeptCommand = new RelayCommand(p => EditDept(p as Department), _ => IsDepartmentSelected || SelectedNode?.DataItem is Department);
            DeleteDeptCommand = new RelayCommand(p => DeleteDept(p as Department), _ => IsDepartmentSelected || SelectedNode?.DataItem is Department);

            AddPosCommand = new RelayCommand(_ => AddPos());
            EditPosCommand = new RelayCommand(p => EditPos(p as Position), _ => IsPositionSelected || SelectedNode?.DataItem is Position);
            DeletePosCommand = new RelayCommand(p => DeletePos(p as Position), _ => IsPositionSelected || SelectedNode?.DataItem is Position);

            AddEmpCommand = new RelayCommand(p => AddEmp(p as Department));
            EditEmpCommand = new RelayCommand(p => EditEmp(p as Employee), p => p is Employee || IsEmployeeSelected || SelectedNode?.DataItem is Employee);
            DeleteEmpCommand = new RelayCommand(p => DeleteEmp(p as Employee), p => p is Employee || IsEmployeeSelected || SelectedNode?.DataItem is Employee);
            ViewEmpDetailCommand = new RelayCommand(p => ViewEmpDetail(p as Employee), p => p is Employee || IsEmployeeSelected || SelectedNode?.DataItem is Employee);

            AddCardCommand = new RelayCommand(p => AddCard(p as Employee), p => p is Employee || IsEmployeeSelected || SelectedNode?.DataItem is Employee);
            RenewCardCommand = new RelayCommand(p => RenewCard(p), p => p is Employee || p is RFIDCards || IsCardSelected || IsEmployeeSelected || SelectedNode?.DataItem is RFIDCards);
            UnassignCardCommand = new RelayCommand(p => UnassignCard(p), p => p is RFIDCards || IsCardSelected || SelectedNode?.DataItem is RFIDCards);
            DeleteCardCommand = new RelayCommand(p => DeleteCard(p), p => p is RFIDCards || IsCardSelected || SelectedNode?.DataItem is RFIDCards);

            EditSelectedCommand = new RelayCommand(async _ => await EditSelected());
            DeleteSelectedCommand = new RelayCommand(async _ => await DeleteSelected());

            _ = LoadDataAsync();
        }

        // ──────────────────────────────────────────────
        // Load & Tree builder
        // ──────────────────────────────────────────────
        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;

                // Save selection state before reloading
                string? prevType = SelectedNode?.NodeType;
                int prevId = 0;
                if (SelectedNode != null)
                {
                    if (SelectedNode.DataItem is Company c) prevId = c.Id;
                    else if (SelectedNode.DataItem is Department d) prevId = d.Id;
                    else if (SelectedNode.DataItem is Position p) prevId = p.Id;
                    else if (SelectedNode.DataItem is Employee e) prevId = e.Id;
                    else if (SelectedNode.DataItem is RFIDCards rc) prevId = rc.Id;
                }
                
                // Fetch datasets asynchronously or in task pool
                var comps = await Task.Run(() => _enterpriseService.GetAllCompanies());
                var depts = await Task.Run(() => _enterpriseService.GetDepartments(null));
                var poss = await Task.Run(() => _enterpriseService.GetPositions());
                var emps = await Task.Run(() => {
                    return _enterpriseService.GetEmployeesPaged("", null, null, null, "All", 0, 100000, out _);
                });
                var cards = await Task.Run(() => _cardService.GetAll());

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Companies.Clear();
                    foreach (var c in comps) Companies.Add(c);

                    Departments.Clear();
                    foreach (var d in depts) Departments.Add(d);

                    Positions.Clear();
                    foreach (var p in poss) Positions.Add(p);

                    Employees.Clear();
                    foreach (var e in emps) Employees.Add(e);

                    Cards.Clear();
                    foreach (var c in cards) Cards.Add(c);

                    // Populate position filter list
                    DeptEmpPositionFilterList.Clear();
                    DeptEmpPositionFilterList.Add(new Position { Id = 0, PositionName = "Tất cả chức vụ" });
                    foreach (var p in poss) DeptEmpPositionFilterList.Add(p);

                    BuildTreeNodes();
                    IsEmpty = TreeNodes.Count == 0;

                    // Restore selection state
                    if (prevType != null && prevId > 0)
                    {
                        FindAndExpandNode(TreeNodes, prevType, prevId);
                    }

                    // Refresh active selection details and list
                    UpdateSelectedItemDetails();

                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                IsLoading = false;
                MessageBox.Show("Lỗi tải dữ liệu sơ đồ: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildTreeNodes()
        {
            TreeNodes.Clear();

            foreach (var company in Companies)
            {
                var companyNode = new PersonnelTreeNode
                {
                    Name = company.Name,
                    Icon = "🏢",
                    NodeType = "Company",
                    DataItem = company,
                    Badge = company.Code,
                    IsActive = company.Status == "Active",
                    IsExpanded = true
                };

                var companyDepts = Departments.Where(d => d.CompanyId == company.Id).ToList();
                companyNode.Subtitle = $"{companyDepts.Count} phòng ban";

                foreach (var dept in companyDepts)
                {
                    var deptNode = new PersonnelTreeNode
                    {
                        Name = dept.DepartmentName,
                        Icon = "📁",
                        NodeType = "Department",
                        DataItem = dept,
                        IsActive = dept.Status == "Active",
                        IsExpanded = false
                    };

                    var deptEmployees = Employees.Where(e => e.CompanyId == company.Id && e.DepartmentId == dept.Id).ToList();
                    deptNode.Subtitle = $"{deptEmployees.Count} nhân sự";

                    companyNode.Children.Add(deptNode);
                }

                TreeNodes.Add(companyNode);
            }

            // Orphan Employees (unassigned to Company/Department)
            var orphanEmployees = Employees.Where(e => e.CompanyId == 0 || !e.DepartmentId.HasValue || e.DepartmentId == 0).ToList();
            if (orphanEmployees.Any())
            {
                var orphanNode = new PersonnelTreeNode
                {
                    Name = "Nhân sự chưa gán đơn vị",
                    Icon = "⚠",
                    NodeType = "OrphanGroup",
                    IsActive = true,
                    IsExpanded = true,
                    Subtitle = $"{orphanEmployees.Count} nhân sự tự do"
                };

                TreeNodes.Add(orphanNode);
            }
        }

        // ──────────────────────────────────────────────
        // Search & Expander
        // ──────────────────────────────────────────────
        private void FilterTree()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SetAllVisible(TreeNodes, true);
                return;
            }

            var query = SearchText.ToLowerInvariant();
            foreach (var node in TreeNodes)
            {
                FilterNode(node, query);
            }
        }

        private bool FilterNode(PersonnelTreeNode node, string query)
        {
            bool selfMatch = node.Name.ToLowerInvariant().Contains(query) ||
                             (node.Badge?.ToLowerInvariant().Contains(query) ?? false);

            bool childMatch = false;
            foreach (var child in node.Children)
            {
                if (FilterNode(child, query))
                    childMatch = true;
            }

            node.IsVisible = selfMatch || childMatch;
            if (childMatch) node.IsExpanded = true;
            return node.IsVisible;
        }

        private void SetAllVisible(ObservableCollection<PersonnelTreeNode> nodes, bool visible)
        {
            foreach (var node in nodes)
            {
                node.IsVisible = visible;
                SetAllVisible(node.Children, visible);
            }
        }

        private void SetAllExpanded(bool expanded)
        {
            foreach (var node in TreeNodes)
                SetNodeExpanded(node, expanded);
        }

        private void SetNodeExpanded(PersonnelTreeNode node, bool expanded)
        {
            node.IsExpanded = expanded;
            foreach (var child in node.Children)
                SetNodeExpanded(child, expanded);
        }

        // ──────────────────────────────────────────────
        // Detail panel Sync
        // ──────────────────────────────────────────────
        private void UpdateSelectedItemDetails()
        {
            if (SelectedNode == null) return;

            switch (SelectedNode.NodeType)
            {
                case "Company":
                    var comp = SelectedNode.DataItem as Company;
                    if (comp != null)
                    {
                        DetailCompanyName = comp.Name;
                        DetailCompanyCode = comp.Code;
                        DetailCompanyAddress = comp.Address ?? "Chưa thiết lập";
                        DetailCompanyPhone = comp.Phone ?? "Chưa thiết lập";
                        DetailCompanyStatus = comp.Status;
                        DetailCompanyDeptsCount = Departments.Count(d => d.CompanyId == comp.Id);
                        DetailCompanyEmpsCount = Employees.Count(e => e.CompanyId == comp.Id);
                    }
                    break;

                case "Department":
                case "OrphanGroup":
                    if (SelectedNode.NodeType == "Department")
                    {
                        var dept = SelectedNode.DataItem as Department;
                        if (dept != null)
                        {
                            DetailDeptName = dept.DepartmentName;
                            var pComp = Companies.FirstOrDefault(c => c.Id == dept.CompanyId);
                            DetailDeptCompanyName = pComp?.Name ?? "Không rõ";
                            DetailDeptStatus = dept.Status;
                            DetailDeptEmpsCount = Employees.Count(e => e.CompanyId == dept.CompanyId && e.DepartmentId == dept.Id);
                        }
                    }
                    else // OrphanGroup
                    {
                        DetailDeptName = SelectedNode.Name;
                        DetailDeptCompanyName = "Hệ thống";
                        DetailDeptStatus = "Active";
                        DetailDeptEmpsCount = Employees.Count(e => e.CompanyId == 0 || !e.DepartmentId.HasValue || e.DepartmentId == 0);
                    }

                    // Reset department employee filters
                    _deptEmpSearchText = string.Empty;
                    OnPropertyChanged(nameof(DeptEmpSearchText));
                    _deptEmpStatusFilter = "All";
                    OnPropertyChanged(nameof(DeptEmpStatusFilter));
                    _deptEmpPositionFilter = 0;
                    OnPropertyChanged(nameof(DeptEmpPositionFilter));

                    FilterDeptEmployees();
                    break;

                case "Position":
                    var pos = SelectedNode.DataItem as Position;
                    if (pos != null)
                    {
                        DetailPosName = pos.PositionName;
                        DetailPosStatus = pos.Status;
                        DetailPosEmpsCount = Employees.Count(e => e.PositionId == pos.Id);
                    }
                    break;

                case "Employee":
                    var emp = SelectedNode.DataItem as Employee;
                    if (emp != null)
                    {
                        DetailEmpCode = emp.EmployeeCode;
                        DetailEmpName = emp.FullName;
                        DetailEmpPhone = emp.Phone ?? "Chưa cập nhật";
                        DetailEmpEmail = emp.Email ?? "Chưa cập nhật";
                        DetailEmpCCCD = emp.CCCD ?? "Chưa cập nhật";
                        DetailEmpStatus = emp.Status;
                        DetailEmpAvatar = emp.Avatar ?? string.Empty;
                        
                        var ec = Companies.FirstOrDefault(c => c.Id == emp.CompanyId);
                        DetailEmpCompanyName = ec?.Name ?? "Chưa gán";

                        var ed = Departments.FirstOrDefault(d => d.Id == emp.DepartmentId);
                        DetailEmpDeptName = ed?.DepartmentName ?? "Chưa gán";

                        var ep = Positions.FirstOrDefault(p => p.Id == emp.PositionId);
                        DetailEmpPositionName = ep?.PositionName ?? "Chưa gán";

                        var empCard = Cards.FirstOrDefault(c => c.EmployeeId == emp.Id);
                        DetailEmpHasCard = empCard != null;
                        if (empCard != null)
                        {
                            DetailEmpCardUID = empCard.CardUID;
                            DetailEmpCardStatus = empCard.TrangThai;
                            DetailEmpCardExp = empCard.NgayHetHan?.ToString("yyyy-MM-dd HH:mm") ?? "Vĩnh viễn";
                        }
                        else
                        {
                            DetailEmpCardUID = "Chưa cấp";
                            DetailEmpCardStatus = string.Empty;
                            DetailEmpCardExp = string.Empty;
                        }
                    }
                    break;

                case "Card":
                    var card = SelectedNode.DataItem as RFIDCards;
                    if (card != null)
                    {
                        DetailCardUID = card.CardUID;
                        DetailCardName = card.CardName ?? "Thẻ nhân sự";
                        DetailCardPlate = card.BienSo ?? "Chưa gán";
                        DetailCardLoaiVe = card.LoaiVe ?? "Thẻ tháng";
                        DetailCardLoaiXe = card.LoaiXe ?? "Không xác định";
                        DetailCardStatus = card.TrangThai;
                        DetailCardCreated = card.NgayDangKy?.ToString("yyyy-MM-dd HH:mm") ?? "Không rõ";
                        DetailCardExp = card.NgayHetHan?.ToString("yyyy-MM-dd HH:mm") ?? "Vĩnh viễn";
                        
                        DetailCardEmpName = card.EmployeeName ?? "Chưa gán nhân viên";
                        DetailCardEmpCode = card.EmployeeCode ?? string.Empty;
                    }
                    break;
            }
        }

        public void FilterDeptEmployees()
        {
            if (SelectedNode == null)
            {
                FilteredDeptEmployees.Clear();
                return;
            }

            IEnumerable<Employee> query;
            if (SelectedNode.NodeType == "Department" && SelectedNode.DataItem is Department dept)
            {
                query = Employees.Where(e => e.CompanyId == dept.CompanyId && e.DepartmentId == dept.Id);
            }
            else if (SelectedNode.NodeType == "OrphanGroup")
            {
                query = Employees.Where(e => e.CompanyId == 0 || !e.DepartmentId.HasValue || e.DepartmentId == 0);
            }
            else
            {
                FilteredDeptEmployees.Clear();
                return;
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(DeptEmpSearchText))
            {
                var s = DeptEmpSearchText.Trim().ToLower();
                query = query.Where(e => (e.FullName != null && e.FullName.ToLower().Contains(s)) ||
                                         (e.EmployeeCode != null && e.EmployeeCode.ToLower().Contains(s)) ||
                                         (e.Phone != null && e.Phone.Contains(s)) ||
                                         (e.CCCD != null && e.CCCD.Contains(s)));
            }

            // Apply status filter
            if (DeptEmpStatusFilter == "Active")
            {
                query = query.Where(e => e.Status == "Active");
            }
            else if (DeptEmpStatusFilter == "Inactive")
            {
                query = query.Where(e => e.Status == "Inactive");
            }

            // Apply position filter
            if (DeptEmpPositionFilter > 0)
            {
                query = query.Where(e => e.PositionId == DeptEmpPositionFilter);
            }

            FilteredDeptEmployees.Clear();
            foreach (var emp in query)
            {
                var card = Cards.FirstOrDefault(c => c.EmployeeId == emp.Id);
                if (card != null)
                {
                    emp.CardUID = card.CardUID;
                    emp.CardStatus = card.TrangThai;
                    emp.CardExpiration = card.NgayHetHan;
                }
                else
                {
                    emp.CardUID = null;
                    emp.CardStatus = null;
                    emp.CardExpiration = null;
                }
                FilteredDeptEmployees.Add(emp);
            }
        }

        private bool FindAndExpandNode(ObservableCollection<PersonnelTreeNode> nodes, string type, int id)
        {
            foreach (var node in nodes)
            {
                if (node.NodeType == type)
                {
                    int nodeId = 0;
                    if (node.DataItem is Company c) nodeId = c.Id;
                    else if (node.DataItem is Department d) nodeId = d.Id;
                    else if (node.DataItem is Position p) nodeId = p.Id;
                    else if (node.DataItem is Employee e) nodeId = e.Id;
                    else if (node.DataItem is RFIDCards rc) nodeId = rc.Id;

                    if (nodeId == id)
                    {
                        node.IsSelected = true;
                        SelectedNode = node;
                        return true;
                    }
                }
                
                if (FindAndExpandNode(node.Children, type, id))
                {
                    node.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        // ──────────────────────────────────────────────
        // Context-aware CRUD actions
        // ──────────────────────────────────────────────
        private async Task EditSelected()
        {
            if (SelectedNode == null) return;
            switch (SelectedNode.NodeType)
            {
                case "Company":
                    EditCompany(SelectedNode.DataItem as Company);
                    break;
                case "Department":
                    EditDept(SelectedNode.DataItem as Department);
                    break;
                case "Position":
                    EditPos(SelectedNode.DataItem as Position);
                    break;
                case "Employee":
                    EditEmp(SelectedNode.DataItem as Employee);
                    break;
                case "Card":
                    MessageBox.Show("Thao tác sửa thẻ được thực hiện trực tiếp thông qua nhân viên hoặc trang quản lý thẻ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

        private async Task DeleteSelected()
        {
            if (SelectedNode == null) return;
            switch (SelectedNode.NodeType)
            {
                case "Company":
                    DeleteCompany(SelectedNode.DataItem as Company);
                    break;
                case "Department":
                    DeleteDept(SelectedNode.DataItem as Department);
                    break;
                case "Position":
                    DeletePos(SelectedNode.DataItem as Position);
                    break;
                case "Employee":
                    DeleteEmp(SelectedNode.DataItem as Employee);
                    break;
                case "Card":
                    DeleteCard(SelectedNode.DataItem);
                    break;
            }
        }

        // ──────────────────────────────────────────────
        // Company operations
        // ──────────────────────────────────────────────
        private void AddCompany()
        {
            var dlg = new CompanyDialog(new Company()) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void EditCompany(Company? comp)
        {
            var target = comp ?? SelectedNode?.DataItem as Company;
            if (target == null) return;

            var clone = new Company
            {
                Id = target.Id,
                Code = target.Code,
                Name = target.Name,
                Address = target.Address,
                Phone = target.Phone,
                Status = target.Status,
                IsDeleted = target.IsDeleted
            };

            var dlg = new CompanyDialog(clone) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void DeleteCompany(Company? comp)
        {
            var target = comp ?? SelectedNode?.DataItem as Company;
            if (target == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc muốn xóa công ty '{target.Name}'?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                if (_enterpriseService.DeleteCompany(target.Id, out string error))
                {
                    MessageBox.Show("Xóa công ty thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = LoadDataAsync();
                }
                else
                {
                    MessageBox.Show(error, "Lỗi xóa dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ──────────────────────────────────────────────
        // Department operations
        // ──────────────────────────────────────────────
        private void AddDept(Company? parentCompany = null)
        {
            var defaultCompany = parentCompany ?? SelectedNode?.DataItem as Company;
            var newDept = new Department();
            if (defaultCompany != null)
            {
                newDept.CompanyId = defaultCompany.Id;
            }

            var dlg = new DepartmentDialog(newDept) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void EditDept(Department? dept)
        {
            var target = dept ?? SelectedNode?.DataItem as Department;
            if (target == null) return;

            var clone = new Department
            {
                Id = target.Id,
                CompanyId = target.CompanyId,
                DepartmentName = target.DepartmentName,
                Status = target.Status,
                IsDeleted = target.IsDeleted
            };

            var dlg = new DepartmentDialog(clone) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void DeleteDept(Department? dept)
        {
            var target = dept ?? SelectedNode?.DataItem as Department;
            if (target == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc muốn xóa phòng ban '{target.DepartmentName}'?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                if (_enterpriseService.DeleteDepartment(target.Id, out string error))
                {
                    MessageBox.Show("Xóa phòng ban thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = LoadDataAsync();
                }
                else
                {
                    MessageBox.Show(error, "Lỗi xóa dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ──────────────────────────────────────────────
        // Position operations
        // ──────────────────────────────────────────────
        private void AddPos()
        {
            var dlg = new PositionDialog(new Position()) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void EditPos(Position? pos)
        {
            var target = pos ?? SelectedNode?.DataItem as Position;
            if (target == null) return;

            var clone = new Position
            {
                Id = target.Id,
                PositionName = target.PositionName,
                Status = target.Status,
                IsDeleted = target.IsDeleted
            };

            var dlg = new PositionDialog(clone) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void DeletePos(Position? pos)
        {
            var target = pos ?? SelectedNode?.DataItem as Position;
            if (target == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc muốn xóa chức vụ '{target.PositionName}'?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                if (_enterpriseService.DeletePosition(target.Id, out string error))
                {
                    MessageBox.Show("Xóa chức vụ thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = LoadDataAsync();
                }
                else
                {
                    MessageBox.Show(error, "Lỗi xóa dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        // ──────────────────────────────────────────────
        // Employee operations
        // ──────────────────────────────────────────────
        private void AddEmp(Department? parentDept = null)
        {
            var targetDept = parentDept ?? SelectedNode?.DataItem as Department;
            var newEmp = new Employee();
            if (targetDept != null)
            {
                newEmp.CompanyId = targetDept.CompanyId;
                newEmp.DepartmentId = targetDept.Id;
            }

            var dlg = new EmployeeDialog(newEmp) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void EditEmp(Employee? emp)
        {
            var target = emp ?? SelectedNode?.DataItem as Employee;
            if (target == null) return;

            var clone = new Employee
            {
                Id = target.Id,
                EmployeeCode = target.EmployeeCode,
                FullName = target.FullName,
                CompanyId = target.CompanyId,
                PositionId = target.PositionId,
                Phone = target.Phone,
                Email = target.Email,
                CCCD = target.CCCD,
                Avatar = target.Avatar,
                DepartmentId = target.DepartmentId,
                Status = target.Status,
                IsDeleted = target.IsDeleted
            };

            var dlg = new EmployeeDialog(clone) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() == true)
            {
                _ = LoadDataAsync();
            }
        }

        private void DeleteEmp(Employee? emp)
        {
            var target = emp ?? SelectedNode?.DataItem as Employee;
            if (target == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn xóa nhân viên '{target.FullName}' và vô hiệu hóa thẻ RFID liên kết?", 
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    _enterpriseService.DeleteEmployee(target.Id);
                    _ = LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xóa nhân viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ViewEmpDetail(Employee? emp)
        {
            var target = emp ?? SelectedNode?.DataItem as Employee;
            if (target == null) return;

            var detailed = _enterpriseService.GetEmployeeById(target.Id);
            if (detailed == null) return;

            var dlg = new EmployeeDetailDialog(detailed) { Owner = Application.Current.MainWindow };
            dlg.ShowDialog();
            _ = LoadDataAsync();
        }

        // ──────────────────────────────────────────────
        // Card operations
        // ──────────────────────────────────────────────
        private void AddCard(Employee? emp)
        {
            var target = emp ?? SelectedNode?.DataItem as Employee;
            if (target == null) return;

            var vm = new RFIDCardWizardViewModel();
            vm.InitForAdd(target.Id);
            vm.ActiveTabIndex = 1; // monthly card tab

            var dlg = new RFIDCardAddEditWindow(null) { Owner = Application.Current.MainWindow };
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
                    _cardService.Add(toAdd);
                    _ = LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cấp thẻ thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RenewCard(object? param)
        {
            RFIDCards? target = null;
            if (param is RFIDCards card) target = card;
            else if (param is Employee emp) target = Cards.FirstOrDefault(c => c.EmployeeId == emp.Id);
            else if (SelectedNode?.NodeType == "Card" && SelectedNode.DataItem is RFIDCards rc) target = rc;
            else if (SelectedNode?.NodeType == "Employee" && SelectedNode.DataItem is Employee e2)
            {
                target = Cards.FirstOrDefault(c => c.EmployeeId == e2.Id);
            }

            if (target == null)
            {
                MessageBox.Show("Nhân viên này chưa được cấp thẻ để gia hạn.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new RFIDGiaHanDialog(target.CardUID, target.BienSo) { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _cardService.GiaHan(target.Id, dialog.SelectedMonths);
                    MessageBox.Show("Gia hạn thẻ thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    _ = LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Gia hạn thẻ thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UnassignCard(object? param)
        {
            var target = param as RFIDCards ?? SelectedNode?.DataItem as RFIDCards;
            if (target == null) return;

            var confirm = MessageBox.Show($"Bạn có muốn THU HỒI (hủy gán) thẻ '{target.CardUID}' khỏi nhân viên?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    _enterpriseService.RemoveRFIDCard(target.CardUID);
                    _ = LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Thu hồi thẻ thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteCard(object? param)
        {
            var target = param as RFIDCards ?? SelectedNode?.DataItem as RFIDCards;
            if (target == null) return;

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn XÓA thẻ '{target.CardUID}' khỏi hệ thống?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    _cardService.Delete(target.Id);
                    _ = LoadDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Xóa thẻ thất bại: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
