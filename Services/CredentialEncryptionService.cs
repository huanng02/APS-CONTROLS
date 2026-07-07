using System;
using System.Security.Cryptography;
using System.Text;

namespace QuanLyGiuXe.Services
{
    public static class CredentialEncryptionService
    {
        // Thêm entropy để gia tăng mức độ bảo mật
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("APS-Parking-Controls-Entropy-Key-2026");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("Encryption", "Encrypt", "Lỗi mã hóa thông tin mật khẩu", ex);
                return plainText; // Trả về dạng thô nếu có lỗi (fallback)
            }
        }

        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText)) return string.Empty;
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // Nếu lỗi giải mã (có thể text là plaintext cũ chưa được mã hóa), trả về dạng thô
                return encryptedText;
            }
        }
    }
}
