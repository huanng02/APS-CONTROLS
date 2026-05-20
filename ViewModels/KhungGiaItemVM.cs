using System;
using System.ComponentModel;

namespace QuanLyGiuXe.ViewModels
{
    public class KhungGiaItemVM : INotifyPropertyChanged
    {
        public int KhungGioId { get; set; }

        private string _tenKhungGio = string.Empty;
        public string TenKhungGio { get => _tenKhungGio; set { _tenKhungGio = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TenKhungGio))); } }

        private TimeSpan _gioBatDau;
        public TimeSpan GioBatDau { get => _gioBatDau; set { _gioBatDau = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioBatDau))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); } }

        private TimeSpan _gioKetThuc;
        public TimeSpan GioKetThuc { get => _gioKetThuc; set { _gioKetThuc = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioKetThuc))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); } }

        public string DisplayTime => $"{GioBatDau:hh\\:mm\\:ss} - {GioKetThuc:hh\\:mm\\:ss}";
        // Alias used by UI for pricing grid (includes seconds)
        public string DisplayLabel => DisplayTime;

        // Night flag (overnight slot)
        public bool QuaDem => GioKetThuc < GioBatDau;

        // Helper string for display (Ban ngày / Ban đêm)
        public string LoaiKhung => QuaDem ? "Ban đêm" : "Ban ngày";

        private decimal _giaTien;
        public decimal GiaTien { get => _giaTien; set { _giaTien = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GiaTien))); } }

        // Pricing VM does not allow editing times; use KhungGio management screen to change times

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
