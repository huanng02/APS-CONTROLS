using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class LoaiXe
    {
        public int Id { get; set; }
        public string TenLoai { get; set; }
        public string TrangThai { get; set; }
        // Non-persistent UI flag for selection in DataGrid
        public bool IsSelected { get; set; }
    }
}
