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
using QuanLyGiuXe.Services.OfflineCache;

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

        private CancellationTokenSource? _searchCts;

        public LichSuViewModel()
        {
            TrangTruocCommand = new RelayCommand(_ => { if (TrangHienTai > 1) { TrangHienTai--; LoadTrangAsync(); } });
            TrangSauCommand = new RelayCommand(_ => { if (TrangHienTai < TongTrang) { TrangHienTai++; LoadTrangAsync(); } });
            TrangDauCommand = new RelayCommand(_ => { TrangHienTai = 1; LoadTrangAsync(); });
            TrangCuoiCommand = new RelayCommand(_ => { TrangHienTai = TongTrang; LoadTrangAsync(); });
            ResetFilterCommand = new RelayCommand(_ => ResetFilter());
            ExportExcelCommand = new RelayCommand(_ => ExportExcel());

            // Default dates
            TuNgay = DateTime.Today;
            DenNgay = DateTime.Today.AddDays(1).AddTicks(-1);

            _ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            try
            {
                var data = await Task.Run(() => db.LayLichSu().ToList());
                
                // Fetch local offline sessions to show as "Pending"
                var localSessions = await OfflineCacheService.Instance.GetAllLocalSessionsAsync();
                foreach (var session in localSessions)
                {
                    data.Add(new LichSuXe
                    {
                        Id = -session.Id, // Use negative ID to distinguish local records
                        BienSo = session.BienSoXe + " (OFFLINE)",
                        ThoiGianVao = session.ThoiGianVao,
                        ThoiGianRa = session.ThoiGianRa,
                        Tien = 0, // Fee logic might need improvement here
                        CardId = 0 // CardId mapping needed if possible
                    });
                }

                TatCaLichSu = data;
                CalculateStats(data);
                await LoadTrangAsync();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("HistoryLoad", "LichSuViewModel", "Lỗi tải lịch sử ban đầu", ex);
            }
        }

        private void CalculateStats(List<LichSuXe> data)
        {
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                TongLuotXe = data.Count;
                TongDoanhThu = data.Sum(x => x.Tien ?? 0);
                
                var today = DateTime.Today;
                var dataToday = data.Where(x => x.ThoiGianVao.Date == today || (x.ThoiGianRa.HasValue && x.ThoiGianRa.Value.Date == today)).ToList();
                XeHomNay = dataToday.Count;
                DoanhThuHomNay = dataToday.Sum(x => x.Tien ?? 0);
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
                    (!string.IsNullOrEmpty(x.BienSo) && x.BienSo.ToLower().Contains(keyword)));
            }

            if (TuNgay.HasValue)
                query = query.Where(x => x.ThoiGianVao.Date >= TuNgay.Value.Date);

            if (DenNgay.HasValue)
                query = query.Where(x => (x.ThoiGianRa ?? x.ThoiGianVao).Date <= DenNgay.Value.Date);

            return query.OrderByDescending(x => x.ThoiGianRa ?? x.ThoiGianVao);
        }

        public async Task LoadTrangAsync()
        {
            try
            {
                var filtered = await Task.Run(() => GetFilteredData().ToList());
                _filteredCount = filtered.Count;

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

            TrangHienTai = 1;
            _ = LoadTrangAsync();
        }

        private async void ExportExcel()
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    Title = "Lưu danh sách lịch sử ra vào",
                    FileName = $"LichSuXe_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                };

                if (sfd.ShowDialog() == true)
                {
                    var dataToExport = await Task.Run(() => GetFilteredData().ToList());

                    await Task.Run(() =>
                    {
                        using var wb = new XLWorkbook();
                        var ws = wb.Worksheets.Add("LichSu");
                        
                        // Header
                        ws.Cell(1, 1).Value = "ID";
                        ws.Cell(1, 2).Value = "Biển số";
                        ws.Cell(1, 3).Value = "Thời gian vào";
                        ws.Cell(1, 4).Value = "Thời gian ra";
                        ws.Cell(1, 5).Value = "Tiền (VNĐ)";
                        ws.Cell(1, 6).Value = "Card ID";
                        
                        // Data
                        for (int i = 0; i < dataToExport.Count; i++)
                        {
                            var row = i + 2;
                            var item = dataToExport[i];
                            
                            ws.Cell(row, 1).Value = item.Id;
                            ws.Cell(row, 2).Value = item.BienSo;
                            ws.Cell(row, 3).Value = item.ThoiGianVao;
                            ws.Cell(row, 4).Value = item.ThoiGianRa;
                            ws.Cell(row, 5).Value = item.Tien;
                            ws.Cell(row, 6).Value = item.CardId;
                        }
                        
                        ws.Columns().AdjustToContents();
                        wb.SaveAs(sfd.FileName);
                    });

                    MessageBox.Show("Xuất file Excel thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoggingService.Instance.LogSecurity("EXPORT", "LichSuViewModel", $"Exported {dataToExport.Count} rows to {sfd.FileName}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("ExportExcel", "LichSuViewModel", "Lỗi xuất Excel", ex);
                MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}