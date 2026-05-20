using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QuanLyGiuXe.Models
{
    public class LaneConfig
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mã Làn")]
        [Description("Mã làn xe viết liền không dấu (VD: L1_IN)")]
        public string LaneCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên Làn")]
        [Description("Tên hiển thị của làn (VD: Làn vào 1)")]
        public string LaneName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Chiều (Direction)")]
        [Description("Chiều của làn: IN hoặc OUT")]
        public string Direction { get; set; } = "IN"; // IN, OUT

        [Description("ID của Zone chứa làn này (có thể để trống)")]
        public int? ZoneId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helper
        public string ZoneName { get; set; } = string.Empty;
    }
}
