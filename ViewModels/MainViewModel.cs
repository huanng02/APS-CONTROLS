using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private readonly DatabaseService db = new();
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
            var zk = AppConfig.Load().ZKTeco;
            C3200Service.Instance.Configure(
                ip: zk.IpAddress, port: zk.TcpPort,
                password: zk.Password, timeoutMs: zk.Timeout,
                barrierDuration: zk.BarrierDuration);
            _ = C3200Service.Instance.ConnectAsync();

            DanhSachXe = new ObservableCollection<Xe>();
            XeVaoCommand = new RelayCommand(async () => await XeVaoAsync());
            XeRaCommand = new RelayCommand(async () => await XeRaAsync());
            XeChiTietCommand = new RelayCommand<Xe>(XeChiTiet);

            CurrentView = new TrangChuViewModel();
            TrangChuCommand = new RelayCommand(() => SetView(new TrangChuViewModel()));
            TimKiemCommand = new RelayCommand(() => SetView(new TimKiemViewModel()));
            LichSuCommand = new RelayCommand(() => SetView(new LichSuViewModel()));

            LoadXeTrongBai();
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
            { TienHienThi = "❌ Vui lòng nhập biển số!"; return; }

            var regex = new System.Text.RegularExpressions.Regex(@"^\d{2}([A-Z]\d{5,6}|[A-Z]{1,2}\d{4,5})$");
            if (!regex.IsMatch(bienSo))
            { TienHienThi = "❌ Biển số không đúng định dạng!"; return; }

            if (DanhSachXe.Any(x => x.BienSo == bienSo))
            { TienHienThi = "Xe này đã vào bãi!"; return; }

            if (!Directory.Exists("Images"))
                Directory.CreateDirectory("Images");

            var xe = new Xe { BienSo = bienSo, ThoiGianVao = DateTime.Now };
            DanhSachXe.Add(xe);

            string uid = string.IsNullOrEmpty(LastScannedUID) ? "MANUAL" : LastScannedUID;
            db.ThemXe(bienSo, uid, "");
            LastScannedUID = "";

            bool opened = await C3200Service.Instance.OpenBarrierAsync(1);
            TienHienThi = opened ? "✅ Xe vào – barrier đã mở" : "⚠ Xe vào – barrier lỗi";
        }

        private async Task XeRaAsync()
        {
            var xe = DanhSachXe.FirstOrDefault(x => x.BienSo == BienSoNhap);
            if (xe == null)
            { MessageBox.Show("Xe này không có trong bãi!"); return; }

            double tien = Math.Ceiling((DateTime.Now - xe.ThoiGianVao).TotalHours) * 5000;
            TienHienThi = $"Tiền: {tien:N0} VNĐ";

            db.LuuLichSu(xe.BienSo, xe.ThoiGianVao, DateTime.Now, tien, "");
            db.XoaXe(BienSoNhap);
            DanhSachXe.Remove(xe);
            BienSoNhap = "";

            await C3200Service.Instance.OpenBarrierAsync(2);
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
