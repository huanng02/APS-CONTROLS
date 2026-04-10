using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService db = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            try
            {
                // avoid logging the realtime log collection changes to prevent recursion/noise
                if (string.Equals(name, nameof(LogEntries), StringComparison.OrdinalIgnoreCase))
                    return;

                // try to read property value via reflection for context
                string val = string.Empty;
                try
                {
                    var pi = this.GetType().GetProperty(name);
                    if (pi != null)
                    {
                        var v = pi.GetValue(this);
                        val = v?.ToString() ?? "";
                    }
                }
                catch { }

                // emit a property-changed audit entry
                try { QuanLyGiuXe.Services.LoggingService.Instance.LogInfo("PropertyChanged", "MainViewModel", $"{name}={val}"); } catch { }
            }
            catch { }
        }

        // ── Properties ────────────────────────────────────────────────────────────

        private string _bienSoNhap = "";
        public string BienSoNhap
        {
            get => _bienSoNhap;
            set { _bienSoNhap = value; OnPropertyChanged(nameof(BienSoNhap)); }
        }

        private string _tienHienThi = "";
        public string TienHienThi
        {
            get => _tienHienThi;
            set { _tienHienThi = value; OnPropertyChanged(nameof(TienHienThi)); }
        }

        private string _tuKhoaTimKiem = "";
        public string TuKhoaTimKiem
        {
            get => _tuKhoaTimKiem;
            set { _tuKhoaTimKiem = value; OnPropertyChanged(nameof(TuKhoaTimKiem)); TimKiemXe(); }
        }

        public string LastScannedUID { get; set; } = "";

        public object CurrentView { get; set; }
        public ObservableCollection<Xe> DanhSachXe { get; set; }

        // ── Làn Vào ──────────────────────────────────────────────────────────────

        private string _lanVaoBienSo = "";
        public string LanVaoBienSo
        {
            get => _lanVaoBienSo;
            set { _lanVaoBienSo = value; OnPropertyChanged(nameof(LanVaoBienSo)); }
        }

        private string _lanVaoTrangThai = "Chờ xe vào...";
        public string LanVaoTrangThai
        {
            get => _lanVaoTrangThai;
            set { _lanVaoTrangThai = value; OnPropertyChanged(nameof(LanVaoTrangThai)); }
        }

        // ── Làn Ra ───────────────────────────────────────────────────────────────

        private string _lanRaBienSo = "";
        public string LanRaBienSo
        {
            get => _lanRaBienSo;
            set { _lanRaBienSo = value; OnPropertyChanged(nameof(LanRaBienSo)); }
        }

        private string _lanRaTrangThai = "Chờ xe ra...";
        public string LanRaTrangThai
        {
            get => _lanRaTrangThai;
            set { _lanRaTrangThai = value; OnPropertyChanged(nameof(LanRaTrangThai)); }
        }

        private string _lanRaTien = "";
        public string LanRaTien
        {
            get => _lanRaTien;
            set { _lanRaTien = value; OnPropertyChanged(nameof(LanRaTien)); }
        }

        // ── Thông tin thêm ───────────────────────────────────────────────────────

        private string _lanVaoUID = "";
        public string LanVaoUID
        {
            get => _lanVaoUID;
            set { _lanVaoUID = value; OnPropertyChanged(nameof(LanVaoUID)); }
        }

        private string _lanRaThoiGianVao = "";
        public string LanRaThoiGianVao
        {
            get => _lanRaThoiGianVao;
            set { _lanRaThoiGianVao = value; OnPropertyChanged(nameof(LanRaThoiGianVao)); }
        }

        private string _lanRaThoiGianTrongBai = "";
        public string LanRaThoiGianTrongBai
        {
            get => _lanRaThoiGianTrongBai;
            set { _lanRaThoiGianTrongBai = value; OnPropertyChanged(nameof(LanRaThoiGianTrongBai)); }
        }

        private string _trangThaiKetNoi = "C3200: Đang kết nối...";
        public string TrangThaiKetNoi
        {
            get => _trangThaiKetNoi;
            set { _trangThaiKetNoi = value; OnPropertyChanged(nameof(TrangThaiKetNoi)); }
        }

        public string SoXeTrongBai => $"Xe trong bãi: {DanhSachXe?.Count ?? 0}";

        public ObservableCollection<Services.LogEntry> LogEntries { get; } = new();

        private void ThemLog(string dir, string bienSo, string status)
        {
            var entry = new Services.LogEntry
            {
                Timestamp = DateTime.Now,
                Level = "Info",
                EventType = dir,
                Source = "UI",
                UserId = string.Empty,
                Plate = bienSo,
                Details = status,
                Exception = null
            };
            if (LogEntries.Count > 200) LogEntries.RemoveAt(0);
            LogEntries.Add(entry);
        }

        // ── Ảnh biển số ─────────────────────────────────────────────────────────────

        private ImageSource? _anhBienSoVao;
        public ImageSource? AnhBienSoVao
        {
            get => _anhBienSoVao;
            set { _anhBienSoVao = value; OnPropertyChanged(nameof(AnhBienSoVao)); }
        }

        private ImageSource? _anhBienSoRaVao;
        public ImageSource? AnhBienSoRaVao
        {
            get => _anhBienSoRaVao;
            set { _anhBienSoRaVao = value; OnPropertyChanged(nameof(AnhBienSoRaVao)); }
        }

        private ImageSource? _anhBienSoRaRa;
        public ImageSource? AnhBienSoRaRa
        {
            get => _anhBienSoRaRa;
            set { _anhBienSoRaRa = value; OnPropertyChanged(nameof(AnhBienSoRaRa)); }
        }

        // ── Ảnh chụp từ 2 cam (snapshot khi xe vào/ra) ──────────────────────────

        private ImageSource? _anhChupVao1;
        public ImageSource? AnhChupVao1
        {
            get => _anhChupVao1;
            set { _anhChupVao1 = value; OnPropertyChanged(nameof(AnhChupVao1)); }
        }

        private ImageSource? _anhChupVao2;
        public ImageSource? AnhChupVao2
        {
            get => _anhChupVao2;
            set { _anhChupVao2 = value; OnPropertyChanged(nameof(AnhChupVao2)); }
        }

        private ImageSource? _anhChupRa1;
        public ImageSource? AnhChupRa1
        {
            get => _anhChupRa1;
            set { _anhChupRa1 = value; OnPropertyChanged(nameof(AnhChupRa1)); }
        }

        private ImageSource? _anhChupRa2;
        public ImageSource? AnhChupRa2
        {
            get => _anhChupRa2;
            set { _anhChupRa2 = value; OnPropertyChanged(nameof(AnhChupRa2)); }
        }

        // ── Log toggle ────────────────────────────────────────────────────────────

        private bool _showLog;
        public bool ShowLog
        {
            get => _showLog;
            set { _showLog = value; OnPropertyChanged(nameof(ShowLog)); }
        }

        // ── Commands ──────────────────────────────────────────────────────────────

        public ICommand XeVaoCommand { get; }
        public ICommand XeRaCommand { get; }
        public ICommand XeChiTietCommand { get; }
        public ICommand TrangChuCommand { get; }
        public ICommand TimKiemCommand { get; }
        public ICommand LichSuCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        public MainViewModel()
        {
            var cfg = AppConfig.Load();
            _showLog = cfg.ShowLog;

            var zk = cfg.ZKTeco;
            C3200Service.Instance.Configure(
                ip: zk.IpAddress, port: zk.TcpPort,
                password: zk.Password, timeoutMs: zk.Timeout,
                barrierDuration: zk.BarrierDuration);
            _ = C3200Service.Instance.ConnectAsync();

            DanhSachXe = new ObservableCollection<Xe>();
            DanhSachXe.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SoXeTrongBai));

            XeVaoCommand = new RelayCommand(async () => await XeVaoAsync());
            XeRaCommand = new RelayCommand(async () => await XeRaAsync());
            XeChiTietCommand = new RelayCommand<Xe>(XeChiTiet);

            C3200Service.Instance.OnConnectionChanged += online =>
                Application.Current?.Dispatcher?.Invoke(() =>
                    TrangThaiKetNoi = online ? "C3200: Online ●" : "C3200: Offline ○");

            CurrentView = new TrangChuViewModel();
            TrangChuCommand = new RelayCommand(() => SetView(new TrangChuViewModel()));
            TimKiemCommand = new RelayCommand(() => SetView(new TimKiemViewModel()));
            LichSuCommand = new RelayCommand(() => SetView(new LichSuViewModel()));

            LoadXeTrongBai();

            // subscribe to emitted log events so UI shows realtime app logs
            try
            {
                QuanLyGiuXe.Services.LoggingService.Instance.LogEmitted += OnLogEmitted;
            }
            catch { }
        }

        private void OnLogEmitted(Services.LogEntry entry)
        {
            try
            {
                // add the LogEntry object directly so UI grid shows fields
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (LogEntries.Count > 200) LogEntries.RemoveAt(0);
                    // convert timestamp to local time for display
                    entry.Timestamp = entry.Timestamp.ToLocalTime();
                    LogEntries.Add(entry);
                });
            }
            catch { }
        }

        private void SetView(object view)
        {
            CurrentView = view;
            OnPropertyChanged(nameof(CurrentView));
        }

        private void LoadXeTrongBai()
        {
            foreach (DataRow row in db.LayXeTrongBai().Rows)
            {
                var xe = new Xe
                {
                    BienSo = row["BienSo"].ToString()!,
                    ThoiGianVao = Convert.ToDateTime(row["ThoiGianVao"])
                };
                DanhSachXe.Add(xe);
            }
        }

        // ── Xe Vào / Ra ──────────────────────────────────────────────────────────

        public async Task XeVaoAsync()
        {
            string bienSo = new string(BienSoNhap?.Where(char.IsLetterOrDigit).ToArray()).ToUpper();

            if (string.IsNullOrEmpty(bienSo))
            { LanVaoTrangThai = "❌ Vui lòng nhập biển số!"; return; }

            var regex = new System.Text.RegularExpressions.Regex(@"^\d{2}([A-Z]\d{5,6}|[A-Z]{1,2}\d{4,5})$");
            if (!regex.IsMatch(bienSo))
            { LanVaoTrangThai = "❌ Biển số không đúng định dạng!"; return; }

            if (DanhSachXe.Any(x => x.BienSo == bienSo))
            { LanVaoTrangThai = "⚠ Xe này đã vào bãi!"; return; }

            if (!Directory.Exists("Images"))
                Directory.CreateDirectory("Images");

            var xe = new Xe { BienSo = bienSo, ThoiGianVao = DateTime.Now };
            DanhSachXe.Add(xe);

            string uid = string.IsNullOrEmpty(LastScannedUID) ? "MANUAL" : LastScannedUID;
            db.ThemXe(bienSo, uid, "");
            LastScannedUID = "";

            LanVaoBienSo = bienSo;
            LanVaoUID = uid;
            bool opened = await C3200Service.Instance.OpenBarrierAsync(1);
            LanVaoTrangThai = opened
                ? $"✅ Xe vào lúc {DateTime.Now:HH:mm} – barrier đã mở"
                : "⚠ Xe vào – barrier lỗi";
            // log event
            try { QuanLyGiuXe.Services.LoggingService.Instance.LogInfo("XeVao", "MainViewModel", $"BienSo={bienSo}; opened={opened}", userId: uid, plate: bienSo); } catch { }
            ThemLog("VÀO", bienSo, opened ? "✅ Barrier đã mở" : "⚠ Barrier lỗi");
            BienSoNhap = "";
        }

        private async Task XeRaAsync()
        {
            var xe = DanhSachXe.FirstOrDefault(x => x.BienSo == BienSoNhap);
            if (xe == null)
            { LanRaTrangThai = "❌ Xe này không có trong bãi!"; return; }

            LanRaBienSo = xe.BienSo;
            var thoiGian = DateTime.Now - xe.ThoiGianVao;
            LanRaThoiGianVao = $"Vào: {xe.ThoiGianVao:HH:mm} │ {thoiGian.Hours}h{thoiGian.Minutes:D2}m";
            LanRaThoiGianTrongBai = $"Thời gian trong bãi: {thoiGian.Days}d {thoiGian.Hours}h{thoiGian.Minutes:D2}m";
            double tien = Math.Ceiling(thoiGian.TotalHours) * 5000;
            LanRaTien = $"💰 {tien:N0} VNĐ";
            TienHienThi = $"Tiền: {tien:N0} VNĐ";

            db.LuuLichSu(xe.BienSo, xe.ThoiGianVao, DateTime.Now, tien, "");
            db.XoaXe(BienSoNhap);
            DanhSachXe.Remove(xe);
            BienSoNhap = "";

            await C3200Service.Instance.OpenBarrierAsync(2);
            LanRaTrangThai = $"✅ Xe ra lúc {DateTime.Now:HH:mm} – barrier đã mở";
            try { QuanLyGiuXe.Services.LoggingService.Instance.LogInfo("XeRa", "MainViewModel", $"BienSo={xe.BienSo}; tien={tien}", userId: null, plate: xe.BienSo); } catch { }
            ThemLog("RA", xe.BienSo, $"💰 {tien:N0} VNĐ");
        }

        // ── Tìm kiếm / Chi tiết ──────────────────────────────────────────────────

        private void TimKiemXe()
        {
            DanhSachXe.Clear();
            var source = string.IsNullOrWhiteSpace(TuKhoaTimKiem)
                ? db.LayXeTrongBai().AsEnumerable()
                : db.LayXeTrongBai().AsEnumerable()
                    .Where(r => r["BienSo"].ToString()!.Contains(TuKhoaTimKiem));

            foreach (var row in source)
                DanhSachXe.Add(new Xe
                {
                    BienSo = row["BienSo"].ToString()!,
                    ThoiGianVao = Convert.ToDateTime(row["ThoiGianVao"])
                });
        }

        public void XeChiTiet(Xe xe)
        {
            if (xe == null) return;
            new Views.VehicleDetailWindow(xe).ShowDialog();
        }
    }
}
