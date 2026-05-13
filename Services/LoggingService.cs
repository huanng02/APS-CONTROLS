using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace QuanLyGiuXe.Services
{
    public sealed class LoggingService
    {
        private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());
        public static LoggingService Instance => _instance.Value;

        private readonly object _streamLock = new();
        private readonly string _logDir;
        private readonly BlockingCollection<LogEntry> _queue = new(new ConcurrentQueue<LogEntry>());
        private readonly CancellationTokenSource _cts = new();
        private Task? _workerTask;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        // internal guard to prevent recursive logging
        private static readonly AsyncLocal<bool> _suppressLogging = new();
        // session id for this application instance
        private static readonly string _sessionId = Guid.NewGuid().ToString();
        // correlation id per async context
        private static readonly AsyncLocal<string?> _currentCorrelation = new();
        // backward compatible DB insert signature detection
        private static readonly bool _dbSupportsExtendedInsert;

        // events we don't want to push to the realtime UI (still written to file)
        private readonly System.Collections.Generic.HashSet<string> _suppressUi = new(StringComparer.OrdinalIgnoreCase)
        {
            "ConfigLoaded",
            "ConfigSaved"
        };

        // Event emitted for UI listeners
        public event Action<LogEntry>? LogEmitted;

        private LoggingService()
        {
            _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);

            _queue = new BlockingCollection<LogEntry>(new ConcurrentQueue<LogEntry>(), 5000);
            _cts = new CancellationTokenSource();
            _workerTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
            
            // Cleanup old logs on startup
            _ = CleanupOldFileLogsAsync();
        }

        private async Task CleanupOldFileLogsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    int retentionDays = 14; // Default retention
                    var limitDate = DateTime.Now.AddDays(-retentionDays);
                    
                    if (!Directory.Exists(_logDir)) return;
                    
                    var subDirs = Directory.GetDirectories(_logDir);
                    foreach (var dir in subDirs)
                    {
                        var files = Directory.GetFiles(dir, "*.jsonl");
                        foreach (var file in files)
                        {
                            var fi = new FileInfo(file);
                            if (fi.LastWriteTime < limitDate)
                            {
                                try { fi.Delete(); } catch { }
                            }
                        }
                    }
                }
                catch { }
            });
        }

        // Public API: specialized audit/security/crud/vehicle/barrier logs
        public void LogAudit(string action, string entityName, string entityId = null, object? oldValues = null, object? newValues = null,
            string source = "App", string userId = null, string plate = null, string details = null, string username = null, string level = "Info")
        {
            try
            {
                if (_suppressLogging.Value) return;
                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = level ?? "Info",
                    EventType = action,
                    Source = source,
                    UserId = userId ?? string.Empty,
                    Plate = plate ?? string.Empty,
                    Details = details ?? string.Empty,
                    Exception = null,
                    Action = action,
                    EntityName = entityName ?? string.Empty,
                    EntityId = entityId ?? string.Empty,
                    OldValues = SerializeSafe(oldValues),
                    NewValues = SerializeSafe(newValues),
                    Username = username ?? GetCurrentUsername(),
                    MachineName = GetMachineName(),
                    DeviceName = GetDeviceName(),
                    SessionId = _sessionId,
                    CorrelationId = GetOrCreateCorrelationId(),
                    IpAddress = GetLocalIpAddress()
                };

                try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }
                EnqueueAndPersist(entry);
            }
            catch { }
        }

        public void LogSecurity(string eventType, string source, string details = null, string userId = null, string plate = null, Exception ex = null, string username = null)
        {
            try
            {
                if (_suppressLogging.Value) return;
                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "Warning",
                    EventType = eventType,
                    Source = source,
                    UserId = userId ?? string.Empty,
                    Plate = plate ?? string.Empty,
                    Details = details ?? string.Empty,
                    Exception = ex?.ToString(),
                    Username = username ?? GetCurrentUsername(),
                    MachineName = GetMachineName(),
                    DeviceName = GetDeviceName(),
                    SessionId = _sessionId,
                    CorrelationId = GetOrCreateCorrelationId(),
                    IpAddress = GetLocalIpAddress()
                };
                try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }
                EnqueueAndPersist(entry);
            }
            catch { }
        }

        public void LogCrud(string action, string entityName, string entityId = null, object? oldValues = null, object? newValues = null,
            string source = "App", string userId = null, string plate = null, string details = null, string username = null, string level = "Info")
        {
            LogAudit(action, entityName, entityId, oldValues, newValues, source, userId, plate, details, username, level);
        }

        public void LogVehicle(string action, string plate, int? entityId = null, object? oldValues = null, object? newValues = null, string source = "Vehicle", string details = null)
        {
            try
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "Info",
                    EventType = action,
                    Source = source,
                    UserId = string.Empty,
                    Plate = plate ?? string.Empty,
                    Details = details ?? string.Empty,
                    Exception = null,
                    Action = action,
                    EntityName = "Xe",
                    EntityId = entityId?.ToString() ?? string.Empty,
                    OldValues = SerializeSafe(oldValues),
                    NewValues = SerializeSafe(newValues),
                    Username = GetCurrentUsername(),
                    MachineName = GetMachineName(),
                    DeviceName = GetDeviceName(),
                    SessionId = _sessionId,
                    CorrelationId = GetOrCreateCorrelationId(),
                    IpAddress = GetLocalIpAddress()
                };
                try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }
                EnqueueAndPersist(entry);
            }
            catch { }
        }

        public void LogBarrier(string action, string source = "Barrier", string plate = null, object? detailsObj = null)
        {
            LogGeneric(LogSeverity.Info, LogEventType.Barrier, action, source, plate, detailsObj: detailsObj);
        }

        // Specialized Monitoring Methods
        public void LogReconnect(string resource, bool success, int attempts, long durationMs, string details = null)
        {
            LogGeneric(success ? LogSeverity.Success : LogSeverity.Error, 
                LogEventType.Reconnect, 
                success ? "RECONNECT_SUCCESS" : "RECONNECT_FAILED", 
                resource, 
                details: details,
                durationMs: durationMs,
                retryCount: attempts);
        }

        public void LogBackup(string fileName, long fileSize, bool success, string details = null)
        {
            LogGeneric(success ? LogSeverity.Success : LogSeverity.Error,
                LogEventType.Backup,
                success ? "BACKUP_SUCCESS" : "BACKUP_FAILED",
                "DatabaseBackup",
                details: details,
                fileSize: fileSize,
                additionalData: fileName);
        }

        public void LogRestore(string fileName, bool success, string details = null)
        {
            LogGeneric(success ? LogSeverity.Success : LogSeverity.Error,
                LogEventType.Restore,
                success ? "RESTORE_SUCCESS" : "RESTORE_FAILED",
                "DatabaseRestore",
                details: details,
                additionalData: fileName);
        }

        public void LogQaTest(string testName, bool success, long durationMs, string details = null, string resultJson = null)
        {
            LogGeneric(success ? LogSeverity.Success : LogSeverity.Error,
                LogEventType.QaTest,
                success ? "TEST_PASSED" : "TEST_FAILED",
                "QASystem",
                details: details,
                durationMs: durationMs,
                testName: testName,
                additionalData: resultJson);
        }

        private void LogGeneric(LogSeverity severity, LogEventType eventType, string action, string source, 
            string plate = null, string userId = null, string details = null, Exception ex = null, 
            object? detailsObj = null, long? durationMs = null, int? retryCount = null, 
            long? fileSize = null, string testName = null, bool? isRecovered = null, string additionalData = null)
        {
            try
            {
                if (_suppressLogging.Value) return;

                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = severity.ToString(),
                    EventType = eventType.ToString(),
                    Source = source ?? "App",
                    UserId = userId ?? string.Empty,
                    Plate = plate ?? string.Empty,
                    Details = details ?? SerializeSafe(detailsObj),
                    Exception = ex?.ToString(),
                    Action = action,
                    Username = GetCurrentUsername(),
                    MachineName = _machineName,
                    DeviceName = _deviceName,
                    SessionId = _sessionId,
                    CorrelationId = GetOrCreateCorrelationId(),
                    IpAddress = _localIpAddress,
                    DurationMs = durationMs,
                    RetryCount = retryCount,
                    FileSize = fileSize,
                    TestName = testName,
                    IsRecovered = isRecovered,
                    AdditionalData = additionalData
                };

                try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }
                EnqueueAndPersist(entry);
            }
            catch { }
        }

        // Allows setting correlation id for a logical operation
        public void SetCorrelationId(string id) => _currentCorrelation.Value = id;
        public void ClearCorrelationId() => _currentCorrelation.Value = null;
        private string GetOrCreateCorrelationId()
        {
            if (!string.IsNullOrEmpty(_currentCorrelation.Value)) return _currentCorrelation.Value!;
            var id = Guid.NewGuid().ToString();
            _currentCorrelation.Value = id;
            return id;
        }

        private string GetCurrentUsername()
        {
            try { return QuanLyGiuXe.Models.CurrentUser.Username ?? string.Empty; } catch { return string.Empty; }
        }

        private static readonly string _machineName = GetMachineNameInternal();
        private static readonly string _deviceName = GetMachineNameInternal();
        private static readonly string _localIpAddress = GetLocalIpAddressInternal();

        private static string GetMachineNameInternal()
        {
            try { return Environment.MachineName; } catch { return string.Empty; }
        }

        private static string GetLocalIpAddressInternal()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return ip?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private string GetMachineName() => _machineName;
        private string GetDeviceName() => _deviceName;
        private string GetLocalIpAddress() => _localIpAddress;

        private string SerializeSafe(object? obj)
        {
            try
            {
                if (obj == null) return string.Empty;
                // redact sensitive fields in anonymous/dictionary objects by serializing via JsonDocument
                var json = JsonSerializer.Serialize(obj, _jsonOptions);
                // if contains password-like keys, redact
                if (json.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 || json.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0 || json.IndexOf("bcrypt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "{\"PasswordChanged\":true}";
                }
                return json;
            }
            catch { return string.Empty; }
        }

        private void EnqueueAndPersist(LogEntry entry)
        {
            try
            {
                if (_suppressLogging.Value) return;
                _queue.Add(entry, _cts.Token);
            }
            catch { }
        }

        public void LogInfo(string eventType, string source, string details = null, string userId = null, string plate = null)
        {
            LogGeneric(LogSeverity.Info, LogEventType.System, eventType, source, plate, userId, details);
        }

        public void LogWarning(string eventType, string source, string details = null, string userId = null, string plate = null)
        {
            LogGeneric(LogSeverity.Warning, LogEventType.System, eventType, source, plate, userId, details);
        }

        public void LogError(string eventType, string source, string details = null, Exception ex = null, string userId = null, string plate = null)
        {
            LogGeneric(LogSeverity.Error, LogEventType.Exception, eventType, source, plate, userId, details, ex);
        }

        public void Shutdown()
        {
            try
            {
                _queue.CompleteAdding();
                _cts.Cancel();
                _workerTask?.Wait(2000);
            }
            catch { }
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            var writers = new System.Collections.Generic.Dictionary<string, (StreamWriter Writer, string Path)>();
            var dbBatch = new System.Collections.Generic.List<LogEntry>();
            try
            {
                foreach (var entry in _queue.GetConsumingEnumerable(ct))
                {
                    try
                    {
                        // Organize by type: app, reconnect, backup, qa
                        string subDir = entry.EventType.ToLower() switch
                        {
                            var t when t.Contains("reconnect") => "reconnect",
                            var t when t.Contains("backup") || t.Contains("restore") => "backup",
                            var t when t.Contains("test") || t.Contains("qa") => "qa",
                            _ => "app"
                        };

                        var dirPath = Path.Combine(_logDir, subDir);
                        if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                        var fileName = Path.Combine(dirPath, $"{subDir}-{entry.Timestamp:yyyy-MM-dd}.jsonl");
                        
                        if (!writers.TryGetValue(subDir, out var writerInfo) || writerInfo.Path != fileName)
                        {
                            writerInfo.Writer?.Dispose();
                            var sw = new StreamWriter(new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read), System.Text.Encoding.UTF8) { AutoFlush = true };
                            writers[subDir] = (sw, fileName);
                            writerInfo = (sw, fileName);
                        }

                        // sanitize sensitive fields before writing
                        var sanitized = SanitizeForLogging(entry);
                        var line = JsonSerializer.Serialize(sanitized, _jsonOptions);
                        await writerInfo.Writer.WriteLineAsync(line).ConfigureAwait(false);
                    }
                    catch { /* swallow per-entry errors */ }

                    dbBatch.Add(entry);
                    if (dbBatch.Count >= 20 || _queue.Count == 0)
                    {
                        PersistBatchToDb(dbBatch);
                        dbBatch.Clear();
                    }
                }
            }
            catch { }
            finally
            {
                foreach (var w in writers.Values) try { w.Writer.Dispose(); } catch { }
                if (dbBatch.Count > 0) { PersistBatchToDb(dbBatch); }
            }
        }

        private void PersistBatchToDb(System.Collections.Generic.List<LogEntry> batch)
        {
            if (batch.Count == 0 || _suppressLogging.Value) return;
            _suppressLogging.Value = true;
            try
            {
                var db = new DatabaseService();
                foreach (var entry in batch)
                {
                    try
                    {
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception,
                            username: entry.Username, action: entry.Action, entityName: entry.EntityName, entityId: entry.EntityId,
                            oldValues: entry.OldValues, newValues: entry.NewValues, ipAddress: entry.IpAddress, machineName: entry.MachineName,
                            deviceName: entry.DeviceName, sessionId: entry.SessionId, correlationId: entry.CorrelationId,
                            durationMs: entry.DurationMs, retryCount: entry.RetryCount, fileSize: entry.FileSize, 
                            testName: entry.TestName, isRecovered: entry.IsRecovered, additionalData: entry.AdditionalData);
                    }
                    catch { }
                }
            }
            catch { }
            finally { _suppressLogging.Value = false; }
        }

        private object SanitizeForLogging(LogEntry entry)
        {
            try
            {
                // create a shallow copy and redact sensitive properties
                var copy = new
                {
                    entry.Timestamp,
                    entry.Level,
                    entry.EventType,
                    entry.Source,
                    entry.UserId,
                    entry.Plate,
                    Details = RedactSensitive(entry.Details),
                    entry.Exception,
                    entry.Username,
                    entry.Action,
                    entry.EntityName,
                    entry.EntityId,
                    OldValues = RedactJson(entry.OldValues),
                    NewValues = RedactJson(entry.NewValues),
                    entry.IpAddress,
                    entry.MachineName,
                    entry.DeviceName,
                    entry.SessionId,
                    entry.CorrelationId
                };
                return copy;
            }
            catch { return entry; }
        }

        private string RedactSensitive(string? input)
        {
            if (string.IsNullOrEmpty(input)) return input ?? string.Empty;
            // basic redaction: remove common password/token keys
            try
            {
                var lowered = input.ToLowerInvariant();
                if (lowered.Contains("password") || lowered.Contains("passwordhash") || lowered.Contains("token") || lowered.Contains("bcrypt"))
                {
                    return "[REDACTED]";
                }
                return input;
            }
            catch { return "[REDACTED]"; }
        }

        private string RedactJson(string? json)
        {
            if (string.IsNullOrEmpty(json)) return json ?? string.Empty;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var obj = new System.Collections.Generic.Dictionary<string, object?>();
                foreach (var prop in root.EnumerateObject())
                {
                    var name = prop.Name;
                    if (string.Equals(name, "password", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "passwordhash", StringComparison.OrdinalIgnoreCase) ||
                        name.ToLowerInvariant().Contains("token") ||
                        prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // redact sensitive or nested objects
                        obj[name] = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object ? "[REDACTED_OBJECT]" : "[REDACTED]";
                    }
                    else
                    {
                        obj[name] = prop.Value.ValueKind switch
                        {
                            System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                            System.Text.Json.JsonValueKind.Number => prop.Value.GetRawText(),
                            System.Text.Json.JsonValueKind.True => true,
                            System.Text.Json.JsonValueKind.False => false,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
                return JsonSerializer.Serialize(obj, _jsonOptions);
            }
            catch { return "[REDACTED]"; }
        }
    }
}
