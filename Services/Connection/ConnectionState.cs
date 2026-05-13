namespace QuanLyGiuXe.Services.Connection
{
    public enum ConnectionState
    {
        Disconnected,   // 🔴 Mất kết nối hoàn toàn
        Reconnecting,   // 🟡 Đang thử kết nối lại
        Connected,      // 🟢 Đang hoạt động bình thường
        Failed          // ❌ Lỗi nghiêm trọng (cần kiểm tra thủ công)
    }

    public enum ResourceType
    {
        Database,
        C3200Controller,
        Camera
    }
}
