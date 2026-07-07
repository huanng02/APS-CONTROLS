using System;
using System.ComponentModel;

namespace QuanLyGiuXe.ViewModels
{
    // DTO for UI editing of time-slot pricing
    public class KhungGiaItem : INotifyPropertyChanged
    {
        public int KhungGioId { get; set; }
        private string _tenKhungGio = string.Empty;
        public string TenKhungGio { get => _tenKhungGio; set { _tenKhungGio = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TenKhungGio))); } }

        private TimeSpan _gioBatDau;
        public TimeSpan GioBatDau { get => _gioBatDau; set { _gioBatDau = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioBatDau))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuaDem))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); } }

        private TimeSpan _gioKetThuc;
        public TimeSpan GioKetThuc { get => _gioKetThuc; set { _gioKetThuc = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioKetThuc))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuaDem))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTime))); } }

        // String wrappers for binding without converters
        public string GioBatDauText
        {
            get => GioBatDau.ToString("hh\\:mm");
            set
            {
                if (TimeSpan.TryParseExact(value?.Trim() ?? string.Empty, "hh\\:mm", null, out var ts))
                {
                    GioBatDau = ts;
                }
                else if (TimeSpan.TryParse(value?.Trim() ?? string.Empty, out ts))
                {
                    GioBatDau = ts;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioBatDauText)));
            }
        }

        public string GioKetThucText
        {
            get => GioKetThuc.ToString("hh\\:mm");
            set
            {
                if (TimeSpan.TryParseExact(value?.Trim() ?? string.Empty, "hh\\:mm", null, out var ts))
                {
                    GioKetThuc = ts;
                }
                else if (TimeSpan.TryParse(value?.Trim() ?? string.Empty, out ts))
                {
                    GioKetThuc = ts;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GioKetThucText)));
            }
        }

        private decimal _giaTien;
        public decimal GiaTien { get => _giaTien; set { _giaTien = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GiaTien))); } }

        public bool QuaDem => GioKetThuc < GioBatDau;

        public string DisplayTime => $"{GioBatDau:hh\\:mm} → {GioKetThuc:hh\\:mm}";

        // Validation
        private bool _isValid = true;
        public bool IsValid { get => _isValid; set { _isValid = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid))); } }
        private string _validationMessage = string.Empty;
        public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationMessage))); } }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
