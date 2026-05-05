using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    public class LichSuViewModel : INotifyPropertyChanged
    {
        DatabaseService db = new DatabaseService();

        private List<LichSuXe> TatCaLichSu = new List<LichSuXe>();

        public ObservableCollection<LichSuXe> DanhSachLichSu { get; set; }

        public LichSuViewModel()
        {
            DanhSachLichSu = new ObservableCollection<LichSuXe>();
            TatCaLichSu = db.LayLichSu().ToList();
            LoadTrang();
        }

        // ======================
        // FILTER
        // ======================

        private string _tuKhoaTimKiem;
        public string TuKhoaTimKiem
        {
            get => _tuKhoaTimKiem;
            set
            {
                _tuKhoaTimKiem = value;
                TrangHienTai = 1;
                LoadTrang();
            }
        }

        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public decimal? TienMin { get; set; }
        public decimal? TienMax { get; set; }

        // ======================
        // PAGING (FIXED)
        // ======================

        private int _pageSize = 10;
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
                    OnPropertyChanged(nameof(TongTrang));

                    LoadTrang(); // 🔥 APPLY NGAY
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
                int total = GetFilteredData().Count();
                return total == 0 ? 1 : (int)Math.Ceiling((double)total / PageSize);
            }
        }

        // ======================
        // FILTER DATA
        // ======================

        private IEnumerable<LichSuXe> GetFilteredData()
        {
            var query = TatCaLichSu.AsEnumerable();

            if (!string.IsNullOrEmpty(TuKhoaTimKiem))
                query = query.Where(x =>
                    !string.IsNullOrEmpty(x.BienSo) &&
                    x.BienSo.ToLower().Contains(TuKhoaTimKiem.ToLower()));

            if (TuNgay.HasValue)
                query = query.Where(x => x.ThoiGianVao.Date >= TuNgay.Value.Date);

            if (DenNgay.HasValue)
                query = query.Where(x => x.ThoiGianVao.Date <= DenNgay.Value.Date);

            if (TienMin.HasValue)
                query = query.Where(x => x.Tien >= (double)TienMin.Value);

            if (TienMax.HasValue)
                query = query.Where(x => x.Tien <= (double)TienMax.Value);

            return query;
        }

        // ======================
        // LOAD PAGE
        // ======================

        public void LoadTrang()
        {
            DanhSachLichSu.Clear();

            var data = GetFilteredData()
                .Skip((TrangHienTai - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            foreach (var item in data)
                DanhSachLichSu.Add(item);

            OnPropertyChanged(nameof(TongTrang));
        }

        // ======================
        // PAGING ACTIONS
        // ======================

        public void TrangTruoc()
        {
            if (TrangHienTai > 1)
            {
                TrangHienTai--;
                LoadTrang();
            }
        }

        public void TrangSau()
        {
            if (TrangHienTai < TongTrang)
            {
                TrangHienTai++;
                LoadTrang();
            }
        }

        public void TrangDau()
        {
            TrangHienTai = 1;
            LoadTrang();
        }

        public void TrangCuoi()
        {
            TrangHienTai = TongTrang;
            LoadTrang();
        }

        // ======================
        // RESET
        // ======================

        public void ResetFilter()
        {
            TuKhoaTimKiem = "";
            TuNgay = null;
            DenNgay = null;
            TienMin = null;
            TienMax = null;

            TrangDau();
        }

        // ======================
        // PROPERTY CHANGED
        // ======================

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}