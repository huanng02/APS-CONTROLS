using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using Newtonsoft.Json;

namespace QuanLyGiuXe.Services.OfflineCache
{
    public class AutoSyncService
    {
        private static readonly Lazy<AutoSyncService> _lazy = new(() => new AutoSyncService());
        public static AutoSyncService Instance => _lazy.Value;

        private CancellationTokenSource? _cts;
        private bool _isStarted = false;

        private AutoSyncService() { }

        public void Start()
        {
            if (_isStarted) return;
            _isStarted = true;
            _cts = new CancellationTokenSource();
            Task.Run(() => SyncLoopAsync(_cts.Token));
            LoggingService.Instance.LogInfo("SYNC_ENGINE", "Service", "AutoSyncService started");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _isStarted = false;
        }

        private async Task SyncLoopAsync(CancellationToken token)
        {
            int delaySeconds = 10; // Initial delay

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Only sync if Online
                    if (ConnectivityStateService.Instance.IsOnline)
                    {
                        // 1. Sync Lookups periodically (e.g., every 5 minutes or if queue empty)
                        await OfflineCacheService.Instance.SyncLookupTablesAsync();

                        // 2. Process Pending Queue
                        var pending = await OfflineQueueService.Instance.GetPendingAsync();
                        if (pending.Any())
                        {
                            LoggingService.Instance.LogInfo("SYNC_ENGINE", "Sync", $"Processing {pending.Count} pending transactions...");
                            
                            foreach (var tx in pending)
                            {
                                if (token.IsCancellationRequested) break;
                                
                                bool success = await ProcessTransactionAsync(tx);
                                if (success)
                                {
                                    await OfflineQueueService.Instance.MarkCompletedAsync(tx.Id);
                                    delaySeconds = 10; // Reset delay on success
                                }
                                else
                                {
                                    await OfflineQueueService.Instance.MarkFailedAsync(tx.Id, "Sync failed");
                                    // Exponential backoff if failed
                                    delaySeconds = Math.Min(delaySeconds * 2, 300); 
                                    break; // Stop batch on first failure
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("SYNC_ENGINE", "Loop", "Error in sync loop", ex);
                }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
            }
        }

        private async Task<bool> ProcessTransactionAsync(PendingTransaction tx)
        {
            try
            {
                var db = new DatabaseService();
                
                switch (tx.TransactionType)
                {
                    case "TEST_WRITE":
                        return true;

                    // RFID Cards
                    case "INSERT_RFID_CARD":
                    case "UPDATE_RFID_CARD":
                        var card = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        if (tx.TransactionType == "INSERT_RFID_CARD")
                            await db.InsertRFIDCardAsync((string)card.UID, (string)card.BienSo, (string)card.CardName, (int)card.LoaiVeId, (int)card.LoaiXeId, (string)card.TrangThai, (DateTime)card.NgayTao, (DateTime?)card.NgayHetHan);
                        else
                            await db.UpdateRFIDCardAsync((int)card.Id, (string)card.UID, (string)card.BienSo, (string)card.CardName, (int)card.LoaiVeId, (int)card.LoaiXeId, (string)card.TrangThai, (DateTime?)card.NgayTao, (DateTime?)card.NgayHetHan);
                        return true;

                    case "DELETE_RFID_CARD":
                        var delCard = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        await db.DeleteRFIDCardAsync((int)delCard.Id);
                        return true;

                    case "RENEW_RFID_CARD":
                        var renew = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        await db.GiaHanRFIDCardAsync((int)renew.Id, (int)renew.Months);
                        return true;

                    // User Management
                    case "CREATE_USER":
                    case "UPDATE_USER":
                        if (tx.TransactionType == "CREATE_USER")
                        {
                            var createModel = JsonConvert.DeserializeObject<UserUpsertModel>(tx.PayloadJson);
                            await new UserManagementRepository().CreateUserAsync(createModel);
                        }
                        else
                        {
                            var updateModel = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                            await new UserManagementRepository().UpdateUserAsync((int)updateModel.Id, (string)updateModel.Ten, (int)updateModel.RoleId, (string)updateModel.TrangThai);
                        }
                        return true;

                    case "DISABLE_USER":
                    case "ENABLE_USER":
                    case "DELETE_USER":
                        var userParams = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        var userRepo = new UserManagementRepository();
                        int targetId = (int)userParams.Id;
                        if (tx.TransactionType == "DISABLE_USER") await userRepo.DisableUserAsync(targetId);
                        else if (tx.TransactionType == "ENABLE_USER") await userRepo.EnableUserAsync(targetId);
                        else await userRepo.DeleteUserAsync(targetId);
                        return true;

                    // Ticket Types (LoaiVe)
                    case "INSERT_LOAI_VE":
                    case "UPDATE_LOAI_VE":
                        var lv = JsonConvert.DeserializeObject<LoaiVe>(tx.PayloadJson);
                        if (tx.TransactionType == "INSERT_LOAI_VE") await new LoaiVeRepository().InsertAsync(lv);
                        else await new LoaiVeRepository().UpdateAsync(lv);
                        return true;

                    case "DELETE_LOAI_VE":
                        var lvId = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        await new LoaiVeRepository().DeleteAsync((int)lvId.Id);
                        return true;

                    // Vehicle Types (LoaiXe)
                    case "INSERT_LOAI_XE":
                    case "UPDATE_LOAI_XE":
                        var lx = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        if (tx.TransactionType == "INSERT_LOAI_XE") await db.InsertLoaiXeAsync((string)lx.Ten, (string)lx.TrangThai);
                        else await db.UpdateLoaiXeAsync((int)lx.Id, (string)lx.Ten, (string)lx.TrangThai);
                        return true;

                    case "DELETE_LOAI_XE":
                        var lxId = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        await db.DeleteLoaiXeAsync((int)lxId.Id);
                        return true;

                    // Pricing (BangGia)
                    case "INSERT_BANG_GIA":
                    case "UPDATE_BANG_GIA":
                        var bg = JsonConvert.DeserializeObject<BangGia>(tx.PayloadJson);
                        if (tx.TransactionType == "INSERT_BANG_GIA") await new BangGiaRepository().InsertAsync(bg);
                        else await new BangGiaRepository().UpdateAsync(bg);
                        return true;

                    case "DELETE_BANG_GIA":
                        var bgId = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        await new BangGiaRepository().DeleteAsync((int)bgId.Id);
                        return true;

                    // Pricing Slots (BangGiaKhungGio)
                    case "INSERT_BANG_GIA_KHUNG_GIO":
                    case "UPDATE_BANG_GIA_KHUNG_GIO":
                        var bgkg = JsonConvert.DeserializeObject<BangGiaKhungGio>(tx.PayloadJson);
                        if (tx.TransactionType == "INSERT_BANG_GIA_KHUNG_GIO") await new BangGiaKhungGioRepository().InsertAsync(bgkg);
                        else await new BangGiaKhungGioRepository().UpdateAsync(bgkg);
                        return true;

                    case "DELETE_BANG_GIA_KHUNG_GIO":
                        var bgkgId = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        await new BangGiaKhungGioRepository().DeleteAsync((int)bgkgId.Id);
                        return true;

                    // ==========================================
                    // PARKING FLOW - Xe Vào / Xe Ra
                    // ==========================================

                    case "INSERT_XE_VAO":
                    {
                        var p = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        int cardId = (int)p.CardId;
                        string? bienSo = (string?)p.BienSo;
                        string? anhXe = (string?)p.AnhXe;
                        DateTime time = (DateTime)p.Time;

                        // Check không insert trùng
                        bool alreadyIn = await db.IsXeTrongBaiByCardIdAsync(cardId);
                        if (!alreadyIn)
                        {
                            using var sqlConn = new System.Data.SqlClient.SqlConnection(ConnectionManager.Instance.CurrentConnectionString);
                            await sqlConn.OpenAsync();
                            string insertSql = @"INSERT INTO XeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe) 
                                                VALUES (@cardId, @bienSo, @time, @anhXe)";
                            using var cmd = new System.Data.SqlClient.SqlCommand(insertSql, sqlConn);
                            cmd.Parameters.AddWithValue("@cardId", cardId);
                            cmd.Parameters.AddWithValue("@bienSo", string.IsNullOrEmpty(bienSo) ? (object)DBNull.Value : bienSo);
                            cmd.Parameters.AddWithValue("@time", time);
                            cmd.Parameters.AddWithValue("@anhXe", string.IsNullOrEmpty(anhXe) ? (object)DBNull.Value : anhXe);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        return true;
                    }

                    case "UPDATE_XE_RA":
                    {
                        var p = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        int id = (int)p.Id;
                        DateTime thoiGianRa = (DateTime)p.ThoiGianRa;
                        await db.UpdateXeRaByIdAsync(id, thoiGianRa);
                        return true;
                    }

                    case "DELETE_XE_TRONG_BAI":
                    {
                        var p = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        int cardId = (int)p.CardId;
                        await db.XoaXeByCardIdAsync(cardId);
                        return true;
                    }

                    case "DELETE_XE_BY_PLATE":
                    {
                        var p = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        string? bienSo = (string?)p.BienSo;
                        await db.XoaXeAsync(bienSo ?? string.Empty);
                        return true;
                    }

                    case "INSERT_LICH_SU":
                    {
                        var p = JsonConvert.DeserializeObject<dynamic>(tx.PayloadJson);
                        string? bienSo = (string?)p.BienSo;
                        DateTime vao = (DateTime)p.Vao;
                        DateTime ra = (DateTime)p.Ra;
                        double tien = (double)p.Tien;
                        string? anhXe = (string?)p.AnhXe;
                        string? cardUid = (string?)p.CardUid;
                        await db.LuuLichSuAsync(bienSo, vao, ra, tien, anhXe ?? string.Empty, cardUid);
                        return true;
                    }

                    default:
                        LoggingService.Instance.LogWarning("SYNC_ENGINE", "Process", $"Unknown transaction type: {tx.TransactionType}");
                        return true; 
                }
            }
            catch (Exception ex)
            {
                tx.ErrorMessage = ex.Message;
                LoggingService.Instance.LogError("SYNC_ENGINE", "Sync", $"Failed to sync {tx.TransactionType}: {ex.Message}", ex);
                return false;
            }
        }

        public async Task TriggerSyncNowAsync()
        {
            try
            {
                LoggingService.Instance.LogInfo("SYNC_ENGINE", "TriggerSyncNow", "Programmatic sync trigger activated.");
                if (!ConnectivityStateService.Instance.IsOnline) return;

                // 1. Sync lookup tables
                await OfflineCacheService.Instance.SyncLookupTablesAsync();

                // 2. Process pending queue
                var pending = await OfflineQueueService.Instance.GetPendingAsync();
                foreach (var tx in pending)
                {
                    bool success = await ProcessTransactionAsync(tx);
                    if (success)
                    {
                        await OfflineQueueService.Instance.MarkCompletedAsync(tx.Id);
                    }
                    else
                    {
                        await OfflineQueueService.Instance.MarkFailedAsync(tx.Id, tx.ErrorMessage ?? "Manual sync failed");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("SYNC_ENGINE", "TriggerSyncNow", "Failed", ex);
            }
        }
    }
}
