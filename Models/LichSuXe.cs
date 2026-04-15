using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class LichSuXe
    {
        public int Id { get; set; }

        public int CardId { get; set; }
        public string BienSo { get; set; }

        public DateTime ThoiGianVao { get; set; }
        public DateTime? ThoiGianRa { get; set; }

        public double? Tien { get; set; }
        public string TrangThai { get; set; }

        public string AnhVao { get; set; }
        public string AnhRa { get; set; }
    }
}
