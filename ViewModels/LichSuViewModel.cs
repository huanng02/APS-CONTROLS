using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ClosedXML.Excel;
using Microsoft.Win32;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LichSuViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService db = new();

        private List<LichSuXe> TatCaLichSu = new();

        public ObservableCollection<LichSuXe> DanhSachLichSu { get; set; } = new();

        // ======================
        // STATS PROPERTIES
        // ======================
        public ObservableCollection<LoaiVeStats> ThongKeLoaiVe { get; set; } = new();
        public ObservableCollection<LoaiXeStats> ThongKeLoaiXe { get; set; } = new();

        private int _tongLuotXe;
        public int TongLuotXe
        {
            get => _tongLuotXe;
            set { _tongLuotXe = value; OnPropertyChanged(nameof(TongLuotXe)); }
        }

        private double _tongDoanhThu;
        public double TongDoanhThu
        {
            get => _tongDoanhThu;
            set { _tongDoanhThu = value; OnPropertyChanged(nameof(TongDoanhThu)); }
        }

        private int _xeHomNay;
        public int XeHomNay
        {
            get => _xeHomNay;
            set { _xeHomNay = value; OnPropertyChanged(nameof(XeHomNay)); }
        }

        private double _doanhThuHomNay;
        public double DoanhThuHomNay
        {
            get => _doanhThuHomNay;
            set { _doanhThuHomNay = value; OnPropertyChanged(nameof(DoanhThuHomNay)); }
        }

        // ======================
        // SELECTION & DETAIL PROPERTIES
        // ======================
        private LichSuXe? _selectedLichSu;
        public LichSuXe? SelectedLichSu
        {
            get => _selectedLichSu;
            set
            {
                _selectedLichSu = value;
                OnPropertyChanged(nameof(SelectedLichSu));
                OnPropertyChanged(nameof(HasSelectedLichSu));
                OnPropertyChanged(nameof(ThoiGianDo));
                OnPropertyChanged(nameof(SelectedAnhVaoToanCanh));
                OnPropertyChanged(nameof(SelectedAnhVaoBienSo));
                OnPropertyChanged(nameof(SelectedAnhVaoBienSoCat));
                OnPropertyChanged(nameof(SelectedAnhRaToanCanh));
                OnPropertyChanged(nameof(SelectedAnhRaBienSo));
                OnPropertyChanged(nameof(SelectedAnhRaBienSoCat));
            }
        }

        public string? SelectedAnhVaoToanCanh => ResolveImagePath(SelectedLichSu?.AnhVao, "full.jpg");
        public string? SelectedAnhVaoBienSo => ResolveImagePath(SelectedLichSu?.AnhVao, "plate_raw.jpg");
        public string? SelectedAnhVaoBienSoCat => ResolveImagePath(SelectedLichSu?.AnhVao, "plate_crop.jpg");

        public string? SelectedAnhRaToanCanh => ResolveImagePath(SelectedLichSu?.AnhRa, "full.jpg");
        public string? SelectedAnhRaBienSo => ResolveImagePath(SelectedLichSu?.AnhRa, "plate_raw.jpg");
        public string? SelectedAnhRaBienSoCat => ResolveImagePath(SelectedLichSu?.AnhRa, "plate_crop.jpg");

        private string? ResolveImagePath(string? folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(folderPath)) return null;

            if (System.IO.File.Exists(folderPath))
            {
                // If it's a file path (for backward compatibility), return it only for the main overview photo
                return fileName == "full.jpg" ? folderPath : null;
            }

            if (System.IO.Directory.Exists(folderPath))
            {
                string targetPath = System.IO.Path.Combine(folderPath, fileName);
                if (System.IO.File.Exists(targetPath)) return targetPath;
            }

            return null;
        }

        public bool HasSelectedLichSu => SelectedLichSu != null;

        public bool CanViewShiftReport => PermissionService.Instance.CheckPermission("VIEW_SHIFT_REPORT");

        public string ThoiGianDo
        {
            get
            {
                if (SelectedLichSu == null) return string.Empty;
                var ra = SelectedLichSu.ThoiGianRa ?? DateTime.Now;
                var duration = ra - SelectedLichSu.ThoiGianVao;
                if (duration.TotalDays >= 1)
                {
                    return $"{(int)duration.TotalDays} ngày {duration.Hours} giờ {duration.Minutes} phút";
                }
                if (duration.TotalHours >= 1)
                {
                    return $"{(int)duration.TotalHours} giờ {duration.Minutes} phút";
                }
                return $"{(int)duration.TotalMinutes} phút";
            }
        }

        // ======================
        // FILTER PROPERTIES
        // ======================
        private string _tuKhoaTimKiem = "";
        public string TuKhoaTimKiem
        {
            get => _tuKhoaTimKiem;
            set
            {
                _tuKhoaTimKiem = value;
                OnPropertyChanged(nameof(TuKhoaTimKiem));
                DebounceSearch();
            }
        }

        private DateTime? _tuNgay;
        public DateTime? TuNgay
        {
            get => _tuNgay;
            set { _tuNgay = value; OnPropertyChanged(nameof(TuNgay)); LoadTrangAsync(); }
        }

        private DateTime? _denNgay;
        public DateTime? DenNgay
        {
            get => _denNgay;
            set { _denNgay = value; OnPropertyChanged(nameof(DenNgay)); LoadTrangAsync(); }
        }

        // New hour filter properties (nullable int)
        private int? _startHour;
        public int? StartHour
        {
            get => _startHour;
            set { _startHour = value; OnPropertyChanged(nameof(StartHour)); LoadTrangAsync(); }
        }

        private int? _endHour;
        public int? EndHour
        {
            get => _endHour;
            set { _endHour = value; OnPropertyChanged(nameof(EndHour)); LoadTrangAsync(); }
        }

        // New minute filter properties (nullable int)
        private int? _startMinute;
        public int? StartMinute
        {
            get => _startMinute;
            set { _startMinute = value; OnPropertyChanged(nameof(StartMinute)); LoadTrangAsync(); }
        }

        private int? _endMinute;
        public int? EndMinute
        {
            get => _endMinute;
            set { _endMinute = value; OnPropertyChanged(nameof(EndMinute)); LoadTrangAsync(); }
        }
        // Lists for time ComboBox populating
        public List<int> HourList { get; } = Enumerable.Range(0, 24).ToList();
        public List<int> MinuteList { get; } = Enumerable.Range(0, 60).ToList();

        // Advanced filter lists
        public ObservableCollection<string> TrangThaiList { get; } = new() { "Tất cả", "Xe đang trong bãi", "Xe đã ra", "Chỉ xe vào (Tất cả lượt vào)", "Chỉ xe ra (Tất cả lượt ra)" };
        public ObservableCollection<string> LoaiXeList { get; } = new() { "Tất cả" };
        public ObservableCollection<string> LoaiVeList { get; } = new() { "Tất cả" };
        public ObservableCollection<string> LaneList { get; } = new() { "Tất cả" };
        public ObservableCollection<string> SiteList { get; } = new() { "Tất cả" };
        public ObservableCollection<string> ZoneList { get; } = new() { "Tất cả" };
        public ObservableCollection<string> FeeStatusList { get; } = new() { "Tất cả", "Có thu phí (> 0đ)", "Miễn phí (0đ)" };

        private bool _isInitializing = false;

        private string _selectedTrangThai = "Tất cả";
        public string SelectedTrangThai
        {
            get => _selectedTrangThai;
            set
            {
                if (_selectedTrangThai != value)
                {
                    _selectedTrangThai = value;
                    OnPropertyChanged(nameof(SelectedTrangThai));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        private string _selectedLoaiXe = "Tất cả";
        public string SelectedLoaiXe
        {
            get => _selectedLoaiXe;
            set
            {
                if (_selectedLoaiXe != value)
                {
                    _selectedLoaiXe = value;
                    OnPropertyChanged(nameof(SelectedLoaiXe));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        private string _selectedLoaiVe = "Tất cả";
        public string SelectedLoaiVe
        {
            get => _selectedLoaiVe;
            set
            {
                if (_selectedLoaiVe != value)
                {
                    _selectedLoaiVe = value;
                    OnPropertyChanged(nameof(SelectedLoaiVe));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        private string _selectedLane = "Tất cả";
        public string SelectedLane
        {
            get => _selectedLane;
            set
            {
                if (_selectedLane != value)
                {
                    _selectedLane = value;
                    OnPropertyChanged(nameof(SelectedLane));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        private string _selectedSite = "Tất cả";
        public string SelectedSite
        {
            get => _selectedSite;
            set
            {
                if (_selectedSite != value)
                {
                    _selectedSite = value;
                    OnPropertyChanged(nameof(SelectedSite));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        private string _selectedZone = "Tất cả";
        public string SelectedZone
        {
            get => _selectedZone;
            set
            {
                if (_selectedZone != value)
                {
                    _selectedZone = value;
                    OnPropertyChanged(nameof(SelectedZone));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        private string _selectedFeeStatus = "Tất cả";
        public string SelectedFeeStatus
        {
            get => _selectedFeeStatus;
            set
            {
                if (_selectedFeeStatus != value)
                {
                    _selectedFeeStatus = value;
                    OnPropertyChanged(nameof(SelectedFeeStatus));
                    if (!_isInitializing) LoadTrangAsync();
                }
            }
        }

        // ======================
        // PAGING
        // ======================
        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    TrangHienTai = 1;
                    OnPropertyChanged(nameof(PageSize));
                    LoadTrangAsync();
                }
            }
        }

        private int _trangHienTai = 1;
        public int TrangHienTai
        {
            get => _trangHienTai;
            set
            {
                _trangHienTai = value;
                OnPropertyChanged(nameof(TrangHienTai));
                OnPropertyChanged(nameof(TongTrang));
            }
        }

        public int TongTrang
        {
            get
            {
                int total = _filteredCount;
                return total == 0 ? 1 : (int)Math.Ceiling((double)total / PageSize);
            }
        }

        private int _filteredCount = 0;

        // ======================
        // COMMANDS
        // ======================
        public ICommand TrangTruocCommand { get; }
        public ICommand TrangSauCommand { get; }
        public ICommand TrangDauCommand { get; }
        public ICommand TrangCuoiCommand { get; }
        public ICommand ResetFilterCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand EndSessionCommand { get; }
        public ICommand BaoCaoCaCommand { get; }

        private CancellationTokenSource? _searchCts;

        public LichSuViewModel()
        {
            TrangTruocCommand = new RelayCommand(_ => { if (TrangHienTai > 1) { TrangHienTai--; LoadTrangAsync(); } });
            TrangSauCommand = new RelayCommand(_ => { if (TrangHienTai < TongTrang) { TrangHienTai++; LoadTrangAsync(); } });
            TrangDauCommand = new RelayCommand(_ => { TrangHienTai = 1; LoadTrangAsync(); });
            TrangCuoiCommand = new RelayCommand(_ => { TrangHienTai = TongTrang; LoadTrangAsync(); });
            ResetFilterCommand = new RelayCommand(_ => ResetFilter());
            ExportExcelCommand = new RelayCommand(async _ => await ExportExcelAsync());
            EndSessionCommand = new RelayCommand(async _ => await EndSessionAsync(), _ => CanEndSession());
            BaoCaoCaCommand = new RelayCommand(_ => BaoCaoCa());

            // Default dates
            TuNgay = DateTime.Today;
            DenNgay = DateTime.Today.AddDays(1).AddTicks(-1);

            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                _isInitializing = true;
                var data = await Task.Run(() => db.LayLichSu().ToList());
                TatCaLichSu = data;
                CalculateOverallTodayStats(data);

                // Populate filter lists from data dynamically
                var loaiXes = data.Select(x => x.LoaiXeName).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
                var loaiVes = data.Select(x => x.LoaiVeName).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
                var entryLanes = data.Select(x => x.EntryLaneName).Where(x => !string.IsNullOrEmpty(x));
                var exitLanes = data.Select(x => x.ExitLaneName).Where(x => !string.IsNullOrEmpty(x));
                var lanes = entryLanes.Concat(exitLanes).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
                var sites = data.Select(x => x.SiteName).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();
                var zones = data.Select(x => x.ZoneName).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x).ToList();

                Application.Current?.Dispatcher?.Invoke(new Action(() =>
                {
                    LoaiXeList.Clear();
                    LoaiXeList.Add("Tất cả");
                    foreach (var item in loaiXes) LoaiXeList.Add(item);

                    LoaiVeList.Clear();
                    LoaiVeList.Add("Tất cả");
                    foreach (var item in loaiVes) LoaiVeList.Add(item);

                    LaneList.Clear();
                    LaneList.Add("Tất cả");
                    foreach (var item in lanes) LaneList.Add(item);

                    SiteList.Clear();
                    SiteList.Add("Tất cả");
                    foreach (var item in sites) SiteList.Add(item);

                    ZoneList.Clear();
                    ZoneList.Add("Tất cả");
                    foreach (var item in zones) ZoneList.Add(item);

                    // Re-set selections to "Tất cả" to avoid null selection reset due to collection clearing
                    SelectedTrangThai = "Tất cả";
                    SelectedLoaiXe = "Tất cả";
                    SelectedLoaiVe = "Tất cả";
                    SelectedLane = "Tất cả";
                    SelectedSite = "Tất cả";
                    SelectedZone = "Tất cả";
                    SelectedFeeStatus = "Tất cả";
                }));

                _isInitializing = false;
                await LoadTrangAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("HistoryLoad", "LichSuViewModel", "Lỗi tải lịch sử ban đầu", ex);
            }
        }

        private void CalculateOverallTodayStats(List<LichSuXe> allData)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                var today = DateTime.Today;
                var dataToday = allData.Where(x => x.ThoiGianVao.Date == today || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Date == today)).ToList();
                XeHomNay = dataToday.Count;
                DoanhThuHomNay = dataToday.Sum(x => x.Tien ?? 0);
            }));
        }

        private void CalculateFilteredStats(List<LichSuXe> filteredData)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                TongLuotXe = filteredData.Count;
                TongDoanhThu = filteredData.Sum(x => x.Tien ?? 0);

                // Thống kê theo loại vé
                ThongKeLoaiVe.Clear();
                var groupedVe = filteredData
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.LoaiVeName) ? "Vé lượt" : x.LoaiVeName)
                    .Select(g => new LoaiVeStats
                    {
                        TenLoaiVe = g.Key,
                        SoLuotVao = g.Count(x => x.ThoiGianVao != DateTime.MinValue),
                        SoLuotRa = g.Count(x => x.ThoiGianRa.HasValue),
                        DoanhThu = g.Sum(x => x.Tien ?? 0)
                    })
                    .OrderByDescending(x => x.SoLuotVao);

                foreach (var item in groupedVe)
                {
                    ThongKeLoaiVe.Add(item);
                }

                // Thống kê theo loại xe
                ThongKeLoaiXe.Clear();
                var groupedXe = filteredData
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.LoaiXeName) ? "Không xác định" : x.LoaiXeName)
                    .Select(g => new LoaiXeStats
                    {
                        TenLoaiXe = g.Key,
                        SoLuot = g.Count(),
                        DangTrongBai = g.Count(x => !x.ThoiGianRa.HasValue),
                        DoanhThu = g.Sum(x => x.Tien ?? 0)
                    })
                    .OrderByDescending(x => x.SoLuot);

                foreach (var item in groupedXe)
                {
                    ThongKeLoaiXe.Add(item);
                }
            }));
        }

        private void DebounceSearch()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Delay(300, token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    TrangHienTai = 1;
                    _ = LoadTrangAsync();
                }
            }, TaskScheduler.Default);
        }

        private IEnumerable<LichSuXe> GetFilteredData()
        {
            var query = TatCaLichSu.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(TuKhoaTimKiem))
            {
                var keyword = TuKhoaTimKiem.ToLower();
                query = query.Where(x =>
                    (!string.IsNullOrEmpty(x.BienSo) && x.BienSo.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(x.EmployeeName) && x.EmployeeName.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(x.EmployeeCode) && x.EmployeeCode.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(x.CompanyName) && x.CompanyName.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(x.DepartmentName) && x.DepartmentName.ToLower().Contains(keyword)));
            }

            // Status and Date/Time filtering
            if (SelectedTrangThai == "Xe đang trong bãi")
            {
                query = query.Where(x => !x.ThoiGianRa.HasValue);
                
                if (TuNgay.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Date >= TuNgay.Value.Date);
                if (DenNgay.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Date <= DenNgay.Value.Date);
                
                if (StartHour.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Hour >= StartHour.Value);
                if (EndHour.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Hour <= EndHour.Value);
                if (StartMinute.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Minute >= StartMinute.Value);
                if (EndMinute.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Minute <= EndMinute.Value);
            }
            else if (SelectedTrangThai == "Xe đã ra" || SelectedTrangThai == "Chỉ xe ra (Tất cả lượt ra)")
            {
                query = query.Where(x => x.ThoiGianRa.HasValue);
                
                if (TuNgay.HasValue)
                    query = query.Where(x => x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Date >= TuNgay.Value.Date);
                if (DenNgay.HasValue)
                    query = query.Where(x => x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Date <= DenNgay.Value.Date);
                
                if (StartHour.HasValue)
                    query = query.Where(x => x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Hour >= StartHour.Value);
                if (EndHour.HasValue)
                    query = query.Where(x => x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Hour <= EndHour.Value);
                if (StartMinute.HasValue)
                    query = query.Where(x => x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Minute >= StartMinute.Value);
                if (EndMinute.HasValue)
                    query = query.Where(x => x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Minute <= EndMinute.Value);
            }
            else if (SelectedTrangThai == "Chỉ xe vào (Tất cả lượt vào)")
            {
                if (TuNgay.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Date >= TuNgay.Value.Date);
                if (DenNgay.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Date <= DenNgay.Value.Date);
                
                if (StartHour.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Hour >= StartHour.Value);
                if (EndHour.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Hour <= EndHour.Value);
                if (StartMinute.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Minute >= StartMinute.Value);
                if (EndMinute.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Minute <= EndMinute.Value);
            }
            else // "Tất cả"
            {
                if (TuNgay.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Date >= TuNgay.Value.Date || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Date >= TuNgay.Value.Date));
                if (DenNgay.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Date <= DenNgay.Value.Date || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Date <= DenNgay.Value.Date));
                
                if (StartHour.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Hour >= StartHour.Value || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Hour >= StartHour.Value));
                if (EndHour.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Hour <= EndHour.Value || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Hour <= EndHour.Value));
                if (StartMinute.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Minute >= StartMinute.Value || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Minute >= StartMinute.Value));
                if (EndMinute.HasValue)
                    query = query.Where(x => x.ThoiGianVao.Minute <= EndMinute.Value || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Minute <= EndMinute.Value));
            }

            // Advanced dropdown filters
            if (!string.IsNullOrEmpty(SelectedLoaiXe) && SelectedLoaiXe != "Tất cả")
            {
                query = query.Where(x => string.Equals(x.LoaiXeName, SelectedLoaiXe, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SelectedLoaiVe) && SelectedLoaiVe != "Tất cả")
            {
                query = query.Where(x => string.Equals(x.LoaiVeName, SelectedLoaiVe, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SelectedLane) && SelectedLane != "Tất cả")
            {
                query = query.Where(x => string.Equals(x.EntryLaneName, SelectedLane, StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(x.ExitLaneName, SelectedLane, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SelectedSite) && SelectedSite != "Tất cả")
            {
                query = query.Where(x => string.Equals(x.SiteName, SelectedSite, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(SelectedZone) && SelectedZone != "Tất cả")
            {
                query = query.Where(x => string.Equals(x.ZoneName, SelectedZone, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedFeeStatus == "Có thu phí (> 0đ)")
            {
                query = query.Where(x => x.Tien.HasValue && x.Tien.Value > 0);
            }
            else if (SelectedFeeStatus == "Miễn phí (0đ)")
            {
                query = query.Where(x => !x.Tien.HasValue || x.Tien.Value == 0);
            }

            return query.OrderByDescending(x => x.ThoiGianRa ?? x.ThoiGianVao);
        }

        public async Task LoadTrangAsync()
        {
            try
            {
                LoggingService.Instance.LogInfo("LoadTrangAsync", "LichSuViewModel", 
                    $"Executing LoadTrangAsync. Filters: TuNgay={TuNgay:yyyy-MM-dd HH:mm:ss}, DenNgay={DenNgay:yyyy-MM-dd HH:mm:ss}, " +
                    $"TrangThai='{SelectedTrangThai}', LoaiXe='{SelectedLoaiXe}', LoaiVe='{SelectedLoaiVe}', Lane='{SelectedLane}', " +
                    $"Site='{SelectedSite}', Zone='{SelectedZone}', FeeStatus='{SelectedFeeStatus}', " +
                    $"StartHour={StartHour}, EndHour={EndHour}, StartMinute={StartMinute}, EndMinute={EndMinute}, " +
                    $"TuKhoa='{TuKhoaTimKiem}', Initializing={_isInitializing}");

                var filtered = await Task.Run(() => GetFilteredData().ToList());
                _filteredCount = filtered.Count;

                LoggingService.Instance.LogInfo("LoadTrangAsync", "LichSuViewModel", 
                    $"Filtered data count: {_filteredCount}");

                CalculateFilteredStats(filtered);

                var pageData = filtered
                    .Skip((TrangHienTai - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    DanhSachLichSu.Clear();
                    foreach (var item in pageData)
                    {
                        DanhSachLichSu.Add(item);
                    }
                    OnPropertyChanged(nameof(TongTrang));
                }));
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("HistoryLoadPage", "LichSuViewModel", "Lỗi tải trang lịch sử", ex);
            }
        }

        public void ResetFilter()
        {
            _tuKhoaTimKiem = "";
            OnPropertyChanged(nameof(TuKhoaTimKiem));

            _tuNgay = DateTime.Today;
            OnPropertyChanged(nameof(TuNgay));

            _denNgay = DateTime.Today.AddDays(1).AddTicks(-1);
            OnPropertyChanged(nameof(DenNgay));

            // Reset hour and minute filters
            _startHour = null;
            OnPropertyChanged(nameof(StartHour));
            _endHour = null;
            OnPropertyChanged(nameof(EndHour));
            _startMinute = null;
            OnPropertyChanged(nameof(StartMinute));
            _endMinute = null;
            OnPropertyChanged(nameof(EndMinute));

            // Reset advanced filters
            _selectedTrangThai = "Tất cả";
            OnPropertyChanged(nameof(SelectedTrangThai));
            _selectedLoaiXe = "Tất cả";
            OnPropertyChanged(nameof(SelectedLoaiXe));
            _selectedLoaiVe = "Tất cả";
            OnPropertyChanged(nameof(SelectedLoaiVe));
            _selectedLane = "Tất cả";
            OnPropertyChanged(nameof(SelectedLane));
            _selectedSite = "Tất cả";
            OnPropertyChanged(nameof(SelectedSite));
            _selectedZone = "Tất cả";
            OnPropertyChanged(nameof(SelectedZone));
            _selectedFeeStatus = "Tất cả";
            OnPropertyChanged(nameof(SelectedFeeStatus));

            TrangHienTai = 1;
            _ = LoadTrangAsync();
        }

        private async void BaoCaoCa()
        {
            if (!CanViewShiftReport) return;

            var loginTime = CurrentUserContext.Instance.LoginTime;
            if (loginTime == default)
            {
                loginTime = DateTime.Today;
            }
            var now = DateTime.Now;

            _tuNgay = loginTime.Date;
            OnPropertyChanged(nameof(TuNgay));

            _denNgay = now.Date;
            OnPropertyChanged(nameof(DenNgay));

            _startHour = loginTime.Hour;
            OnPropertyChanged(nameof(StartHour));

            _startMinute = loginTime.Minute;
            OnPropertyChanged(nameof(StartMinute));

            _endHour = now.Hour;
            OnPropertyChanged(nameof(EndHour));

            _endMinute = now.Minute;
            OnPropertyChanged(nameof(EndMinute));

            TrangHienTai = 1;
            await LoadTrangAsync();

            bool saved = await ExportExcelAsync($"BaoCaoCa_{CurrentUserContext.Instance.Username}_{now:yyyyMMdd_HHmm}.xlsx", loginTime, now);
            if (saved)
            {
                SessionService.ClearUserShiftStart(CurrentUserContext.Instance.Username);
                var app = Application.Current as App;
                if (app != null)
                {
                    app.PerformLogout();
                }
            }
        }

        private async Task<bool> ExportExcelAsync(string defaultFileName = null, DateTime? shiftStart = null, DateTime? shiftEnd = null)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Lưu danh sách lịch sử ra vào",
                    FileName = defaultFileName ?? $"LichSuXe_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };

                if (sfd.ShowDialog() == true)
                {
                    var dataToExport = await Task.Run(() => GetFilteredData().ToList());

                    await Task.Run(() =>
                    {
                        // Calculate stats on the background thread from dataToExport
                        var statsLoaiVe = dataToExport
                            .GroupBy(x => string.IsNullOrWhiteSpace(x.LoaiVeName) ? "Vé lượt" : x.LoaiVeName)
                            .Select(g => new
                            {
                                TenLoaiVe = g.Key,
                                SoLuotVao = g.Count(x => x.ThoiGianVao != DateTime.MinValue),
                                SoLuotRa = g.Count(x => x.ThoiGianRa.HasValue),
                                DoanhThu = g.Sum(x => x.Tien ?? 0)
                            })
                            .OrderByDescending(x => x.SoLuotVao)
                            .ToList();

                        var statsLoaiXe = dataToExport
                            .GroupBy(x => string.IsNullOrWhiteSpace(x.LoaiXeName) ? "Không xác định" : x.LoaiXeName)
                            .Select(g => new
                            {
                                TenLoaiXe = g.Key,
                                SoLuot = g.Count(),
                                DangTrongBai = g.Count(x => !x.ThoiGianRa.HasValue),
                                DoanhThu = g.Sum(x => x.Tien ?? 0)
                            })
                            .OrderByDescending(x => x.SoLuot)
                            .ToList();

                        using var wb = new XLWorkbook();
                        var ws = wb.Worksheets.Add("LichSu");
                        
                        // Report Title
                        ws.Cell(1, 1).Value = "BÁO CÁO CHI TIẾT LỊCH SỬ XE RA VÀO";
                        var titleRange = ws.Range(1, 1, 1, 18);
                        titleRange.Merge();
                        titleRange.Style.Font.Bold = true;
                        titleRange.Style.Font.FontSize = 16;
                        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        
                        ws.Cell(2, 1).Value = $"Thời gian xuất file: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                        var subTitleRange = ws.Range(2, 1, 2, 18);
                        subTitleRange.Merge();
                        subTitleRange.Style.Font.Italic = true;
                        subTitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        if (shiftStart.HasValue && shiftEnd.HasValue)
                        {
                            ws.Cell(3, 1).Value = $"Ca làm việc: {shiftStart.Value:dd/MM/yyyy HH:mm:ss} - {shiftEnd.Value:dd/MM/yyyy HH:mm:ss} | Nhân viên: {CurrentUserContext.Instance.Ten} ({CurrentUserContext.Instance.Username})";
                            var shiftRange = ws.Range(3, 1, 3, 18);
                            shiftRange.Merge();
                            shiftRange.Style.Font.Bold = true;
                            shiftRange.Style.Font.Italic = true;
                            shiftRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                        
                        // Header Row (Row 4)
                        string[] headers = {
                            "ID", "Biển số", "Loại vé", "Loại xe", 
                            "Thời gian vào", "Thời gian ra", "Thời gian đỗ", 
                            "Tiền (VNĐ)", "Mã thẻ", "Khu vực (Site)", 
                            "Vùng (Zone)", "Làn vào (Entry)", "Làn ra (Exit)", "Trạng thái",
                            "Chủ thẻ", "Mã nhân viên", "Công ty", "Phòng ban"
                        };
                        
                        for (int i = 0; i < headers.Length; i++)
                        {
                            var cell = ws.Cell(4, i + 1);
                            cell.Value = headers[i];
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E4FA3");
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }
                        
                        // Data
                        for (int i = 0; i < dataToExport.Count; i++)
                        {
                            var row = i + 5;
                            var item = dataToExport[i];
                            
                            ws.Cell(row, 1).Value = item.Id;
                            ws.Cell(row, 2).Value = item.BienSo;
                            ws.Cell(row, 3).Value = string.IsNullOrEmpty(item.LoaiVeName) ? "Vé lượt" : item.LoaiVeName;
                            ws.Cell(row, 4).Value = item.LoaiXeName;
                            
                            // Format entry / exit dates
                            var cellVao = ws.Cell(row, 5);
                            cellVao.Value = item.ThoiGianVao;
                            cellVao.Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
                            
                            var cellRa = ws.Cell(row, 6);
                            if (item.ThoiGianRa.HasValue)
                            {
                                cellRa.Value = item.ThoiGianRa.Value;
                                cellRa.Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
                            }
                            else
                            {
                                cellRa.Value = "Chưa ra";
                            }
                            
                            // Stay duration
                            var durationStr = "-";
                            if (item.ThoiGianRa.HasValue)
                            {
                                var d = item.ThoiGianRa.Value - item.ThoiGianVao;
                                durationStr = d.TotalDays >= 1 
                                    ? $"{(int)d.TotalDays}d {d.Hours}h {d.Minutes}m" 
                                    : $"{d.Hours}h {d.Minutes}m";
                            }
                            ws.Cell(row, 7).Value = durationStr;
                            
                            // Revenue
                            var cellTien = ws.Cell(row, 8);
                            cellTien.Value = item.Tien ?? 0;
                            cellTien.Style.NumberFormat.Format = "#,##0";
                            
                            ws.Cell(row, 9).Value = item.CardId;
                            ws.Cell(row, 10).Value = item.SiteName;
                            ws.Cell(row, 11).Value = item.ZoneName;
                            ws.Cell(row, 12).Value = item.EntryLaneName;
                            ws.Cell(row, 13).Value = item.ExitLaneName;
                            ws.Cell(row, 14).Value = item.TrangThai;
                            ws.Cell(row, 15).Value = item.EmployeeName;
                            ws.Cell(row, 16).Value = item.EmployeeCode;
                            ws.Cell(row, 17).Value = item.CompanyName;
                            ws.Cell(row, 18).Value = item.DepartmentName;
                        }
                        
                        // Summary Row
                        var summaryRow = dataToExport.Count + 5;
                        ws.Cell(summaryRow, 1).Value = "Tổng cộng";
                        ws.Cell(summaryRow, 1).Style.Font.Bold = true;
                        ws.Range(summaryRow, 1, summaryRow, 4).Merge();
                        
                        // Total visits
                        ws.Cell(summaryRow, 5).Value = $"Số lượt: {dataToExport.Count}";
                        ws.Cell(summaryRow, 5).Style.Font.Bold = true;
                        
                        // SUM formula for revenue
                        var totalTienCell = ws.Cell(summaryRow, 8);
                        totalTienCell.FormulaA1 = $"SUM(H5:H{summaryRow - 1})";
                        totalTienCell.Style.Font.Bold = true;
                        totalTienCell.Style.NumberFormat.Format = "#,##0";
                        
                        // Styling borders for entire table
                        var tableRange = ws.Range(4, 1, summaryRow, 18);
                        tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                        // --- ADD DETAILED ANALYTICS TO EXCEL WORKBOOK ---
                        // Title for stats section
                        var statsTitleRow = summaryRow + 3;
                        ws.Cell(statsTitleRow, 1).Value = "BẢNG THỐNG KÊ CHI TIẾT THEO BỘ LỌC / FILTERED ANALYTICS BREAKDOWN";
                        var statsTitleRange = ws.Range(statsTitleRow, 1, statsTitleRow, 18);
                        statsTitleRange.Merge();
                        statsTitleRange.Style.Font.Bold = true;
                        statsTitleRange.Style.Font.FontSize = 12;
                        statsTitleRange.Style.Font.FontColor = XLColor.White;
                        statsTitleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E4FA3");
                        statsTitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        var statsHeaderRow = statsTitleRow + 2;

                        // Ticket type breakdown (Columns A to D)
                        ws.Cell(statsHeaderRow, 1).Value = "THỐNG KÊ THEO LOẠI VÉ / TICKET TYPE STATS";
                        ws.Range(statsHeaderRow, 1, statsHeaderRow, 4).Merge();
                        ws.Range(statsHeaderRow, 1, statsHeaderRow, 4).Style.Font.Bold = true;
                        ws.Range(statsHeaderRow, 1, statsHeaderRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Range(statsHeaderRow, 1, statsHeaderRow, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");

                        var tHeaderRow = statsHeaderRow + 1;
                        ws.Cell(tHeaderRow, 1).Value = "Loại vé";
                        ws.Cell(tHeaderRow, 2).Value = "Lượt vào";
                        ws.Cell(tHeaderRow, 3).Value = "Lượt ra";
                        ws.Cell(tHeaderRow, 4).Value = "Doanh thu (đ)";

                        for (int col = 1; col <= 4; col++)
                        {
                            var cell = ws.Cell(tHeaderRow, col);
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2196F3");
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int tRow = tHeaderRow + 1;
                        foreach (var stat in statsLoaiVe)
                        {
                            ws.Cell(tRow, 1).Value = stat.TenLoaiVe;
                            ws.Cell(tRow, 2).Value = stat.SoLuotVao;
                            ws.Cell(tRow, 3).Value = stat.SoLuotRa;
                            
                            var revenueCell = ws.Cell(tRow, 4);
                            revenueCell.Value = stat.DoanhThu;
                            revenueCell.Style.NumberFormat.Format = "#,##0";
                            tRow++;
                        }

                        // Add borders to Ticket stats
                        var ticketStatsRange = ws.Range(tHeaderRow, 1, tRow - 1, 4);
                        ticketStatsRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        ticketStatsRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                        // Vehicle type breakdown (Columns F to I)
                        ws.Cell(statsHeaderRow, 6).Value = "THỐNG KÊ THEO LOẠI XE / VEHICLE TYPE STATS";
                        ws.Range(statsHeaderRow, 6, statsHeaderRow, 9).Merge();
                        ws.Range(statsHeaderRow, 6, statsHeaderRow, 9).Style.Font.Bold = true;
                        ws.Range(statsHeaderRow, 6, statsHeaderRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Range(statsHeaderRow, 6, statsHeaderRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F3FF");

                        var vHeaderRow = statsHeaderRow + 1;
                        ws.Cell(vHeaderRow, 6).Value = "Loại xe";
                        ws.Cell(vHeaderRow, 7).Value = "Tổng lượt";
                        ws.Cell(vHeaderRow, 8).Value = "Trong bãi";
                        ws.Cell(vHeaderRow, 9).Value = "Doanh thu (đ)";

                        for (int col = 6; col <= 9; col++)
                        {
                            var cell = ws.Cell(vHeaderRow, col);
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#7C3AED");
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int vRow = vHeaderRow + 1;
                        foreach (var stat in statsLoaiXe)
                        {
                            ws.Cell(vRow, 6).Value = stat.TenLoaiXe;
                            ws.Cell(vRow, 7).Value = stat.SoLuot;
                            ws.Cell(vRow, 8).Value = stat.DangTrongBai;
                            
                            var revenueCell = ws.Cell(vRow, 9);
                            revenueCell.Value = stat.DoanhThu;
                            revenueCell.Style.NumberFormat.Format = "#,##0";
                            vRow++;
                        }

                        // Add borders to Vehicle stats
                        var vehicleStatsRange = ws.Range(vHeaderRow, 6, vRow - 1, 9);
                        vehicleStatsRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        vehicleStatsRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        
                        ws.Columns().AdjustToContents();
                        wb.SaveAs(sfd.FileName);
                    });

                    MessageBox.Show("Xuất file Excel thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoggingService.Instance.LogSecurity("EXPORT", "LichSuViewModel", $"Exported {dataToExport.Count} rows with stats to {sfd.FileName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ExportExcel", "LichSuViewModel", "Lỗi xuất Excel", ex);
                MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        private bool CanEndSession()
        {
            return SelectedLichSu != null && SelectedLichSu.TrangThai == "Trong bãi" && !SelectedLichSu.ThoiGianRa.HasValue;
        }

        private async Task EndSessionAsync()
        {
            if (SelectedLichSu == null) return;

            var selected = SelectedLichSu;

            var confirmResult = MessageBox.Show(
                $"Bạn có chắc chắn muốn kết thúc phiên cho xe có biển số '{selected.BienSo}' ngay lập tức không? Hệ thống sẽ tự động tính phí gửi xe và lưu thông tin vào lịch sử.",
                "Xác nhận kết thúc phiên",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                // 1. Get the card if exists
                var card = await db.GetRFIDCardByIdAsync(selected.CardId);

                // 2. Compute fee
                DateTime timeIn = selected.ThoiGianVao;
                DateTime timeOut = DateTime.Now;
                int? loaiVeId = card?.LoaiVeId > 0 ? card.LoaiVeId : (int?)null;
                int? loaiXeId = card?.LoaiXeId > 0 ? card.LoaiXeId : (int?)null;
                double fee = db.TinhTien(loaiXeId, loaiVeId, timeIn, timeOut);

                // 3. Persist exit
                // Note: active session ID is negated in LayLichSuAsync to distinguish from archived history
                int xeTrongBaiId = -selected.Id;

                await db.UpdateXeRaByIdAsync(xeTrongBaiId, timeOut);
                await db.LuuLichSuAsync(
                    selected.BienSo,
                    timeIn,
                    timeOut,
                    fee,
                    string.Empty,
                    card?.UID,
                    selected.SiteId,
                    selected.ZoneId,
                    selected.EntryLaneId,
                    selected.ExitLaneId,
                    selected.AnhVao);

                await db.XoaXeByCardIdAsync(selected.CardId);

                LoggingService.Instance.LogVehicle("MANUAL_XE_RA", selected.BienSo, entityId: selected.CardId, details: $"Manually ended session from History window. Fee: {fee:N0} VND", source: "HistoryWindow");

                MessageBox.Show("Kết thúc phiên xe thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reload data
                await InitializeDataAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ManualEndSessionError", "LichSuViewModel", $"CardId={selected.CardId}", ex);
                MessageBox.Show($"Lỗi khi kết thúc phiên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class LoaiVeStats
    {
        public string TenLoaiVe { get; set; } = string.Empty;
        public int SoLuotVao { get; set; }
        public int SoLuotRa { get; set; }
        public double DoanhThu { get; set; }
    }

    public class LoaiXeStats
    {
        public string TenLoaiXe { get; set; } = string.Empty;
        public int SoLuot { get; set; }
        public int DangTrongBai { get; set; }
        public double DoanhThu { get; set; }
    }
}