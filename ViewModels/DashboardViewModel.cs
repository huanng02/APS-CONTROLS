using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading.Tasks;
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
        public SeriesCollection RevenueSeries { get; set; }
        public ObservableCollection<string> RevenueLabels { get; set; }
        public Func<double, string> RevenueFormatter { get; set; }

        public SeriesCollection HourlySeries { get; set; }
        public ObservableCollection<string> HourlyLabels { get; set; }
        public Func<double, string> HourlyFormatter { get; set; }

        // Recent Activity
        public ObservableCollection<HoatDongGhiNhan> RecentActivities { get; set; }

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

            RevenueSeries = new SeriesCollection();
            RevenueLabels = new ObservableCollection<string>();
            RevenueFormatter = value => value.ToString("N0") + " đ";

            HourlySeries = new SeriesCollection();
            HourlyLabels = new ObservableCollection<string>();
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
                var kpiTask = _service.GetKpiAsync(FromDate, ToDate);
                var revTask = _service.GetRevenueByDayAsync(FromDate, ToDate);
                var entriesTask = _service.GetEntriesByHourAsync(FromDate, ToDate);
                var transTask = _service.GetTransactionsAsync(FromDate, ToDate);

                await System.Threading.Tasks.Task.WhenAll(kpiTask, revTask, entriesTask, transTask);

                var kpi = await kpiTask;
                var revDt = await revTask;
                var entriesDt = await entriesTask;
                var transDt = await transTask;

                // 2. Validation
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

                    string period = $"BÁO CÁO TỪ {FromDate:dd/MM/yyyy} ĐẾN {ToDate:dd/MM/yyyy}";

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

                    // --- Table Styling Helper ---
                    void StyleWorksheet(IXLWorksheet ws, string title)
                    {
                        int colCount = ws.LastColumnUsed().ColumnNumber();
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

                        int lastRow = ws.LastRowUsed().RowNumber();
                        var dataRange = ws.Range(5, 1, lastRow, colCount);
                        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        dataRange.Style.Border.InsideBorderColor = borderColor;
                        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        dataRange.Style.Border.OutsideBorderColor = borderColor;

                        for (int i = 5; i <= lastRow; i += 2)
                        {
                            ws.Range(i, 1, i, colCount).Style.Fill.BackgroundColor = stripeColor;
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
                    wsTrans.Column(4).Style.NumberFormat.Format = "#,##0";
                    wsTrans.Column(4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    StyleWorksheet(wsTrans, "DANH SÁCH CHI TIẾT GIAO DỊCH");

                    // 4. Save file
                    string fileName = $"Report_{FromDate:yyyy-MM-dd}_to_{ToDate:yyyy-MM-dd}.xlsx";
                    filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                    workbook.SaveAs(filePath);
                }

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
                // In a real app, we'd show a message box. Here we can just cap it or set an error.
                // For this task, we'll just log and maybe show a warning in UI if we had a property.
                LoggingService.Instance.LogInfo("Dashboard", "LoadDataAsync", "Range exceeded 90 days. Capping range.");
            }

            IsLoading = true;
            try
            {
                // Load KPI
                var kpiTask = _service.GetKpiAsync(FromDate, ToDate);
                var revTask = _service.GetRevenueByDayAsync(FromDate, ToDate);
                var hourlyTask = _service.GetEntriesByHourAsync(FromDate, ToDate);
                
                await System.Threading.Tasks.Task.WhenAll(kpiTask, revTask, hourlyTask);

                var kpi = await kpiTask;
                XeTrongBai = kpi.XeTrongBai;
                LuotXeVao = kpi.LuotXeVao;
                DoanhThu = kpi.DoanhThu;
                VeActive = kpi.VeActive;

                OnPropertyChanged(nameof(TyLeLapDay));
                OnPropertyChanged(nameof(ChoTrong));
                OnPropertyChanged(nameof(IsNearFull));

                // Load Charts
                LoadRevenueChart(await revTask);
                LoadHourlyChart(await hourlyTask);

                // Load Recent Activities (Sync for now as it doesn't take params in service yet)
                var activities = _service.GetRecentActivities();
                RecentActivities.Clear();
                foreach (var act in activities)
                {
                    RecentActivities.Add(act);
                }

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
            RevenueSeries.Clear();
            RevenueLabels.Clear();

            var values = new ChartValues<double>();

            foreach (System.Data.DataRow row in dt.Rows)
            {
                DateTime date = Convert.ToDateTime(row["Ngay"]);
                double rev = Convert.ToDouble(row["DoanhThu"]);

                RevenueLabels.Add(date.ToString("dd/MM"));
                values.Add(rev);
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

        private void LoadHourlyChart(System.Data.DataTable dt)
        {
            HourlySeries.Clear();
            HourlyLabels.Clear();

            var values = new ChartValues<double>();

            foreach (System.Data.DataRow row in dt.Rows)
            {
                int hour = Convert.ToInt32(row["Gio"]);
                double count = Convert.ToDouble(row["SoLuot"]);

                HourlyLabels.Add(hour.ToString("00") + ":00");
                values.Add(count);
            }

            HourlySeries.Add(new ColumnSeries
            {
                Title = "Lượt vào",
                Values = values,
                Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226))
            });
        }

        // Clean up
        public void Dispose()
        {
            _timer?.Stop();
        }
    }
}
