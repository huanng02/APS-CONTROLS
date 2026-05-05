using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using System.Windows.Threading;
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
                    LoadData();
                }
            }
        }

        public ICommand RefreshCommand { get; }

        public DashboardViewModel()
        {
            FilterOptions = new ObservableCollection<string> { "Hôm nay", "7 ngày qua", "30 ngày qua" };
            _selectedFilter = "Hôm nay";

            RevenueSeries = new SeriesCollection();
            RevenueLabels = new ObservableCollection<string>();
            RevenueFormatter = value => value.ToString("N0") + " đ";

            HourlySeries = new SeriesCollection();
            HourlyLabels = new ObservableCollection<string>();
            HourlyFormatter = value => value.ToString("N0");

            RecentActivities = new ObservableCollection<HoatDongGhiNhan>();

            RefreshCommand = new RelayCommand(_ => LoadData());

            LoadData();

            // Realtime Update every 10 seconds
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _timer.Tick += (s, e) => LoadData();
            _timer.Start();
        }

        private void LoadData()
        {
            DateTime start = DateTime.Today;
            DateTime end = DateTime.Now;

            if (SelectedFilter == "7 ngày qua")
            {
                start = DateTime.Today.AddDays(-6);
            }
            else if (SelectedFilter == "30 ngày qua")
            {
                start = DateTime.Today.AddDays(-29);
            }

            // Load KPI
            var kpi = _service.GetKpi(start, end);
            XeTrongBai = kpi.XeTrongBai;
            LuotXeVao = kpi.LuotXeVao;
            DoanhThu = kpi.DoanhThu;
            VeActive = kpi.VeActive;

            OnPropertyChanged(nameof(TyLeLapDay));
            OnPropertyChanged(nameof(ChoTrong));
            OnPropertyChanged(nameof(IsNearFull));

            // Load Revenue Chart
            var revData = _service.GetRevenueByDay(start, end);
            LoadRevenueChart(revData);

            // Load Hourly Chart (only makes sense for "Hôm nay" usually, but can aggregate)
            var hourlyData = _service.GetEntriesByHour(start, end);
            LoadHourlyChart(hourlyData);

            // Load Recent Activities
            var activities = _service.GetRecentActivities();
            RecentActivities.Clear();
            foreach (var act in activities)
            {
                RecentActivities.Add(act);
            }
        }

        private void LoadRevenueChart(DataTable dt)
        {
            RevenueSeries.Clear();
            RevenueLabels.Clear();

            var values = new ChartValues<double>();

            foreach (DataRow row in dt.Rows)
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

        private void LoadHourlyChart(DataTable dt)
        {
            HourlySeries.Clear();
            HourlyLabels.Clear();

            var values = new ChartValues<double>();

            foreach (DataRow row in dt.Rows)
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
