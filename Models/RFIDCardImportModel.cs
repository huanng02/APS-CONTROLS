using System;
using System.ComponentModel;

namespace QuanLyGiuXe.Models
{
    public enum ImportStatus
    {
        UNKNOWN,
        VALID,
        DUPLICATE_FILE,
        DUPLICATE_DB,
        INVALID_DATA,
        SKIPPED
    }

    public class RFIDCardImportModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private int _rowNumber;
        public int RowNumber { get => _rowNumber; set { _rowNumber = value; Raise(nameof(RowNumber)); } }

        public string CardUID { get; set; } = string.Empty;
        public string BienSo { get; set; } = string.Empty;
        // mapped ids after normalization & lookup
        public int? LoaiXeId { get; set; }
        public int? LoaiVeId { get; set; }
        // original text values from Excel
        public string LoaiXeTextRaw { get; set; } = string.Empty;
        public string LoaiVeTextRaw { get; set; } = string.Empty;
        public DateTime? NgayDangKy { get; set; }
        public DateTime? NgayHetHan { get; set; }
        public string TrangThai { get; set; } = string.Empty;

        // UI-friendly text fields (populated after reading, using DB lookup)
        private string _loaiXeText = string.Empty;
        public string LoaiXeText { get => _loaiXeText; set { _loaiXeText = value; Raise(nameof(LoaiXeText)); } }

        private string _loaiVeText = string.Empty;
        public string LoaiVeText { get => _loaiVeText; set { _loaiVeText = value; Raise(nameof(LoaiVeText)); } }

        private ImportStatus _status = ImportStatus.UNKNOWN;
        public ImportStatus Status { get => _status; set { _status = value; Raise(nameof(Status)); } }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Raise(nameof(StatusMessage)); } }
    }
}
