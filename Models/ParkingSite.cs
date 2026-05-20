using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QuanLyGiuXe.Models
{
    public class ParkingSite
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Mã Site")]
        [Description("Mã bãi xe viết liền không dấu (VD: SITE_01)")]
        public string SiteCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập Tên Site")]
        [Description("Tên hiển thị của bãi xe (VD: Bãi xe Tầng Hầm)")]
        public string SiteName { get; set; } = string.Empty;

        [Description("Mô tả thêm về bãi xe này")]
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
