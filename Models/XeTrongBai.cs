using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuanLyGiuXe.Models
{
    public class XeTrongBai
    {
        public int Id { get; set; }
        public int? CardId { get; set; }

        public string BienSo { get; set; }
        public DateTime? ThoiGianVao { get; set; }

        public string AnhXe { get; set; }
    }
}
