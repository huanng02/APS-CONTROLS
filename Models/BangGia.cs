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
        // Renamed: giaBanNgay -> GiaBanNgay (represents daytime price 06:00-20:00)
        public decimal? GiaBanNgay { get; set; }
        public decimal? GiaQuaDem { get; set; }
        public decimal? GiaThang { get; set; }

        public string TrangThai { get; set; } = string.Empty;
        // UI helpers
        public string LoaiXe { get; set; } = string.Empty;
        public string LoaiVe { get; set; } = string.Empty;
    }
}
