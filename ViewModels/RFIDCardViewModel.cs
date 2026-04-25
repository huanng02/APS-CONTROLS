using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class RFIDCardViewModel : INotifyPropertyChanged
    {
        private readonly RFIDCardService service = new RFIDCardService();

        public ObservableCollection<QuanLyGiuXe.Models.RFIDCards> Items { get; } = new ObservableCollection<QuanLyGiuXe.Models.RFIDCards>();
        // tabs for LoaiVe
        public ObservableCollection<LoaiVeTabViewModel> Tabs { get; } = new ObservableCollection<LoaiVeTabViewModel>();

        private LoaiVeTabViewModel _selectedTab;
        public LoaiVeTabViewModel SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value) return;
                _selectedTab = value;
                OnPropertyChanged(nameof(SelectedTab));
                // inform convenience properties
                OnPropertyChanged(nameof(IsAllTabSelected));
                OnPropertyChanged(nameof(CanAdd));
                OnPropertyChanged(nameof(IsLoaiVeSelectable));
                // load tab data into Items without reinitializing ViewModel
                if (_selectedTab != null)
                {
                    // ensure tab has data
                    if (_selectedTab.Items.Count == 0) _selectedTab.Load(service);
                    Items.Clear();
                    foreach (var it in _selectedTab.Items) Items.Add(it);
                }
            }
        }

        // Convenience: true when current tab is 'Tất cả' (Id == 0)
        public bool IsAllTabSelected => SelectedTab != null && SelectedTab.Id == 0;

        // LoaiVe is selectable only when on 'Tất cả' tab
        public bool IsLoaiVeSelectable => IsAllTabSelected;

        // Whether Add button should be enabled. Disable when current tab is 'Tất cả'.
        public bool CanAdd => !IsAllTabSelected;

        // When opening add form, provide DefaultLoaiVeId to form VM
        private int? _defaultLoaiVeId;
        public int? DefaultLoaiVeId { get => _defaultLoaiVeId; set { _defaultLoaiVeId = value; OnPropertyChanged(nameof(DefaultLoaiVeId)); } }

        private QuanLyGiuXe.Models.RFIDCards _selectedItem;
        public QuanLyGiuXe.Models.RFIDCards SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
                if (_selectedItem != null)
                {
                    UID = _selectedItem.CardUID;
                    BienSo = _selectedItem.BienSo;
                    // LoaiVe/LoaiXe names are available in SelectedItem.LoaiVe / LoaiXe but we expose ids via Temp fields when editing
                    TrangThai = _selectedItem.TrangThai;
                }
            }
        }

        private string _uid;
        public string UID { get => _uid; set { _uid = value; OnPropertyChanged(nameof(UID)); } }

        private string _bienSo;
        public string BienSo { get => _bienSo; set { _bienSo = value; OnPropertyChanged(nameof(BienSo)); } }

        private int _loaiVeId;
        public int LoaiVeId { get => _loaiVeId; set { _loaiVeId = value; OnPropertyChanged(nameof(LoaiVeId)); } }

        private int _loaiXeId;
        public int LoaiXeId { get => _loaiXeId; set { _loaiXeId = value; OnPropertyChanged(nameof(LoaiXeId)); } }

        private string _trangThai;
        public string TrangThai { get => _trangThai; set { _trangThai = value; OnPropertyChanged(nameof(TrangThai)); } }

        private string _searchText;
        public string SearchText 
        { 
            get => _searchText; 
            set 
            { 
                _searchText = value; 
                OnPropertyChanged(nameof(SearchText)); 
                Load(); // Real-time search
            } 
        }

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand DeleteAllCommand { get; }
        public ICommand DownloadTemplateCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand GiaHanCommand { get; }

        public RFIDCardViewModel()
        {
            LoadCommand = new RelayCommand(_ => Load());
            // initialize tabs
            InitTabs();
            AddCommand = new RelayCommand(_ => Add());
            UpdateCommand = new RelayCommand(param => Update(param));
            DeleteCommand = new RelayCommand(param => Delete(param));
            DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected());
            DeleteAllCommand = new RelayCommand(_ => DeleteAll());
            ClearCommand = new RelayCommand(_ => Clear());
            DownloadTemplateCommand = new RelayCommand(async _ => await DownloadTemplate());
            ExportCommand = new RelayCommand(_ => Export());
            GiaHanCommand = new RelayCommand(param => GiaHan(param));
            Load();
        }

        private void InitTabs()
        {
            Tabs.Clear();
            // 'Tất cả' tab id=0
            Tabs.Add(new LoaiVeTabViewModel { Id = 0, Title = "Tất cả" });
            
            // Theo yêu cầu: LoaiVeId = 2 là Vé tháng, LoaiVeId = 1 là Vãng lai
            Tabs.Add(new LoaiVeTabViewModel { Id = 2, Title = "Vé tháng" });
            Tabs.Add(new LoaiVeTabViewModel { Id = 1, Title = "Vãng lai" });

            // default select first
            SelectedTab = Tabs.Count > 0 ? Tabs[0] : null;
        }


        private void Export()
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.Filter = "Excel Workbook|*.xlsx";
                dlg.FileName = "RFIDCards_Export.xlsx";
                if (dlg.ShowDialog() == true)
                {
                    var svc = new ImportExportService();
                    int? selectedLoaiVe = SelectedTab != null && SelectedTab.Id > 0 ? SelectedTab.Id : (int?)null;
                    svc.ExportToExcel(dlg.FileName, selectedLoaiVe);
                    System.Windows.MessageBox.Show("Export hoàn tất", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Export thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task DownloadTemplate()
        {
            try
            {
                var svc = new TemplateExportService();
                var path = await svc.CreateTemplateOnDesktopAsync();
                if (!string.IsNullOrEmpty(path))
                {
                    System.Windows.MessageBox.Show($"Template folder created:\n{path}", "Done", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show($"Failed to create template folder.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Load()
        {
            // Populate tabs data cache only if empty
            foreach (var tab in Tabs)
            {
                if (tab.Items.Count == 0) tab.Load(service);
            }

            // Set selected tab items into Items with Search Filter
            if (SelectedTab != null)
            {
                Items.Clear();
                var rawItems = SelectedTab.Items;
                
                foreach (var it in rawItems)
                {
                    if (string.IsNullOrWhiteSpace(SearchText) || 
                        (it.CardUID != null && it.CardUID.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (it.BienSo != null && it.BienSo.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        Items.Add(it);
                    }
                }
            }
        }

        // Refresh a single tab by id (0 = all)
        private void RefreshTab(int loaiVeId)
        {
            var tab = Tabs.FirstOrDefault(t => t.Id == loaiVeId);
            if (tab == null) return;
            tab.Load(service);
            // if refreshing current tab, update Items
            if (SelectedTab != null && SelectedTab.Id == loaiVeId)
            {
                Items.Clear();
                foreach (var it in SelectedTab.Items) Items.Add(it);
            }
        }

        private void Add()
        {
            // open wizard (multi-step) add dialog
            var model = new QuanLyGiuXe.Models.RFIDCards { CardUID = string.Empty, BienSo = string.Empty, TrangThai = "Active" };

            var vm = new RFIDCardWizardViewModel();
            vm.InitForAdd();
            // seed initial values from model (if any)
            vm.CardUID = model.CardUID;
            vm.BienSo = model.BienSo;
            vm.TrangThai = model.TrangThai;
            // If current tab is specific, preselect that LoaiVe in wizard
            if (SelectedTab != null && SelectedTab.Id > 0)
            {
                vm.LoaiVeId = SelectedTab.Id;
            }

            var dlg = new Views.RFIDCardAddEditWindow(null) { Owner = System.Windows.Application.Current.MainWindow };
            dlg.DataContext = vm;
            var result = dlg.ShowDialog();
            if (result == true)
            {
                try
                {
                    var toAdd = new QuanLyGiuXe.Models.RFIDCards
                    {
                        CardUID = vm.CardUID,
                        CardName = vm.CardName,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId ?? 0,
                        LoaiVeId = vm.LoaiVeId ?? (SelectedTab != null ? SelectedTab.Id : 0),
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai
                    };

                    service.Add(toAdd);
                    System.Windows.MessageBox.Show("Thêm thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                    int addedLoaiVe = toAdd.LoaiVeId ?? 0;
                    int current = SelectedTab?.Id ?? 0;
                    RefreshTab(addedLoaiVe);
                    if (addedLoaiVe != current) RefreshTab(current);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Thêm thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void Update(object parameter)
        {
            QuanLyGiuXe.Models.RFIDCards target = null;
            if (parameter is QuanLyGiuXe.Models.RFIDCards rc) target = rc;
            else if (SelectedItem != null) target = SelectedItem;
            if (target == null) return;
            // Use wizard ViewModel for edit flow: load data BEFORE showing window
            var vm = new RFIDCardWizardViewModel();
            vm.LoadForEdit(target.Id);

            var window = new Views.RFIDCardAddEditWindow(null) { Owner = System.Windows.Application.Current.MainWindow };
            window.DataContext = vm;
            var result = window.ShowDialog();
            if (result == true)
            {
                try
                {
                    // map back from vm to model and update
                    var updated = new QuanLyGiuXe.Models.RFIDCards
                    {
                        Id = vm.Id,
                        // preserve original CardUID (identity) — do not allow edits to UID
                        CardUID = target.CardUID,
                        CardName = vm.CardName,
                        BienSo = vm.BienSo,
                        LoaiXeId = vm.LoaiXeId,
                        // if user did not select LoaiVe in wizard, keep the SelectedTab.Id as priority
                        LoaiVeId = vm.LoaiVeId ?? (SelectedTab != null && SelectedTab.Id > 0 ? SelectedTab.Id : vm.LoaiVeId ?? 0),
                        NgayDangKy = vm.NgayDangKy,
                        NgayHetHan = vm.NgayHetHan,
                        TrangThai = vm.TrangThai
                    };
                    // remember previous LoaiVe for refresh decisions
                    int previousLoaiVe = target.LoaiVeId ?? 0;
                    service.Update(updated);
                    System.Windows.MessageBox.Show("Sửa thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cập nhật thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                // refresh current tab and previous tab if LoaiVe moved
                RefreshTab(SelectedTab?.Id ?? 0);
                // if updated.LoaiVeId different from SelectedTab.Id, also refresh that tab
                if (vm.LoaiVeId.HasValue && vm.LoaiVeId.Value != (SelectedTab?.Id ?? 0))
                    RefreshTab(vm.LoaiVeId.Value);
            }
        }

        private void Delete(object parameter)
        {
            QuanLyGiuXe.Models.RFIDCards toDelete = null;
            if (parameter is QuanLyGiuXe.Models.RFIDCards rc) toDelete = rc;
            else if (SelectedItem != null) toDelete = SelectedItem;
            if (toDelete == null) return;

            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa thẻ '{toDelete.CardUID}'?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                service.Delete(toDelete.Id);
                System.Windows.MessageBox.Show("Xoá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("Xoá thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            // refresh only the current tab
            RefreshTab(SelectedTab?.Id ?? 0);
            Clear();
        }

        private void GiaHan(object parameter)
        {
            QuanLyGiuXe.Models.RFIDCards target = null;
            if (parameter is QuanLyGiuXe.Models.RFIDCards rc) target = rc;
            else if (SelectedItem != null) target = SelectedItem;
            if (target == null) return;

            var dialog = new Views.RFIDGiaHanDialog(target.CardUID, target.BienSo) 
            { 
                Owner = System.Windows.Application.Current.MainWindow 
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    service.GiaHan(target.Id, dialog.SelectedMonths);
                    System.Windows.MessageBox.Show($"Gia hạn thẻ {target.CardUID} thành công thêm {dialog.SelectedMonths} tháng.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    
                    // Reload data
                    RefreshTab(SelectedTab?.Id ?? 0);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Gia hạn thất bại: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void Clear()
        {
            SelectedItem = null;
            UID = string.Empty;
            BienSo = string.Empty;
            LoaiVeId = 0;
            LoaiXeId = 0;
            TrangThai = string.Empty;
        }

        private void DeleteSelected()
        {
            var selected = Items.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) return;
            if (System.Windows.MessageBox.Show($"Bạn có chắc muốn xóa {selected.Count} mục đã chọn?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) != System.Windows.MessageBoxResult.Yes)
                return;

            bool ok = true;
            foreach (var it in selected)
            {
                try { service.Delete(it.Id); }
                catch { ok = false; }
            }

            if (ok) System.Windows.MessageBox.Show("Xoá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            else System.Windows.MessageBox.Show("Xoá thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            Load();
        }

        private void DeleteAll()
        {
            if (System.Windows.MessageBox.Show("Bạn có chắc muốn xóa tất cả thẻ?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.Yes)
                return;

            bool ok = true;
            foreach (var it in Items.ToList())
            {
                try { service.Delete(it.Id); }
                catch { ok = false; }
            }

            if (ok) System.Windows.MessageBox.Show("Xoá thành công", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            else System.Windows.MessageBox.Show("Xoá thất bại", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

            Load();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
