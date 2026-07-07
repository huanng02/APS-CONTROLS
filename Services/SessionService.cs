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
        public static void SaveUserShiftStart(string username, DateTime shiftStart)
        {
            lock (_lock)
            {
                try
                {
                    var shifts = LoadAllShifts();
                    shifts[username] = shiftStart;
                    string json = JsonSerializer.Serialize(shifts, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_shifts.json"), json);
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SessionService", "SaveUserShiftStart", "Failed to save shift start time", ex);
                }
            }
        }

        public static DateTime? GetUserShiftStart(string username)
        {
            lock (_lock)
            {
                try
                {
                    var shifts = LoadAllShifts();
                    if (shifts.TryGetValue(username, out DateTime shiftStart))
                    {
                        return shiftStart;
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SessionService", "GetUserShiftStart", "Failed to get shift start time", ex);
                }
                return null;
            }
        }

        public static void ClearUserShiftStart(string username)
        {
            lock (_lock)
            {
                try
                {
                    var shifts = LoadAllShifts();
                    if (shifts.Remove(username))
                    {
                        string json = JsonSerializer.Serialize(shifts, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_shifts.json"), json);
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SessionService", "ClearUserShiftStart", "Failed to clear shift start time", ex);
                }
            }
        }

        private static System.Collections.Generic.Dictionary<string, DateTime> LoadAllShifts()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_shifts.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, DateTime>>(json) 
                        ?? new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch { }
            return new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
