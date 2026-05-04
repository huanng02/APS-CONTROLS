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

        private bool _newestOnTop = true;
        public bool NewestOnTop
        {
            get => _newestOnTop;
            set { _newestOnTop = value; OnPropertyChanged(nameof(NewestOnTop)); }
        }

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
            // insert according to NewestOnTop preference
            if (NewestOnTop)
            {
                if (LogEntries.Count > 200) LogEntries.RemoveAt(LogEntries.Count - 1);
                LogEntries.Insert(0, entry);
            }
            else
            {
                if (LogEntries.Count > 200) LogEntries.RemoveAt(0);
                LogEntries.Add(entry);
            }
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
        public ICommand DatabaseExplorerCommand { get; }

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

            XeVaoCommand = new RelayCommand(async _ => await XeVaoAsync());
            XeRaCommand = new RelayCommand(async _ => await XeRaAsync());
            XeChiTietCommand = new RelayCommand<Xe>(XeChiTiet);

            C3200Service.Instance.OnConnectionChanged += online =>
                Application.Current?.Dispatcher?.Invoke(() =>
                    TrangThaiKetNoi = online ? "C3200: Online ●" : "C3200: Offline ○");

            CurrentView = new TrangChuViewModel();

            TrangChuCommand = new RelayCommand(_ => SetView(new TrangChuViewModel()));
            TimKiemCommand = new RelayCommand(_ => SetView(new TimKiemViewModel()));
            LichSuCommand = new RelayCommand(_ => SetView(new LichSuViewModel()));
            DatabaseExplorerCommand = new RelayCommand(_ => SetView(new DatabaseExplorerViewModel()));

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
                    // convert timestamp to local time for display
                    entry.Timestamp = entry.Timestamp.ToLocalTime();
                    // insert according to NewestOnTop preference
                    if (NewestOnTop)
                    {
                        if (LogEntries.Count > 200) LogEntries.RemoveAt(LogEntries.Count - 1);
                        LogEntries.Insert(0, entry);
                    }
                    else
                    {
                        if (LogEntries.Count > 200) LogEntries.RemoveAt(0);
                        LogEntries.Add(entry);
                    }
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
            // ENTRY must be by RFID only. Plate is optional and not required for validation.
            string uid = string.IsNullOrEmpty(LastScannedUID) ? string.Empty : LastScannedUID;

            if (string.IsNullOrEmpty(uid))
            {
                LanVaoTrangThai = "❌ Vui lòng quét thẻ RFID!";
                return;
            }

            try
            {
                LoggingService.Instance.LogInfo("XeVaoScan", "MainViewModel", $"Scan in UID={uid}");

                // verify card exists
                var card = db.GetRFIDCardByUid(uid);
                if (card == null || card.Id == 0)
                {
                    LanVaoTrangThai = $"❌ Thẻ {uid} chưa đăng ký!";
                    LoggingService.Instance.LogInfo("XeVao", "MainViewModel", $"Unregistered UID={uid}");
                    return;
                }

                string plate = string.IsNullOrEmpty(card.BienSo) ? string.Empty : card.BienSo;

                if (!Directory.Exists("Images"))
                    Directory.CreateDirectory("Images");

                // insert into DB (CardId-first - FIXED)
                try
                {
                    db.ThemXe(card.Id, string.IsNullOrEmpty(plate) ? null : plate, "");
                }
                catch (Exception ex)
                {
                    LanVaoTrangThai = $"❌ Lỗi ghi DB: {ex.Message}";
                    LoggingService.Instance.LogError("XeVaoInsertFailed", "MainViewModel", $"CardId={card.Id}", ex);
                    return;
                }

                // verify insert (FIXED: CardId)
                int count = db.GetXeTrongBaiCountByCardId(card.Id);

                LoggingService.Instance.LogInfo(
                    "XeVao",
                    "MainViewModel",
                    $"After insert CardId={card.Id} activeCount={count}"
                );

                if (count == 0)
                {
                    LanVaoTrangThai = "⚠ Insert DB không thành công (không tìm thấy bản ghi sau insert). Kiểm tra logs.";
                    return;
                }

                // reflect in UI list
                var xe = new Xe
                {
                    BienSo = plate ?? string.Empty,
                    ThoiGianVao = DateTime.Now
                };

                DanhSachXe.Add(xe);

                LanVaoBienSo = plate;
                LanVaoUID = uid;

                bool opened = await C3200Service.Instance.OpenBarrierAsync(1);

                LanVaoTrangThai = opened
                    ? $"✅ Xe vào lúc {DateTime.Now:HH:mm} – barrier đã mở"
                    : "⚠ Xe vào – barrier lỗi";

                LoggingService.Instance.LogInfo(
                    "XeVao",
                    "MainViewModel",
                    $"CardId={card.Id}; Plate={plate}; opened={opened}"
                );

                ThemLog("VÀO", plate, opened ? "✅ Barrier đã mở" : "⚠ Barrier lỗi");

                BienSoNhap = "";
                LastScannedUID = string.Empty;
            }
            catch (Exception ex)
            {
                LanVaoTrangThai = $"❌ Lỗi xử lý vào: {ex.Message}";
                LoggingService.Instance.LogError(
                    "XeVaoUnhandled",
                    "MainViewModel",
                    $"UID={LastScannedUID}",
                    ex
                );
            }
        }

        private async Task XeRaAsync()
        {
            // EXIT must be by RFID only.
            string cardUid = string.IsNullOrEmpty(LastScannedUID) ? string.Empty : LastScannedUID;

            if (string.IsNullOrEmpty(cardUid))
            {
                LanRaTrangThai = "❌ Vui lòng quét thẻ RFID!";
                return;
            }

            try
            {
                LoggingService.Instance.LogInfo("XeRaScan", "MainViewModel", $"Scan out UID={cardUid}");

                var card = db.GetRFIDCardByUid(cardUid);
                if (card == null || card.Id == 0)
                {
                    LanRaTrangThai = $"❌ Thẻ {cardUid} chưa đăng ký!";
                    return;
                }

                int cardId = card.Id;

                // debug count (CARD ID)
                int activeCount = db.GetXeTrongBaiCountByCardId(cardId);
                LoggingService.Instance.LogInfo("XeRaDebug", "MainViewModel",
                    $"Active XeTrongBai rows for CardId={cardId}: {activeCount}");

                var rec = db.GetXeTrongBaiRecordByCardId(cardId);
                if (rec == null)
                {
                    LanRaTrangThai = "⚠ Không tìm thấy xe trong bãi cho thẻ này";
                    LoggingService.Instance.LogInfo("XeRaNotFound", "MainViewModel",
                        $"No active XeTrongBai for CardId={cardId}. ActiveCount={activeCount}");
                    return;
                }

                var (id, plate, timeIn) = rec.Value;

                LanRaBienSo = plate;

                var thoiGian = DateTime.Now - timeIn;

                LanRaThoiGianVao =
                    $"Vào: {timeIn:HH:mm} │ {thoiGian.Hours}h{thoiGian.Minutes:D2}m";

                LanRaThoiGianTrongBai =
                    $"Thời gian trong bãi: {thoiGian.Days}d {thoiGian.Hours}h{thoiGian.Minutes:D2}m";

                int? loaiXeId = (card.LoaiXeId > 0) ? card.LoaiXeId : (int?)null;
                int? loaiVeId = (card.LoaiVeId > 0) ? card.LoaiVeId : (int?)null;

                double tien = db.TinhTien(loaiXeId, loaiVeId, timeIn, DateTime.Now);

                LanRaTien = $"💰 {tien:N0} VNĐ";
                TienHienThi = $"Tiền: {tien:N0} VNĐ";

                // DB update
                try
                {
                    db.UpdateXeRaById(id, DateTime.Now);
                    db.LuuLichSu(   
                        string.IsNullOrEmpty(plate) ? null : plate,
                        timeIn,
                        DateTime.Now,
                        tien,
                        string.Empty,
                        cardUid
                    );

                    db.XoaXeByCardId(cardId);

                    LoggingService.Instance.LogInfo(
                        "XeRa",
                        "MainViewModel",
                        $"Processed exit CardId={cardId}, Id={id}, Fee={tien}"
                    );
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError(
                        "XeRaDbFail",
                        "MainViewModel",
                        $"CardId={cardId}, Id={id}",
                        ex
                    );

                    LanRaTrangThai = $"❌ Lỗi ghi DB khi xử lý ra: {ex.Message}";
                    return;
                }

                // remove from UI list
                if (!string.IsNullOrEmpty(plate))
                {
                    var xeInList = DanhSachXe.FirstOrDefault(x => x.BienSo == plate);
                    if (xeInList != null) DanhSachXe.Remove(xeInList);
                }

                BienSoNhap = string.Empty;
                LastScannedUID = string.Empty;

                await C3200Service.Instance.OpenBarrierAsync(2);

                LanRaTrangThai = $"✅ Xe ra lúc {DateTime.Now:HH:mm} – barrier đã mở";

                LoggingService.Instance.LogInfo(
                    "XeRaComplete",
                    "MainViewModel",
                    $"CardId={cardId}, Fee={tien}"
                );

                ThemLog("RA", plate, $"💰 {tien:N0} VNĐ");
            }
            catch (Exception ex)
            {
                LanRaTrangThai = $"❌ Lỗi xử lý ra: {ex.Message}";
                LoggingService.Instance.LogError(
                    "XeRaUnhandled",
                    "MainViewModel",
                    $"UID={LastScannedUID}",
                    ex
                );
            }
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
