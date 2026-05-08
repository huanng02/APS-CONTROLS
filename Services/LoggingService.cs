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
        private Task? _worker;
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
            _logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, "logs");
            try { Directory.CreateDirectory(_logDir); } catch { }
            _worker = Task.Run(() => ProcessQueueAsync(_cts.Token));
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
                    Details = SerializeSafe(detailsObj),
                    Exception = null,
                    Action = action,
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

        private string GetMachineName()
        {
            try { return Environment.MachineName; } catch { return string.Empty; }
        }

        private string GetDeviceName()
        {
            try { return Environment.MachineName; } catch { return string.Empty; }
        }

        private string GetLocalIpAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                return ip?.ToString() ?? string.Empty;
            }
            catch { return string.Empty; }
        }

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

                Task.Run(() =>
                {
                    if (_suppressLogging.Value) return;
                    _suppressLogging.Value = true;
                    try
                    {
                        var db = new DatabaseService();
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception,
                            username: entry.Username, action: entry.Action, entityName: entry.EntityName, entityId: entry.EntityId,
                            oldValues: entry.OldValues, newValues: entry.NewValues, ipAddress: entry.IpAddress, machineName: entry.MachineName,
                            deviceName: entry.DeviceName, sessionId: entry.SessionId, correlationId: entry.CorrelationId);
                    }
                    catch { }
                    finally { _suppressLogging.Value = false; }
                }, _cts.Token);
            }
            catch { }
        }

        public void LogInfo(string eventType, string source, string details = null, string userId = null, string plate = null)
        {
            if (_suppressLogging.Value) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Info",
                EventType = eventType,
                Source = source,
                UserId = userId ?? string.Empty,
                Plate = plate ?? string.Empty,
                Details = details ?? string.Empty,
                Exception = null
            };

            // emit to UI listeners unless suppressed
            try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }

            // enqueue for background write (best-effort)
            try { _queue.Add(entry, _cts.Token); } catch { }

            // also persist to DB (best-effort, fire-and-forget)
            // persist to DB asynchronously; do not allow logging recursion
            try
            {
                Task.Run(() =>
                {
                    if (_suppressLogging.Value) return;
                    _suppressLogging.Value = true;
                    try
                    {
                        var db = new DatabaseService();
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception,
                            username: entry.Username, action: entry.Action, entityName: entry.EntityName, entityId: entry.EntityId,
                            oldValues: entry.OldValues, newValues: entry.NewValues, ipAddress: entry.IpAddress, machineName: entry.MachineName,
                            deviceName: entry.DeviceName, sessionId: entry.SessionId, correlationId: entry.CorrelationId);
                    }
                    catch { }
                    finally { _suppressLogging.Value = false; }
                }, _cts.Token);
            }
            catch { }
        }

        public void LogWarning(string eventType, string source, string details = null, string userId = null, string plate = null)
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
                Exception = null
            };

            try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }
            try { _queue.Add(entry, _cts.Token); } catch { }

            try
            {
                Task.Run(() =>
                {
                    if (_suppressLogging.Value) return;
                    _suppressLogging.Value = true;
                    try
                    {
                        var db = new DatabaseService();
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception,
                            username: entry.Username, action: entry.Action, entityName: entry.EntityName, entityId: entry.EntityId,
                            oldValues: entry.OldValues, newValues: entry.NewValues, ipAddress: entry.IpAddress, machineName: entry.MachineName,
                            deviceName: entry.DeviceName, sessionId: entry.SessionId, correlationId: entry.CorrelationId);
                    }
                    catch { }
                    finally { _suppressLogging.Value = false; }
                }, _cts.Token);
            }
            catch { }
        }

        public void LogError(string eventType, string source, string details = null, Exception ex = null, string userId = null, string plate = null)
        {
            if (_suppressLogging.Value) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = "Error",
                EventType = eventType,
                Source = source,
                UserId = userId ?? string.Empty,
                Plate = plate ?? string.Empty,
                Details = details ?? string.Empty,
                Exception = ex?.ToString()
            };

            try { if (!_suppressUi.Contains(entry.EventType)) LogEmitted?.Invoke(entry); } catch { }
            try { _queue.Add(entry, _cts.Token); } catch { }

            // persist error to DB as well
            try
            {
                Task.Run(() =>
                {
                    if (_suppressLogging.Value) return;
                    _suppressLogging.Value = true;
                    try
                    {
                        var db = new DatabaseService();
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception,
                            username: entry.Username, action: entry.Action, entityName: entry.EntityName, entityId: entry.EntityId,
                            oldValues: entry.OldValues, newValues: entry.NewValues, ipAddress: entry.IpAddress, machineName: entry.MachineName,
                            deviceName: entry.DeviceName, sessionId: entry.SessionId, correlationId: entry.CorrelationId);
                    }
                    catch { }
                    finally { _suppressLogging.Value = false; }
                }, _cts.Token);
            }
            catch { }
        }

        public void Shutdown()
        {
            try
            {
                _queue.CompleteAdding();
                _cts.Cancel();
                _worker?.Wait(2000);
            }
            catch { }
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            StreamWriter? writer = null;
            string? currentFile = null;
            try
            {
                foreach (var entry in _queue.GetConsumingEnumerable(ct))
                {
                    try
                    {
                        var fileName = Path.Combine(_logDir, $"app-log-{entry.Timestamp:yyyy-MM-dd}.jsonl");
                        if (currentFile != fileName || writer == null)
                        {
                            lock (_streamLock)
                            {
                                writer?.Dispose();
                                writer = new StreamWriter(new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read), System.Text.Encoding.UTF8) { AutoFlush = true };
                                currentFile = fileName;
                            }
                        }

                        // sanitize sensitive fields before writing
                        var sanitized = SanitizeForLogging(entry);
                        var line = JsonSerializer.Serialize(sanitized, _jsonOptions);
                        await writer.WriteLineAsync(line).ConfigureAwait(false);
                    }
                    catch { /* swallow per-entry errors */ }
                }
            }
            catch { }
            finally
            {
                try { writer?.Dispose(); } catch { }
            }
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
