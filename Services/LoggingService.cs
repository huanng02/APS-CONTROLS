using System;
using System.IO;
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
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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

        public void LogInfo(string eventType, string source, string details = null, string userId = null, string plate = null)
        {
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
            try
            {
                Task.Run(() =>
                {
                    try
                    {
                        var db = new DatabaseService();
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception);
                    }
                    catch { }
                }, _cts.Token);
            }
            catch { }
        }

        public void LogError(string eventType, string source, string details = null, Exception ex = null, string userId = null, string plate = null)
        {
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
                    try
                    {
                        var db = new DatabaseService();
                        db.InsertAppLog(entry.Timestamp, entry.Level, entry.EventType, entry.Source, entry.UserId, entry.Plate, entry.Details, entry.Exception);
                    }
                    catch { }
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

                        var line = JsonSerializer.Serialize(entry, _jsonOptions);
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
    }
}
