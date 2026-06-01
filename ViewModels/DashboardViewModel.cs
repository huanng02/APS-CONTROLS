using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows;
using ClosedXML.Excel;
using LiveCharts;
using LiveCharts.Wpf;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly DashboardService _service = new DashboardService();
        private readonly ParkingTopologyService _topologyService = ParkingTopologyService.Instance;
        private DispatcherTimer _timer;

        // KPI Properties
        private int _xeTrongBai;
        public int XeTrongBai
        {
            get => _xeTrongBai;
            set { _xeTrongBai = value; OnPropertyChanged(nameof(XeTrongBai)); }
        }

        private int _luotXeVao;
        public int LuotXeVao
        {
            get => _luotXeVao;
            set { _luotXeVao = value; OnPropertyChanged(nameof(LuotXeVao)); }
        }

        private double _doanhThu;
        public double DoanhThu
        {
            get => _doanhThu;
            set { _doanhThu = value; OnPropertyChanged(nameof(DoanhThu)); }
        }

        private int _veActive;
        public int VeActive
        {
            get => _veActive;
            set { _veActive = value; OnPropertyChanged(nameof(VeActive)); }
        }

        // Bãi Xe Status
        private int _tongCho = 200; // Giả định
        public int TongCho
        {
            get => _tongCho;
            set { _tongCho = value; OnPropertyChanged(nameof(TongCho)); }
        }

        public double TyLeLapDay => TongCho > 0 ? (double)XeTrongBai / TongCho * 100 : 0;
        public int ChoTrong => TongCho - XeTrongBai;
        
        public bool IsNearFull => TyLeLapDay > 90;

        // Charts
        public SeriesCollection RevenueSeries { get; set; } = new SeriesCollection();
        public ObservableCollection<string> RevenueLabels { get; set; } = new ObservableCollection<string>();
        public Func<double, string> RevenueFormatter { get; set; }

        public SeriesCollection HourlySeries { get; set; } = new SeriesCollection();
        public ObservableCollection<string> HourlyLabels { get; set; } = new ObservableCollection<string>();
        public Func<double, string> HourlyFormatter { get; set; }

        // Recent Activity
        public ObservableCollection<HoatDongGhiNhan> RecentActivities { get; set; }

        // Statistics Breakdown Collections
        public ObservableCollection<LoaiVeStats> ThongKeLoaiVe { get; set; } = new ObservableCollection<LoaiVeStats>();
        public ObservableCollection<LoaiXeStats> ThongKeLoaiXe { get; set; } = new ObservableCollection<LoaiXeStats>();

        // Filter
        public ObservableCollection<string> FilterOptions { get; set; }
        private string _selectedFilter;
        public string SelectedFilter
        {
            get => _selectedFilter;
            set 
            { 
                if (_selectedFilter != value)
                {
                    _selectedFilter = value; 
                    OnPropertyChanged(nameof(SelectedFilter));
                    OnPropertyChanged(nameof(IsCustomDateVisible));
                    
                    if (value != "Tùy chọn...")
                    {
                        var range = GetDateRange(value);
                        FromDate = range.start;
                        ToDate = range.end;
                        _ = LoadDataAsync();
                    }
                }
            }
        }

        public ObservableCollection<QuanLyGiuXe.Models.ParkingSite> Sites { get; set; } = new ObservableCollection<QuanLyGiuXe.Models.ParkingSite>();
        public ObservableCollection<QuanLyGiuXe.Models.ParkingZone> Zones { get; set; } = new ObservableCollection<QuanLyGiuXe.Models.ParkingZone>();

        private QuanLyGiuXe.Models.ParkingSite _selectedSite;
        public QuanLyGiuXe.Models.ParkingSite SelectedSite
        {
            get => _selectedSite;
            set
            {
                if (_selectedSite != value)
                {
                    _selectedSite = value;
                    OnPropertyChanged(nameof(SelectedSite));
                    _ = LoadZonesAsync(value?.Id);
                    _lastLoadedRange = string.Empty; // Force reload
                    _ = LoadDataAsync();
                }
            }
        }

        private QuanLyGiuXe.Models.ParkingZone _selectedZone;
        public QuanLyGiuXe.Models.ParkingZone SelectedZone
        {
            get => _selectedZone;
            set
            {
                if (_selectedZone != value)
                {
                    _selectedZone = value;
                    OnPropertyChanged(nameof(SelectedZone));
                    _lastLoadedRange = string.Empty; // Force reload
                    _ = LoadDataAsync();
                }
            }
        }

        private async Task LoadTopologyAsync()
        {
            try
            {
                var sites = await _topologyService.GetSitesAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Sites.Clear();
                    Sites.Add(new QuanLyGiuXe.Models.ParkingSite { Id = 0, SiteName = "Tất cả các Site (All Sites)" });
                    foreach (var s in sites) Sites.Add(s);
                    SelectedSite = Sites[0];
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardViewModel", "LoadTopologyAsync", "Lỗi tải topology", ex);
            }
        }

        private async Task LoadZonesAsync(int? siteId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Zones.Clear();
                Zones.Add(new QuanLyGiuXe.Models.ParkingZone { Id = 0, ZoneName = "Tất cả khu vực (All Zones)" });
            });

            if (siteId.HasValue && siteId.Value > 0)
            {
                try
                {
                    var allZones = await _topologyService.GetZonesAsync();
                    var filteredZones = allZones.Where(z => z.SiteId == siteId.Value);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var z in filteredZones) Zones.Add(z);
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("DashboardViewModel", "LoadZonesAsync", "Lỗi tải zones", ex);
                }
            }
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Zones.Count > 0)
                    SelectedZone = Zones[0];
            });
        }
        
        private DateTime _fromDate;
        public DateTime FromDate
        {
            get => _fromDate;
            set 
            { 
                if (_fromDate != value)
                {
                    _fromDate = value; 
                    OnPropertyChanged(nameof(FromDate));
                    UpdateRangeDisplay();
                    if (SelectedFilter == "Tùy chọn...") _ = LoadDataAsync();
                }
            }
        }

        private DateTime _toDate;
        public DateTime ToDate
        {
            get => _toDate;
            set 
            { 
                if (_toDate != value)
                {
                    _toDate = value; 
                    OnPropertyChanged(nameof(ToDate));
                    UpdateRangeDisplay();
                    if (SelectedFilter == "Tùy chọn...") _ = LoadDataAsync();
                }
            }
        }

        public bool IsCustomDateVisible => SelectedFilter == "Tùy chọn...";

        private string _selectedDateRangeDisplay;
        public string SelectedDateRangeDisplay
        {
            get => _selectedDateRangeDisplay;
            set { _selectedDateRangeDisplay = value; OnPropertyChanged(nameof(SelectedDateRangeDisplay)); }
        }

        private bool _isExporting;
        public bool IsExporting
        {
            get => _isExporting;
            set { _isExporting = value; OnPropertyChanged(nameof(IsExporting)); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        private string _lastLoadedRange = "";

        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }

        public DashboardViewModel()
        {
            FilterOptions = new ObservableCollection<string> 
            { 
                "Hôm nay", 
                "Hôm qua", 
                "7 ngày qua", 
                "30 ngày qua", 
                "Tháng này", 
                "Tháng trước", 
                "Tùy chọn..." 
            };

            RevenueFormatter = value => value.ToString("N0") + " đ";
            HourlyFormatter = value => value.ToString("N0");
            RecentActivities = new ObservableCollection<HoatDongGhiNhan>();

            RefreshCommand = new RelayCommand(_ => _ = LoadDataAsync());
            ExportCommand = new RelayCommand(_ => _ = ExportExcelAsync(), _ => !IsExporting);

            // Default to "Hôm nay"
            _selectedFilter = "Hôm nay";
            var range = GetDateRange(_selectedFilter);
            _fromDate = range.start;
            _toDate = range.end;
            UpdateRangeDisplay();

            _ = LoadTopologyAsync();
            _ = LoadDataAsync();

            // Realtime Update every 10 seconds
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _timer.Tick += (s, e) => { if (SelectedFilter != "Tùy chọn...") _ = LoadDataAsync(); };
            _timer.Start();
        }

        private async Task ExportExcelAsync()
        {
            if (IsExporting) return;
            IsExporting = true;
            string filePath = string.Empty;

            try
            {
                // 1. Load data in parallel
                int? siteIdFilter = SelectedSite?.Id > 0 ? SelectedSite.Id : (int?)null;
                int? zoneIdFilter = SelectedZone?.Id > 0 ? SelectedZone.Id : (int?)null;

                var kpiTask = _service.GetKpiAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                var revTask = _service.GetRevenueByDayAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                var entriesTask = _service.GetEntriesByHourAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                var transTask = _service.GetTransactionsAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);

                await System.Threading.Tasks.Task.WhenAll(kpiTask, revTask, entriesTask, transTask);

                var kpi = await kpiTask;
                var revDt = await revTask;
                var entriesDt = await entriesTask;
                var transDt = await transTask;

                // 2. Validation
                if (transDt.Rows.Count == 0)
                {
                    System.Windows.MessageBox.Show("Không có dữ liệu giao dịch trong khoảng thời gian và bộ lọc đã chọn!", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                if (transDt.Rows.Count > 20000)
                {
                    System.Windows.MessageBox.Show("Dữ liệu quá lớn (> 20,000 dòng). Vui lòng chọn khoảng thời gian ngắn hơn.", "Cảnh báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 3. Create Excel
                using (var workbook = new XLWorkbook())
                {
                    var headerColor = XLColor.FromHtml("#2C3E50"); // Dark Navy
                    var headerFontColor = XLColor.White;
                    var stripeColor = XLColor.FromHtml("#F8F9F9"); // Very light gray
                    var borderColor = XLColor.FromHtml("#D1D1D1"); // Light gray border

                    string siteText = SelectedSite?.Id > 0 ? SelectedSite.SiteName : "Tất cả các Site";
                    string zoneText = SelectedZone?.Id > 0 ? SelectedZone.ZoneName : "Tất cả khu vực";
                    string period = $"BÁO CÁO TỪ {FromDate:dd/MM/yyyy} ĐẾN {ToDate:dd/MM/yyyy} | Site: {siteText} | Khu vực: {zoneText}";

                    // --- Sheet 1: KPI (Dashboard Style) ---
                    var wsKpi = workbook.Worksheets.Add("KPI");
                    wsKpi.Cell(1, 1).Value = "TỔNG QUAN CHỈ SỐ VẬN HÀNH";
                    wsKpi.Range(1, 1, 1, 3).Merge().Style.Font.Bold = true;
                    wsKpi.Range(1, 1, 1, 3).Style.Font.FontSize = 18;
                    wsKpi.Range(1, 1, 1, 3).Style.Font.FontColor = headerColor;
                    wsKpi.Range(1, 1, 1, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    wsKpi.Cell(2, 1).Value = period;
                    wsKpi.Range(2, 1, 2, 3).Merge().Style.Font.Italic = true;
                    wsKpi.Range(2, 1, 2, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    var kpiData = new[] {
                        new { Label = "1. TỔNG DOANH THU", Value = kpi.DoanhThu.ToString("#,##0 VNĐ"), Color = XLColor.FromHtml("#27AE60") },
                        new { Label = "2. TỔNG LƯỢT XE VÀO", Value = kpi.LuotXeVao.ToString("#,##0"), Color = XLColor.FromHtml("#2980B9") },
                        new { Label = "3. XE TRONG BÃI HIỆN TẠI", Value = kpi.XeTrongBai.ToString("#,##0"), Color = XLColor.FromHtml("#E67E22") },
                        new { Label = "4. TỔNG VÉ ĐANG HOẠT ĐỘNG", Value = kpi.VeActive.ToString("#,##0"), Color = XLColor.FromHtml("#8E44AD") }
                    };

                    int startRow = 5;
                    foreach (var item in kpiData)
                    {
                        var labelCell = wsKpi.Cell(startRow, 2);
                        labelCell.Value = item.Label;
                        labelCell.Style.Font.Bold = true;
                        labelCell.Style.Font.FontSize = 12;

                        var valueCell = wsKpi.Cell(startRow + 1, 2);
                        valueCell.Value = item.Value;
                        valueCell.Style.Font.Bold = true;
                        valueCell.Style.Font.FontSize = 20;
                        valueCell.Style.Font.FontColor = item.Color;

                        startRow += 3;
                    }
                    wsKpi.Column(2).Width = 45;

                    // --- Side-by-side statistics breakdown starting at row 19 ---
                    var listVeStats = transDt.AsEnumerable()
                        .GroupBy(row => string.IsNullOrWhiteSpace(row.Field<string>("Loại Vé")) ? "Vé lượt" : row.Field<string>("Loại Vé"))
                        .Select(g => new 
                        {
                            TenLoaiVe = g.Key,
                            SoLuotVao = g.Count(r => r.Field<DateTime?>("Giờ Vào") != null),
                            SoLuotRa = g.Count(r => r.Field<DateTime?>("Giờ Ra") != null),
                            DoanhThu = g.Sum(r => r.Field<double?>("Số Tiền") ?? 0)
                        })
                        .OrderByDescending(x => x.SoLuotVao)
                        .ToList();

                    var listXeStats = transDt.AsEnumerable()
                        .GroupBy(row => string.IsNullOrWhiteSpace(row.Field<string>("Loại Xe")) ? "Không xác định" : row.Field<string>("Loại Xe"))
                        .Select(g => new 
                        {
                            TenLoaiXe = g.Key,
                            SoLuot = g.Count(),
                            DangTrongBai = g.Count(r => r.Field<string>("Trạng Thái") == "Trong bãi" || r.Field<DateTime?>("Giờ Ra") == null),
                            DoanhThu = g.Sum(r => r.Field<double?>("Số Tiền") ?? 0)
                        })
                        .OrderByDescending(x => x.SoLuot)
                        .ToList();

                    // Row 19: Section Headers
                    wsKpi.Cell(19, 2).Value = "🎫 THỐNG KÊ THEO LOẠI VÉ / TICKET STATS";
                    wsKpi.Range(19, 2, 19, 5).Merge().Style.Font.Bold = true;
                    wsKpi.Range(19, 2, 19, 5).Style.Font.FontSize = 12;
                    wsKpi.Range(19, 2, 19, 5).Style.Font.FontColor = XLColor.FromHtml("#2196F3");
                    wsKpi.Range(19, 2, 19, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    wsKpi.Cell(19, 7).Value = "🚗 THỐNG KÊ THEO LOẠI XE / VEHICLE STATS";
                    wsKpi.Range(19, 7, 19, 10).Merge().Style.Font.Bold = true;
                    wsKpi.Range(19, 7, 19, 10).Style.Font.FontSize = 12;
                    wsKpi.Range(19, 7, 19, 10).Style.Font.FontColor = XLColor.FromHtml("#7C3AED");
                    wsKpi.Range(19, 7, 19, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Row 20: Table Headers
                    var ticketHeaders = new[] { "LOẠI VÉ / TICKET TYPE", "LƯỢT VÀO / ENTRIES", "LƯỢT RA / EXITS", "DOANH THU / REVENUE" };
                    for (int i = 0; i < ticketHeaders.Length; i++)
                    {
                        var cell = wsKpi.Cell(20, 2 + i);
                        cell.Value = ticketHeaders[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2196F3");
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    var vehicleHeaders = new[] { "LOẠI XE / VEHICLE TYPE", "LƯỢT XE / VISITS", "TRONG BÃI / INSIDE", "DOANH THU / REVENUE" };
                    for (int i = 0; i < vehicleHeaders.Length; i++)
                    {
                        var cell = wsKpi.Cell(20, 7 + i);
                        cell.Value = vehicleHeaders[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#7C3AED");
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    wsKpi.Row(20).Height = 22;

                    // Row 21+: Write Data
                    int currentTicketRow = 21;
                    foreach (var stat in listVeStats)
                    {
                        wsKpi.Cell(currentTicketRow, 2).Value = stat.TenLoaiVe;
                        wsKpi.Cell(currentTicketRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                        wsKpi.Cell(currentTicketRow, 3).Value = stat.SoLuotVao;
                        wsKpi.Cell(currentTicketRow, 3).Style.NumberFormat.Format = "#,##0";
                        wsKpi.Cell(currentTicketRow, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        wsKpi.Cell(currentTicketRow, 4).Value = stat.SoLuotRa;
                        wsKpi.Cell(currentTicketRow, 4).Style.NumberFormat.Format = "#,##0";
                        wsKpi.Cell(currentTicketRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        wsKpi.Cell(currentTicketRow, 5).Value = stat.DoanhThu;
                        wsKpi.Cell(currentTicketRow, 5).Style.NumberFormat.Format = "#,##0";
                        wsKpi.Cell(currentTicketRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        var range = wsKpi.Range(currentTicketRow, 2, currentTicketRow, 5);
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D1D1D1");
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1D1D1");
                        if (currentTicketRow % 2 == 1)
                        {
                            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9F9");
                        }

                        currentTicketRow++;
                    }

                    int currentVehicleRow = 21;
                    foreach (var stat in listXeStats)
                    {
                        wsKpi.Cell(currentVehicleRow, 7).Value = stat.TenLoaiXe;
                        wsKpi.Cell(currentVehicleRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                        wsKpi.Cell(currentVehicleRow, 8).Value = stat.SoLuot;
                        wsKpi.Cell(currentVehicleRow, 8).Style.NumberFormat.Format = "#,##0";
                        wsKpi.Cell(currentVehicleRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        wsKpi.Cell(currentVehicleRow, 9).Value = stat.DangTrongBai;
                        wsKpi.Cell(currentVehicleRow, 9).Style.NumberFormat.Format = "#,##0";
                        wsKpi.Cell(currentVehicleRow, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        wsKpi.Cell(currentVehicleRow, 10).Value = stat.DoanhThu;
                        wsKpi.Cell(currentVehicleRow, 10).Style.NumberFormat.Format = "#,##0";
                        wsKpi.Cell(currentVehicleRow, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        var range = wsKpi.Range(currentVehicleRow, 7, currentVehicleRow, 10);
                        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#D1D1D1");
                        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D1D1D1");
                        if (currentVehicleRow % 2 == 1)
                        {
                            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9F9");
                        }

                        currentVehicleRow++;
                    }

                    wsKpi.Column(2).Width = 30;
                    wsKpi.Column(3).Width = 15;
                    wsKpi.Column(4).Width = 15;
                    wsKpi.Column(5).Width = 20;
                    wsKpi.Column(6).Width = 5;
                    wsKpi.Column(7).Width = 30;
                    wsKpi.Column(8).Width = 15;
                    wsKpi.Column(9).Width = 15;
                    wsKpi.Column(10).Width = 20;

                    // --- Table Styling Helper ---
                    void StyleWorksheet(IXLWorksheet ws, string title)
                    {
                        var lastCol = ws.LastColumnUsed();
                        int colCount = lastCol != null ? lastCol.ColumnNumber() : 1;
                        if (colCount < 1) colCount = 1;

                        ws.Cell(1, 1).Value = title;
                        var titleRange = ws.Range(1, 1, 1, colCount);
                        titleRange.Merge().Style.Font.Bold = true;
                        titleRange.Style.Font.FontSize = 16;
                        titleRange.Style.Font.FontColor = headerColor;
                        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        ws.Cell(2, 1).Value = period;
                        ws.Range(2, 1, 2, colCount).Merge().Style.Font.Italic = true;
                        ws.Range(2, 1, 2, colCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        var headerRange = ws.Range(4, 1, 4, colCount);
                        headerRange.Style.Fill.BackgroundColor = headerColor;
                        headerRange.Style.Font.FontColor = headerFontColor;
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        ws.Row(4).Height = 25;

                        var lastRowUsed = ws.LastRowUsed();
                        int lastRow = lastRowUsed != null ? lastRowUsed.RowNumber() : 4;

                        if (lastRow >= 5)
                        {
                            var dataRange = ws.Range(5, 1, lastRow, colCount);
                            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                            dataRange.Style.Border.InsideBorderColor = borderColor;
                            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            dataRange.Style.Border.OutsideBorderColor = borderColor;

                            for (int i = 5; i <= lastRow; i += 2)
                            {
                                ws.Range(i, 1, i, colCount).Style.Fill.BackgroundColor = stripeColor;
                            }
                        }

                        ws.Columns().AdjustToContents();
                        ws.SheetView.FreezeRows(4);
                    }

                    // --- Sheet 2: Revenue ---
                    var wsRev = workbook.Worksheets.Add("Revenue");
                    wsRev.Cell(4, 1).InsertTable(revDt);
                    wsRev.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsRev.Column(2).Style.NumberFormat.Format = "#,##0";
                    wsRev.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    StyleWorksheet(wsRev, "THỐNG KÊ DOANH THU THEO NGÀY");

                    // --- Sheet 3: Entries ---
                    var wsEntries = workbook.Worksheets.Add("Entries");
                    wsEntries.Cell(4, 1).InsertTable(entriesDt);
                    wsEntries.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsEntries.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    StyleWorksheet(wsEntries, "PHÂN TÍCH LƯỢT XE VÀO THEO GIỜ");

                    // --- Sheet 4: Transactions ---
                    var wsTrans = workbook.Worksheets.Add("Transactions");
                    wsTrans.Cell(4, 1).InsertTable(transDt);
                    wsTrans.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(4).Style.NumberFormat.Format = "#,##0";
                    wsTrans.Column(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    wsTrans.Column(5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    wsTrans.Column(9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    StyleWorksheet(wsTrans, "DANH SÁCH CHI TIẾT GIAO DỊCH");

                    // 4. Save file
                    string fileName = $"Report_{FromDate:yyyy-MM-dd}_to_{ToDate:yyyy-MM-dd}.xlsx";
                    filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                    workbook.SaveAs(filePath);
                }

                // Audit Log
                try
                {
                    LoggingService.Instance.LogAudit(
                        "EXPORT_DASHBOARD", 
                        "Dashboard", 
                        null, 
                        null, 
                        new { Filename = Path.GetFileName(filePath), FromDate = FromDate, ToDate = ToDate },
                        source: "Dashboard",
                        details: $"Dashboard report exported for period {FromDate:dd/MM/yyyy} - {ToDate:dd/MM/yyyy}");
                }
                catch { }

                // 5. Success Notification
                MessageBox.Show($"Xuất báo cáo thành công!\nFile đã được lưu tại: {filePath}", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // 6. Open file
                var p = new Process();
                p.StartInfo = new ProcessStartInfo(filePath) { UseShellExecute = true };
                p.Start();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardViewModel", "ExportExcelAsync", "Lỗi xuất báo cáo", ex);
                MessageBox.Show("Có lỗi xảy ra khi xuất báo cáo: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }


        private (DateTime start, DateTime end) GetDateRange(string option)
        {
            DateTime now = DateTime.Now;
            DateTime today = DateTime.Today;

            switch (option)
            {
                case "Hôm nay":
                    return (today, now);
                case "Hôm qua":
                    return (today.AddDays(-1), today.AddTicks(-1));
                case "7 ngày qua":
                    return (today.AddDays(-6), now);
                case "30 ngày qua":
                    return (today.AddDays(-29), now);
                case "Tháng này":
                    return (new DateTime(today.Year, today.Month, 1), now);
                case "Tháng trước":
                    var firstDayLastMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    var lastDayLastMonth = new DateTime(today.Year, today.Month, 1).AddTicks(-1);
                    return (firstDayLastMonth, lastDayLastMonth);
                default:
                    return (today, now);
            }
        }

        private void UpdateRangeDisplay()
        {
            SelectedDateRangeDisplay = $"📅 {FromDate:dd/MM/yyyy} → {ToDate:dd/MM/yyyy}";
        }

        private async Task LoadDataAsync()
        {
            if (IsLoading) return;

            // Simple cache check
            string currentRangeKey = $"{FromDate:yyyyMMddHHmm}-{ToDate:yyyyMMddHHmm}";
            if (currentRangeKey == _lastLoadedRange && SelectedFilter != "Hôm nay") return;

            // 90-day limit validation
            if ((ToDate - FromDate).TotalDays > 90)
            {
                LoggingService.Instance.LogInfo("Dashboard", "LoadDataAsync", "Range exceeded 90 days. Capping range.");
            }

            IsLoading = true;
            try
            {
                int? siteIdFilter = SelectedSite?.Id > 0 ? SelectedSite.Id : (int?)null;
                int? zoneIdFilter = SelectedZone?.Id > 0 ? SelectedZone.Id : (int?)null;

                // Load KPI
                var kpiTask = _service.GetKpiAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                var revTask = _service.GetRevenueByDayAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                var hourlyTask = _service.GetEntriesByHourAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                var transTask = _service.GetTransactionsAsync(FromDate, ToDate, siteIdFilter, zoneIdFilter);
                
                await System.Threading.Tasks.Task.WhenAll(kpiTask, revTask, hourlyTask, transTask);

                var kpi = await kpiTask;
                XeTrongBai = kpi.XeTrongBai;
                LuotXeVao = kpi.LuotXeVao;
                DoanhThu = kpi.DoanhThu;
                VeActive = kpi.VeActive;

                TongCho = kpi.TongCho;

                OnPropertyChanged(nameof(TyLeLapDay));
                OnPropertyChanged(nameof(ChoTrong));
                OnPropertyChanged(nameof(IsNearFull));

                // Load Charts
                LoadRevenueChart(await revTask);
                LoadHourlyChart(await hourlyTask);

                // Load Recent Activities
                var activities = await _service.GetRecentActivitiesAsync(siteIdFilter, zoneIdFilter);
                if (RecentActivities != null)
                {
                    RecentActivities.Clear();
                    foreach (var act in activities)
                    {
                        RecentActivities.Add(act);
                    }
                }

                // Calculate detailed statistics from the transactions DataTable on the background thread
                var transDt = await transTask;

                var listVeStats = transDt.AsEnumerable()
                    .GroupBy(row => string.IsNullOrWhiteSpace(row.Field<string>("Loại Vé")) ? "Vé lượt" : row.Field<string>("Loại Vé"))
                    .Select(g => new LoaiVeStats
                    {
                        TenLoaiVe = g.Key,
                        SoLuotVao = g.Count(r => r.Field<DateTime?>("Giờ Vào") != null),
                        SoLuotRa = g.Count(r => r.Field<DateTime?>("Giờ Ra") != null),
                        DoanhThu = g.Sum(r => r.Field<double?>("Số Tiền") ?? 0)
                    })
                    .OrderByDescending(x => x.SoLuotVao)
                    .ToList();

                var listXeStats = transDt.AsEnumerable()
                    .GroupBy(row => string.IsNullOrWhiteSpace(row.Field<string>("Loại Xe")) ? "Không xác định" : row.Field<string>("Loại Xe"))
                    .Select(g => new LoaiXeStats
                    {
                        TenLoaiXe = g.Key,
                        SoLuot = g.Count(),
                        DangTrongBai = g.Count(r => r.Field<string>("Trạng Thái") == "Trong bãi" || r.Field<DateTime?>("Giờ Ra") == null),
                        DoanhThu = g.Sum(r => r.Field<double?>("Số Tiền") ?? 0)
                    })
                    .OrderByDescending(x => x.SoLuot)
                    .ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ThongKeLoaiVe.Clear();
                    foreach (var item in listVeStats) ThongKeLoaiVe.Add(item);

                    ThongKeLoaiXe.Clear();
                    foreach (var item in listXeStats) ThongKeLoaiXe.Add(item);
                });

                _lastLoadedRange = currentRangeKey;
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardViewModel", "LoadDataAsync", "Error loading dashboard", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadRevenueChart(System.Data.DataTable dt)
        {
            try
            {
                if (RevenueSeries == null || RevenueLabels == null) return;

                RevenueSeries.Clear();
                RevenueLabels.Clear();

                if (dt == null || !dt.Columns.Contains("Ngay") || !dt.Columns.Contains("DoanhThu"))
                    return;

                var values = new ChartValues<double>();

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    if (row == null) continue;

                    object ngayVal = row["Ngay"];
                    object doanhThuVal = row["DoanhThu"];

                    if (ngayVal == null || ngayVal == DBNull.Value ||
                        doanhThuVal == null || doanhThuVal == DBNull.Value)
                        continue;

                    try
                    {
                        DateTime date = Convert.ToDateTime(ngayVal);
                        double rev = Convert.ToDouble(doanhThuVal);

                        RevenueLabels.Add(date.ToString("dd/MM"));
                        values.Add(rev);
                    }
                    catch (Exception valEx)
                    {
                        LoggingService.Instance.LogError("DashboardViewModel", "LoadRevenueChart_Row", "Error parsing row values", valEx);
                    }
                }

                RevenueSeries.Add(new LineSeries
                {
                    Title = "Doanh thu",
                    Values = values,
                    PointGeometrySize = 10,
                    StrokeThickness = 3,
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)),
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 39, 174, 96))
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardViewModel", "LoadRevenueChart", "Error loading revenue chart", ex);
            }
        }

        private void LoadHourlyChart(System.Data.DataTable dt)
        {
            try
            {
                if (HourlySeries == null || HourlyLabels == null) return;

                HourlySeries.Clear();
                HourlyLabels.Clear();

                if (dt == null || !dt.Columns.Contains("Gio") || !dt.Columns.Contains("SoLuot"))
                    return;

                var values = new ChartValues<double>();

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    if (row == null) continue;

                    object gioVal = row["Gio"];
                    object soLuotVal = row["SoLuot"];

                    if (gioVal == null || gioVal == DBNull.Value ||
                        soLuotVal == null || soLuotVal == DBNull.Value)
                        continue;

                    try
                    {
                        int hour = Convert.ToInt32(gioVal);
                        double count = Convert.ToDouble(soLuotVal);

                        HourlyLabels.Add(hour.ToString("00") + ":00");
                        values.Add(count);
                    }
                    catch (Exception valEx)
                    {
                        LoggingService.Instance.LogError("DashboardViewModel", "LoadHourlyChart_Row", "Error parsing row values", valEx);
                    }
                }

                HourlySeries.Add(new ColumnSeries
                {
                    Title = "Lượt vào",
                    Values = values,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226))
                });
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("DashboardViewModel", "LoadHourlyChart", "Error loading hourly chart", ex);
            }
        }

        // Clean up
        public void Dispose()
        {
            _timer?.Stop();
        }
    }
}
