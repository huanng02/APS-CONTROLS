using System;

namespace QuanLyGiuXe.Models
{
    public class ParkingSession
    {
        public int Id { get; set; }
        public string? CardNumber { get; set; }
        public string? BienSoXe { get; set; }
        public int LoaiXeId { get; set; }
        public int LoaiVeId { get; set; }
        
        public DateTime ThoiGianVao { get; set; }
        public int LanVaoId { get; set; }
        public string? HinhAnhVao { get; set; }
        
        public DateTime? ThoiGianRa { get; set; }
        public int? LanRaId { get; set; }
        public string? HinhAnhRa { get; set; }
        
        public bool IsActive { get; set; }
        public bool IsPendingSync { get; set; }
        public string? SyncStatus { get; set; }
    }
}
