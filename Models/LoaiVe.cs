using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuanLyGiuXe.Models
{
    // LoaiVe model with INotifyPropertyChanged for WPF binding
    public class LoaiVe : INotifyPropertyChanged
    {
        private int _id;
        private string _tenLoai = string.Empty;
        // Pricing moved to BangGia table; LoaiVe keeps metadata only
        private string _trangThai = string.Empty;
        private string _detail = string.Empty;
        private bool _isSelected;

        public int Id { get => _id; set => SetField(ref _id, value); }
        public string TenLoai { get => _tenLoai; set => SetField(ref _tenLoai, value); }
        // keep as string for compatibility with existing UI code
        public string TrangThai { get => _trangThai; set => SetField(ref _trangThai, value); }

        // pricing moved to BangGia; no GiaTien property on LoaiVe model

        // New column in DB: Detail (nullable)
        public string Detail { get => _detail; set => SetField(ref _detail, value); }

        private bool _coTheGiaHan;
        public bool CoTheGiaHan { get => _coTheGiaHan; set => SetField(ref _coTheGiaHan, value); }

        // UI-only selection flag for DataGrid bulk actions
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
