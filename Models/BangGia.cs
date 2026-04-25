using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class BangGia
    {
        public int Id { get; set; }
        public int LoaiXeId { get; set; }
        public int LoaiVeId { get; set; }

        // Use decimal for money
        // New model: only GiaThang is stored on BangGia. Per-slot prices are in BangGiaKhungGio.
        public decimal? GiaThang { get; set; }

        // Number of BangGiaKhungGio entries associated with this BangGia
        public int KhungGiaCount { get; set; }

        public string TrangThai { get; set; } = string.Empty;
        // Display of per-slot pricing joined from BangGiaKhungGio + KhungGio
        public string GiaTheoKhungDisplay { get; set; } = string.Empty;
        // Collection of per-slot pricing for editing in UI (not persisted on BangGia table)
        public System.Collections.ObjectModel.ObservableCollection<BangGiaKhungGio> KhungGiaList { get; set; } = new System.Collections.ObjectModel.ObservableCollection<BangGiaKhungGio>();
        // UI helpers
        public string LoaiXe { get; set; } = string.Empty;
        public string LoaiVe { get; set; } = string.Empty;
    }
}
