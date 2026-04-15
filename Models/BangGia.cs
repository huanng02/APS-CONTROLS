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
        public int? LoaiXeId { get; set; }

        public double? GiaTheoGio { get; set; }
        public double? GiaQuaDem { get; set; }

        public string TrangThai { get; set; }
    }
}
