using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services.OfflineCache
{
    /// <summary>
    /// Chuyên trách điều phối READ/WRITE giữa SQL Server và SQLite Cache.
    /// Hỗ trợ timeout 2s và tự động fallback.
    /// </summary>
    public class ConnectivityAwareRepository
    {
        private static readonly Lazy<ConnectivityAwareRepository> _lazy = new(() => new ConnectivityAwareRepository());
        public static ConnectivityAwareRepository Instance => _lazy.Value;

        private ConnectivityAwareRepository() { }

        private string ConnectionString => ConnectionManager.Instance.CurrentConnectionString;

        /// <summary>
        /// Thực hiện truy vấn READ với timeout 2s. Nếu lỗi hoặc timeout -> Fallback SQLite.
        /// </summary>
        public async Task<T?> ExecuteReadAsync<T>(string cacheKey, Func<SqlConnection, Task<T>> sqlQuery)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                // 1. Check simulation or manual offline
                if (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline)
                    throw new Exception("SQL Server is offline");

                // 2. Try SQL Server
                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync(cts.Token);
                
                T result = await sqlQuery(conn);
                
                // 3. Success: Update SQLite Cache in background
                _ = OfflineCacheService.Instance.SaveCacheAsync(cacheKey, result!);
                
                return result;
            }
            catch (Exception ex)
            {
                string reason = ex is OperationCanceledException ? "TIMEOUT" : "ERROR";
                LoggingService.Instance.LogWarning("CACHE_FALLBACK", "Repository", $"SQL {reason} for {cacheKey}. Falling back to SQLite.");
                
                // 4. Fallback: Return from SQLite Cache
                return await OfflineCacheService.Instance.GetCacheAsync<T>(cacheKey);
            }
        }

        /// <summary>
        /// Thực hiện lệnh WRITE. Nếu lỗi hoặc timeout -> Enqueue Offline Queue.
        /// Tham số localCacheUpdater (tùy chọn): cập nhật SQLite cache ngay sau khi write offline
        /// để các lần READ sau nhận ra trạng thái mới ngay lập tức.
        /// </summary>
        public async Task<bool> ExecuteWriteAsync(string transactionType, object payload, Func<SqlConnection, Task> sqlWrite,
            Func<Task>? localCacheUpdater = null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)); // Apply 2s timeout to writes too
            try
            {
                if (ConnectivityStateService.Instance.IsSimulatingOffline || !ConnectivityStateService.Instance.IsOnline)
                    throw new Exception("SQL Server is offline");

                using var conn = new SqlConnection(ConnectionString);
                await conn.OpenAsync(cts.Token);
                
                await sqlWrite(conn);
                return true;
            }
            catch (Exception ex)
            {
                // Do not swallow DB logic errors (e.g., FK constraints, syntax errors)
                if (ex is SqlException sqlEx)
                {
                    bool isNetworkError = false;
                    string msg = sqlEx.Message.ToLower();
                    if (msg.Contains("network-related") || msg.Contains("instance-specific") || msg.Contains("transport-level"))
                    {
                        isNetworkError = true;
                    }
                    else
                    {
                        foreach (SqlError err in sqlEx.Errors)
                        {
                            // Catch common network/timeout errors
                            if (err.Number == -2 || err.Number == 2 || err.Number == 53 || 
                                err.Number == 11001 || err.Number == 26 || err.Number == 40 ||
                                err.Number == 10053 || err.Number == 10054 || err.Number == 10060 || 
                                err.Number == 10061 || err.Number == 233 || err.Number == 0)
                            {
                                isNetworkError = true;
                                break;
                            }
                        }
                    }
                    if (!isNetworkError && transactionType != "INSERT_LOG") throw; // Logic error, throw to UI
                }
                else if (ex is not OperationCanceledException && ex.Message != "Simulated offline")
                {
                    // Also catch general socket exceptions wrapped in other exceptions if needed
                    if (ex.InnerException != null && ex.InnerException.Message.ToLower().Contains("network-related"))
                    {
                        // treat as network error
                    }
                    else
                    {
                        if (transactionType != "INSERT_LOG") throw; // E.g., NullReferenceException, InvalidOperationException
                    }
                }

                string reason = ex is OperationCanceledException ? "TIMEOUT" : "ERROR";

                if (transactionType != "INSERT_LOG")
                {
                    LoggingService.Instance.LogWarning("QUEUE_ADD", "Repository", $"SQL WRITE {reason} for {transactionType}. Adding to Offline Queue.");
                    
                    // Fingerprint to prevent duplicates during sync
                    string finger = $"{transactionType}_{DateTime.UtcNow.Ticks}";
                    try
                    {
                        // Check if payload has a Fingerprint property safely
                        var prop = payload?.GetType().GetProperty("Fingerprint");
                        if (prop != null)
                        {
                            var val = prop.GetValue(payload);
                            if (val != null) finger = val.ToString()!;
                        }
                    }
                    catch { }

                    // Enqueue for background sync
                    await OfflineQueueService.Instance.EnqueueAsync(transactionType, payload, finger);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ [ConnectivityAwareRepository] SQL WRITE failed for INSERT_LOG: {ex.Message}");
                }

                // Update local cache immediately so subsequent READs see the new state
                if (localCacheUpdater != null)
                {
                    try { await localCacheUpdater(); }
                    catch (Exception cacheEx)
                    {
                        if (transactionType != "INSERT_LOG")
                        {
                            LoggingService.Instance.LogWarning("CACHE_UPDATE", "Repository", $"Failed to update local cache after offline write: {cacheEx.Message}");
                        }
                    }
                }

                return false;
            }
        }
    }
}
