using System;
using System.IO;
using System.Text.Json;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class SessionService
    {
        private static readonly string SessionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "session.json");
        private static readonly object _lock = new();

        public class UserSession
        {
            public string Username { get; set; } = string.Empty;
            public string EncryptedPassword { get; set; } = string.Empty;
            public DateTime LoginTime { get; set; }
        }

        public static void SaveSession(string username, string plainPassword, DateTime loginTime)
        {
            lock (_lock)
            {
                try
                {
                    var session = new UserSession
                    {
                        Username = username,
                        EncryptedPassword = CredentialEncryptionService.Encrypt(plainPassword),
                        LoginTime = loginTime
                    };

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(session, options);
                    File.WriteAllText(SessionFilePath, json);
                    LoggingService.Instance.LogInfo("SessionService", "SaveSession", $"Saved remember login session for user: {username}");
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SessionService", "SaveSession", "Failed to save login session", ex);
                }
            }
        }

        public static UserSession? LoadSession()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(SessionFilePath))
                    {
                        string json = File.ReadAllText(SessionFilePath);
                        return JsonSerializer.Deserialize<UserSession>(json);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SessionService", "LoadSession", "Failed to load login session", ex);
                }
                return null;
            }
        }

        public static void ClearSession()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(SessionFilePath))
                    {
                        File.Delete(SessionFilePath);
                        LoggingService.Instance.LogInfo("SessionService", "ClearSession", "Deleted remember login session file.");
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SessionService", "ClearSession", "Failed to delete login session file", ex);
                }
            }
        }
    }
}
