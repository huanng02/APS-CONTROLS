using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class LoaiVe
    {
        public int Id { get; set; }
        public string TenLoai { get; set; } = string.Empty;
        public decimal GiaTien { get; set; }
        public string TrangThai { get; set; } = string.Empty;
        // UI-only selection flag for DataGrid bulk actions
        public bool IsSelected { get; set; }
    }
}
