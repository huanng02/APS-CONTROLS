using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QuanLyGiuXe.Models
{
    public class ParkingGate
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Site ID")]
        public int SiteId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mã Cổng")]
        [Description("Mã cổng viết liền không dấu (VD: GATE_01)")]
        public string GateCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên Cổng")]
        [Description("Tên hiển thị của cổng (VD: Cổng số 1)")]
        public string GateName { get; set; } = string.Empty;

        [Description("Mô tả thêm về cổng này")]
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        // Display helpers
        [Obsolete("Display helper only")]
        public string SiteCode { get; set; } = string.Empty;

        [Obsolete("Display helper only")]
        public string SiteName { get; set; } = string.Empty;
    }
}
