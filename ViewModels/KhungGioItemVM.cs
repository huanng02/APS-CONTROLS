using System;
using System.ComponentModel;

namespace QuanLyGiuXe.ViewModels
{
    public class KhungGioItemVM : INotifyPropertyChanged
    {
        public int Id { get; set; }

        private string _ten;
        public string TenKhungGio { get => _ten; set { _ten = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TenKhungGio))); } }

        private TimeSpan _gb;
        public TimeSpan GioBatDau { get => _gb; set { _gb = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioBatDau))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuaDem))); } }

        private TimeSpan _gk;
        public TimeSpan GioKetThuc { get => _gk; set { _gk = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioKetThuc))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuaDem))); } }

        public string DisplayTime => $"{GioBatDau:hh\\:mm\\:ss} - {GioKetThuc:hh\\:mm\\:ss}";

        public bool QuaDem => GioKetThuc < GioBatDau;

        private bool _trangThai;
        public bool TrangThai { get => _trangThai; set { _trangThai = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrangThai))); } }

        // String wrappers for editing in XAML (HH:mm:ss)
        public string GioBatDauText
        {
            get => GioBatDau.ToString("hh\\:mm\\:ss");
            set
            {
                var s = value?.Trim() ?? string.Empty;
                if (TimeSpan.TryParseExact(s, "hh\\:mm\\:ss", null, out var ts)) GioBatDau = ts;
                else if (TimeSpan.TryParseExact(s, "hh\\:mm", null, out ts)) GioBatDau = ts;
                else if (TimeSpan.TryParse(s, out ts)) GioBatDau = ts;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioBatDauText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuaDem)));
            }
        }

        public string GioKetThucText
        {
            get => GioKetThuc.ToString("hh\\:mm\\:ss");
            set
            {
                var s = value?.Trim() ?? string.Empty;
                if (TimeSpan.TryParseExact(s, "hh\\:mm\\:ss", null, out var ts)) GioKetThuc = ts;
                else if (TimeSpan.TryParseExact(s, "hh\\:mm", null, out ts)) GioKetThuc = ts;
                else if (TimeSpan.TryParse(s, out ts)) GioKetThuc = ts;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioKetThucText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuaDem)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}