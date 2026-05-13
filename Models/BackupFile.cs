using System;
using System.IO;

namespace QuanLyGiuXe.Models
{
    public class BackupFile
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public long SizeBytes { get; set; }
        
        // Helper property để hiển thị dung lượng (MB)
        public string SizeDisplay => $"{SizeBytes / 1024.0 / 1024.0:F2} MB";

        // Helper property để hiển thị ngày tạo
        public string CreatedAtDisplay => CreatedAt.ToString("dd/MM/yyyy HH:mm:ss");
    }
}
