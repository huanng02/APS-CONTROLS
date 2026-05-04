using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService db = new();
        private readonly AnprService _anprService = new AnprService();
        public event PropertyChangedEventHandler? PropertyChanged;
        private readonly DatabaseService _dbService = new DatabaseService();
        private readonly C3200Service _plcService = C3200Service.Instance;

        // ── Field & Properties (Đã dọn dẹp trùng lặp) ────────────────────────────────

        private ImageSource _lanVaoRoiImage;
        public ImageSource LanVaoRoiImage
        {
            get => _lanVaoRoiImage;
            set { _lanVaoRoiImage = value; OnPropertyChanged(); }
        }

        private string _lanVaoTrangThai = "Chờ xe vào...";
        public string LanVaoTrangThai
        {
            get => _lanVaoTrangThai;
            set { _lanVaoTrangThai = value; OnPropertyChanged(); }
        }

        private string _lanVaoBienSo = "";
        public string LanVaoBienSo
        {
            get => _lanVaoBienSo;
            set { _lanVaoBienSo = value; OnPropertyChanged(); }
        }

        private string _bienSoNhap = "";
        public string BienSoNhap
        {
            get => _bienSoNhap;
            set { _bienSoNhap = value; OnPropertyChanged(); }
        }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        private string _thongBao;
        public string ThongBao
        {
            get => _thongBao;
            set { _thongBao = value; OnPropertyChanged(); }
        }
        public ObservableCollection<Xe> DanhSachXe { get; set; } = new();
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        private string _tienHienThi = "";
        public string TienHienThi
        {
            get => _tienHienThi;
            set { _tienHienThi = value; OnPropertyChanged(); }
        }

        private string _tuKhoaTimKiem = "";
        public string TuKhoaTimKiem
        {
            get => _tuKhoaTimKiem;
            set { _tuKhoaTimKiem = value; OnPropertyChanged(); TimKiemXe(); }
        }

        public string LastScannedUID { get; set; } = "";

        // ── Làn Ra ───────────────────────────────────────────────────────────────

        private string _lanRaBienSo = "";
        public string LanRaBienSo
        {
            get => _lanRaBienSo;
            set { _lanRaBienSo = value; OnPropertyChanged(); }
        }

        private string _lanRaTrangThai = "Chờ xe ra...";
        public string LanRaTrangThai
        {
            get => _lanRaTrangThai;
            set { _lanRaTrangThai = value; OnPropertyChanged(); }
        }

        private string _lanRaTien = "";
        public string LanRaTien
        {
            get => _lanRaTien;
            set { _lanRaTien = value; OnPropertyChanged(); }
        }

        private string _lanVaoUID = "";
        public string LanVaoUID
        {
            get => _lanVaoUID;
            set { _lanVaoUID = value; OnPropertyChanged(); }
        }

        private string _lanRaThoiGianVao = "";
        public string LanRaThoiGianVao
        {
            get => _lanRaThoiGianVao;
            set { _lanRaThoiGianVao = value; OnPropertyChanged(); }
        }

        private string _lanRaThoiGianTrongBai = "";
        public string LanRaThoiGianTrongBai
        {
            get => _lanRaThoiGianTrongBai;
            set { _lanRaThoiGianTrongBai = value; OnPropertyChanged(); }
        }

        private string _trangThaiKetNoi = "C3200: Đang kết nối...";
        public string TrangThaiKetNoi
        {
            get => _trangThaiKetNoi;
            set { _trangThaiKetNoi = value; OnPropertyChanged(); }
        }

        public string SoXeTrongBai => $"Xe trong bãi: {DanhSachXe?.Count ?? 0}";

        private bool _newestOnTop = true;
        public bool NewestOnTop
        {
            get => _newestOnTop;
            set { _newestOnTop = value; OnPropertyChanged(); }
        }

        private bool _showLog;
        public bool ShowLog
        {
            get => _showLog;
            set { _showLog = value; OnPropertyChanged(); }
        }

        // ── Ảnh biển số & Camera ──────────────────────────────────────────────────

        public ImageSource? AnhBienSoVao { get; set; } // Giản lược để tránh trùng
        public ImageSource? AnhBienSoRaVao { get; set; }
        public ImageSource? AnhBienSoRaRa { get; set; }
        public ImageSource? AnhChupVao1 { get; set; }
        public ImageSource? AnhChupVao2 { get; set; }
        public ImageSource? AnhChupRa1 { get; set; }
        public ImageSource? AnhChupRa2 { get; set; }

        // ── Commands ──────────────────────────────────────────────────────────────

        public ICommand XeVaoCommand { get; }
        public ICommand XeRaCommand { get; }
        public ICommand XeChiTietCommand { get; }
        public ICommand TrangChuCommand { get; }
        public ICommand TimKiemCommand { get; }
        public ICommand LichSuCommand { get; }
        public ICommand DatabaseExplorerCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────────────

        private string _currentCardUID;
        public string CurrentCardUID
        {
            get => _currentCardUID;
            set
            {
                if (_currentCardUID != value)
                {
                    _currentCardUID = value;
                    OnPropertyChanged(nameof(CurrentCardUID));

                    if (!string.IsNullOrEmpty(value))
                    {
                        XeVaoCommand.Execute(null); 
                    }
                }
            }
        }

        private string _pathAnhVao;
        public string PathAnhVao
        {
            get => _pathAnhVao;
            set
            {
                _pathAnhVao = value;
                OnPropertyChanged(nameof(PathAnhVao));
            }
        }

        public MainViewModel()
        {
            var cfg = AppConfig.Load();
            _showLog = cfg.ShowLog;

            var zk = cfg.ZKTeco;
            C3200Service.Instance.Configure(zk.IpAddress, zk.TcpPort, zk.Password, zk.Timeout, zk.BarrierDuration);
            _ = C3200Service.Instance.ConnectAsync();

            DanhSachXe.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SoXeTrongBai));

            XeVaoCommand = new RelayCommand(async _ => await XeVaoAsync());
            XeRaCommand = new RelayCommand(async _ => await XeRaAsync());
            XeChiTietCommand = new RelayCommand<Xe>(XeChiTiet);

            C3200Service.Instance.OnConnectionChanged += online =>
                Application.Current?.Dispatcher?.Invoke(() =>
                    TrangThaiKetNoi = online ? "C3200: Online ●" : "C3200: Offline ○");

            SetView(new TrangChuViewModel());
            LoadXeTrongBai();

            TrangChuCommand = new RelayCommand(_ => SetView(new TrangChuViewModel()));
            TimKiemCommand = new RelayCommand(_ => SetView(new TimKiemViewModel()));
            LichSuCommand = new RelayCommand(_ => SetView(new LichSuViewModel()));
            DatabaseExplorerCommand = new RelayCommand(_ => SetView(new DatabaseExplorerViewModel()));

            try { LoggingService.Instance.LogEmitted += OnLogEmitted; } catch { }
        }

        // ── Methods ───────────────────────────────────────────────────────────────

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void SetView(object view)
        {
            CurrentView = view;
        }

        private void OnLogEmitted(LogEntry entry)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                entry.Timestamp = entry.Timestamp.ToLocalTime();
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
        public async Task ProcessAutoDetection(Bitmap bitmap)
        {
            var plate = await ApiService.SendImageAsync(bitmap);
            if (!string.IsNullOrEmpty(plate))
            {
                BienSoNhap = plate.ToUpper();
                LanVaoTrangThai = "✅ Tự động nhận diện: " + BienSoNhap;
            }
        }

        private void ThemLog(string dir, string bienSo, string status)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = "Info",
                EventType = dir,
                Source = "UI",
                Plate = bienSo,
                Details = status
            };
            OnLogEmitted(entry);
        }
        private async Task OpenBarrier()
        {
            try
            {
                bool success = await _plcService.OpenBarrierAsync(1);

                if (!success)
                {
                    ThongBao = $"Lỗi mở barrier: {_plcService.LastError}";
                }
                else
                {
                    ThongBao = "Mở barrier thành công!";
                }
            }
            catch (Exception ex)
            {
                ThongBao = "Lỗi hệ thống: " + ex.Message;
            }
        }

        private void InsertXeVao(string cardUid, string bienSo, int loaiXe, int loaiVe)
        {
            try
            {
                string pathAnhRong = "";

                _dbService.InsertXeVao(bienSo, cardUid, loaiXe, loaiVe, pathAnhRong);

                ThongBao = "Đã ghi nhận xe vào bãi (Không ảnh).";
            }
            catch (Exception ex)
            {
                ThongBao = "Lỗi Database: " + ex.Message;
            }
        }

        public async void ProcessCardSwipe(string cardUid)
        {
            var card = _dbService.GetRFIDCardByUid(cardUid);
            if (card == null)
            {
                ThongBao = "THẺ CHƯA ĐĂNG KÝ!";
                return;
            }

            string plateFromUI = this.LanVaoBienSo;

            if (card.LoaiVeId == 1) 
            {
                if (PlateService.IsMatch(plateFromUI, card.BienSo))
                {
                    InsertXeVao(cardUid, plateFromUI, card.LoaiXeId, card.LoaiVeId);
                    await OpenBarrier();
                    ThongBao = "XE THÁNG - MỜI VÀO";
                }
                else
                {
                    ThongBao = "SAI BIỂN SỐ! (ĐK: " + card.BienSo + ")";
                }
            }
            else
            {
                InsertXeVao(cardUid, plateFromUI, card.LoaiXeId, card.LoaiVeId);
                await OpenBarrier();
                ThongBao = "VÃNG LAI - MỜI VÀO";
            }
        }
        public void LoadRoiImage(string path)
        {
            if (!System.IO.File.Exists(path)) return;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad; 
            bitmap.EndInit();
            bitmap.Freeze(); 

            LanVaoRoiImage = bitmap;
        }
        private void LoadXeTrongBai()
        {
            DanhSachXe.Clear();
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
            if (xe != null) new Views.VehicleDetailWindow(xe).ShowDialog();
        }
    }
}