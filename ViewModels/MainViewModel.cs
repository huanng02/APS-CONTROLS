using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using System.Windows.Media;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.ViewModels
{
    internal class MainViewModel : INotifyPropertyChanged
    {

        PlateRecognitionService plateService = new PlateRecognitionService();
        public object CurrentView { get; set; }
        private List<Xe> TatCaXe = new List<Xe>();
        DatabaseService db = new DatabaseService();
        public event PropertyChangedEventHandler? PropertyChanged;
        ParkingLogicService parkingService = new ParkingLogicService();
       
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private string _bienSoNhap = "";
        public string BienSoNhap
        {
            get { return _bienSoNhap; }
            set
            {
                _bienSoNhap = value;
                OnPropertyChanged(nameof(BienSoNhap));
            }
        }
        public ObservableCollection<Xe> DanhSachXe { get; set; }
        public ICommand XeVaoCommand { get; set; }
        public ICommand XeRaCommand { get; set; }

        private string _tienHienThi = "";
        public ICommand TrangChuCommand { get; set; }
        public ICommand TimKiemCommand { get; set; }
        public ICommand LichSuCommand { get; set; }
        public string TienHienThi
        {
            get { return _tienHienThi; }
            set
            {
                _tienHienThi = value;
                OnPropertyChanged(nameof(TienHienThi));
            }
        }

        public MainViewModel()
        {

            RFIDService.Instance.OnCardScanned += (uid) =>
            {
                parkingService.OnRFIDScanned(uid);
            };
            RFIDService.Instance.Start();
           
            DanhSachXe = new ObservableCollection<Xe>();

            XeVaoCommand = new RelayCommand(XeVao);
            XeRaCommand = new RelayCommand(XeRa);

            CurrentView = new TrangChuViewModel();

            TrangChuCommand = new RelayCommand(() => {
                CurrentView = new TrangChuViewModel();
                OnPropertyChanged(nameof(CurrentView));
            });

            TimKiemCommand = new RelayCommand(() => {
                CurrentView = new TimKiemViewModel();
                OnPropertyChanged(nameof(CurrentView));
            });

            LichSuCommand = new RelayCommand(() => {
                CurrentView = new LichSuViewModel();
                OnPropertyChanged(nameof(CurrentView));
            });
           
            var table = db.LayXeTrongBai();

            foreach (DataRow row in table.Rows)
            {
                var xe = new Xe
                {
                    BienSo = row["BienSo"].ToString(),
                    ThoiGianVao = Convert.ToDateTime(row["ThoiGianVao"])
                };
                DanhSachXe.Add(xe);
                TatCaXe.Add(xe);
            }
        }

        public void XeVao()
        {
            MessageBox.Show("⚠ Biển số không trùng khớp");
            if (DanhSachXe.Any(x => x.BienSo == BienSoNhap))
            {
                TienHienThi = "Xe này đã vào bãi!";
                return;
            }
            string folder = "Images";

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var xe = new Xe
            {
                BienSo = BienSoNhap,
                ThoiGianVao = DateTime.Now
            };

            DanhSachXe.Add(xe);
            TatCaXe.Add(xe);
            db.ThemXe(BienSoNhap, "UID_TEST", "");
            TienHienThi = "✅ Xe vào thành công";
            string path = Path.Combine(folder, DateTime.Now.Ticks + ".jpg");
        }

        private void XeRa()
        {
            var xe = DanhSachXe.FirstOrDefault(x => x.BienSo == BienSoNhap);

            // Nếu không tìm thấy xe
            if (xe == null)
            {
                MessageBox.Show("Xe này không có trong bãi!");
                return;
            }

            var thoiGianGui = DateTime.Now - xe.ThoiGianVao;

            double tien = Math.Ceiling(thoiGianGui.TotalHours) * 5000;

            TienHienThi = "Tiền: " + tien.ToString("N0") + " VNĐ";

            db.LuuLichSu(xe.BienSo, xe.ThoiGianVao, DateTime.Now, tien, "");

            db.XoaXe(BienSoNhap);

            DanhSachXe.Remove(xe);
            TatCaXe.Remove(xe);

            BienSoNhap = "";
        }

        private void TimKiemXe()
        {
            DanhSachXe.Clear();

            if (string.IsNullOrWhiteSpace(TuKhoaTimKiem))
            {
                foreach (var xe in TatCaXe)
                {
                    DanhSachXe.Add(xe);
                }
                return;
            }

            var ketQua = TatCaXe
                .Where(x => x.BienSo.Contains(TuKhoaTimKiem))
                .ToList();

            foreach (var xe in ketQua)
            {
                DanhSachXe.Add(xe);
            }
        }
        private string _tuKhoaTimKiem;

        public string TuKhoaTimKiem
        {
            get { return _tuKhoaTimKiem; }
            set
            {
                _tuKhoaTimKiem = value;
                OnPropertyChanged(nameof(TuKhoaTimKiem));

                TimKiemXe();
            }
        }
    }
}
