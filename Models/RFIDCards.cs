using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class RFIDCards
    {
        public int Id { get; set; }
        public string CardUID { get; set; }
        public string BienSo { get; set; }

        public int? LoaiXeId { get; set; }
        public int? LoaiVeId { get; set; }
        public DateTime? NgayDangKy { get; set; }
        public DateTime? NgayHetHan { get; set; }

        public string TrangThai { get; set; }

        public string LoaiXe { get; set; }
        public string LoaiVe { get; set; } // 👉 dùng cái này thay LoaiThe
    }
}
