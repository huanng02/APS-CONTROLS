using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public partial class DatabaseService
    {
        // Bulk insert RFIDCard list using SqlBulkCopy
        public void BulkInsertRFIDCards(IEnumerable<RFIDCard> cards)
        {
            var dt = new DataTable();
            dt.Columns.Add("CardUID", typeof(string));
            dt.Columns.Add("BienSo", typeof(string));
            dt.Columns.Add("LoaiVeId", typeof(int));
            dt.Columns.Add("LoaiXeId", typeof(int));
            dt.Columns.Add("NgayDangKy", typeof(DateTime));
            dt.Columns.Add("NgayHetHan", typeof(DateTime));
            dt.Columns.Add("TrangThai", typeof(string));

            foreach (var c in cards)
            {
                var row = dt.NewRow();
                row["CardUID"] = c.UID ?? string.Empty;
                row["BienSo"] = c.BienSo ?? string.Empty;
                row["LoaiVeId"] = c.LoaiVeId;
                row["LoaiXeId"] = c.LoaiXeId;
                row["NgayDangKy"] = c.NgayTao == DateTime.MinValue ? (object)DBNull.Value : c.NgayTao;
                row["NgayHetHan"] = c.NgayHetHan ?? (object)DBNull.Value;
                row["TrangThai"] = c.TrangThai ?? string.Empty;
                dt.Rows.Add(row);
            }

            string conn = GetWorkingConnection();
            using (var sqlConn = new SqlConnection(conn))
            {
                sqlConn.Open();
                using (var bulk = new SqlBulkCopy(sqlConn))
                {
                    bulk.DestinationTableName = "RFIDCards";
                    bulk.ColumnMappings.Add("CardUID", "CardUID");
                    bulk.ColumnMappings.Add("BienSo", "BienSo");
                    bulk.ColumnMappings.Add("LoaiVeId", "LoaiVeId");
                    bulk.ColumnMappings.Add("LoaiXeId", "LoaiXeId");
                    bulk.ColumnMappings.Add("NgayDangKy", "NgayDangKy");
                    bulk.ColumnMappings.Add("NgayHetHan", "NgayHetHan");
                    bulk.ColumnMappings.Add("TrangThai", "TrangThai");

                    bulk.WriteToServer(dt);
                }
            }
        }

        /// <summary>
        /// Bulk upsert (insert or update) RFIDCards using a temp table + MERGE. Returns (inserted, updated).
        /// </summary>
        public (int Inserted, int Updated) BulkUpsertRFIDCards(IEnumerable<RFIDCard> cards, int batchSize = 1000, IProgress<int>? progress = null, bool updateExisting = true)
        {
            if (cards == null) return (0, 0);

            var all = cards.ToList();
            int total = all.Count;
            int processed = 0;
            int totalInserted = 0;
            int totalUpdated = 0;

            string conn = GetWorkingConnection();
            using (var sqlConn = new SqlConnection(conn))
            {
                sqlConn.Open();

                using (var tran = sqlConn.BeginTransaction())
                {
                    try
                    {
                        // create temp table
                        using (var cmdCreate = new SqlCommand(@"
IF OBJECT_ID('tempdb..#TmpRFID') IS NOT NULL DROP TABLE #TmpRFID;
CREATE TABLE #TmpRFID (
    CardUID NVARCHAR(200) NOT NULL,
    BienSo NVARCHAR(200) NULL,
    LoaiXeId INT NULL,
    LoaiVeId INT NULL,
    NgayDangKy DATETIME NULL,
    NgayHetHan DATETIME NULL,
    TrangThai NVARCHAR(200) NULL
);", sqlConn, tran))
                        {
                            cmdCreate.ExecuteNonQuery();
                        }

                        int index = 0;
                        while (index < total)
                        {
                            var batch = all.Skip(index).Take(batchSize).ToList();

                            // Load DataTable
                            var dt = new DataTable();
                            dt.Columns.Add("CardUID", typeof(string));
                            dt.Columns.Add("BienSo", typeof(string));
                            dt.Columns.Add("LoaiXeId", typeof(int));
                            dt.Columns.Add("LoaiVeId", typeof(int));
                            dt.Columns.Add("NgayDangKy", typeof(DateTime));
                            dt.Columns.Add("NgayHetHan", typeof(DateTime));
                            dt.Columns.Add("TrangThai", typeof(string));

                            foreach (var c in batch)
                            {
                                var row = dt.NewRow();
                                row["CardUID"] = c.UID ?? string.Empty;
                                row["BienSo"] = c.BienSo ?? string.Empty;
                                row["LoaiXeId"] = c.LoaiXeId;
                                row["LoaiVeId"] = c.LoaiVeId;
                                row["NgayDangKy"] = c.NgayTao == DateTime.MinValue ? (object)DBNull.Value : c.NgayTao;
                                row["NgayHetHan"] = c.NgayHetHan ?? (object)DBNull.Value;
                                row["TrangThai"] = c.TrangThai ?? string.Empty;
                                dt.Rows.Add(row);
                            }

                            // bulk copy into temp table
                            using (var bulk = new SqlBulkCopy(sqlConn, SqlBulkCopyOptions.Default, tran))
                            {
                                bulk.DestinationTableName = "#TmpRFID";
                                bulk.ColumnMappings.Add("CardUID", "CardUID");
                                bulk.ColumnMappings.Add("BienSo", "BienSo");
                                bulk.ColumnMappings.Add("LoaiXeId", "LoaiXeId");
                                bulk.ColumnMappings.Add("LoaiVeId", "LoaiVeId");
                                bulk.ColumnMappings.Add("NgayDangKy", "NgayDangKy");
                                bulk.ColumnMappings.Add("NgayHetHan", "NgayHetHan");
                                bulk.ColumnMappings.Add("TrangThai", "TrangThai");

                                bulk.WriteToServer(dt);
                            }

                            // Diagnostic: log counts in temp table before MERGE
                            try
                            {
                                using (var cmdCount = new SqlCommand("SELECT COUNT(1) FROM #TmpRFID", sqlConn, tran))
                                {
                                    var cnt = Convert.ToInt32(cmdCount.ExecuteScalar());
                                    try { LoggingService.Instance.LogInfo("BulkUpsert", "DatabaseService", $"#TmpRFID rows={cnt}"); } catch { }
                                }

                                using (var cmdNulls = new SqlCommand("SELECT COUNT(1) FROM #TmpRFID WHERE LoaiXeId IS NULL OR LoaiVeId IS NULL", sqlConn, tran))
                                {
                                    var ncnt = Convert.ToInt32(cmdNulls.ExecuteScalar());
                                    try { LoggingService.Instance.LogInfo("BulkUpsert", "DatabaseService", $"#TmpRFID rows with NULL LoaiXeId/LoaiVeId={ncnt}"); } catch { }
                                }
                            }
                            catch { }

                            // MERGE
                            using (var cmd = new SqlCommand(@"
DECLARE @Output TABLE ([Action] NVARCHAR(20));
MERGE INTO dbo.RFIDCards AS target
USING #TmpRFID AS src
ON target.CardUID = src.CardUID
WHEN MATCHED AND @updateExisting = 1 THEN
    UPDATE SET BienSo = src.BienSo, LoaiXeId = src.LoaiXeId, LoaiVeId = src.LoaiVeId, NgayDangKy = src.NgayDangKy, NgayHetHan = src.NgayHetHan, TrangThai = src.TrangThai
WHEN NOT MATCHED BY TARGET AND src.LoaiXeId IS NOT NULL AND src.LoaiXeId <> 0 AND src.LoaiVeId IS NOT NULL AND src.LoaiVeId <> 0 THEN
    INSERT (CardUID, BienSo, LoaiXeId, LoaiVeId, NgayDangKy, NgayHetHan, TrangThai)
    VALUES (src.CardUID, src.BienSo, src.LoaiXeId, src.LoaiVeId, src.NgayDangKy, src.NgayHetHan, src.TrangThai)
OUTPUT $action INTO @Output;

SELECT SUM(CASE WHEN [Action] = 'INSERT' THEN 1 ELSE 0 END) AS Inserted, SUM(CASE WHEN [Action] = 'UPDATE' THEN 1 ELSE 0 END) AS Updated FROM @Output;
", sqlConn, tran))
                            {
                                cmd.Parameters.AddWithValue("@updateExisting", updateExisting ? 1 : 0);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    if (reader.Read())
                                    {
                                        int inserted = reader["Inserted"] != DBNull.Value ? Convert.ToInt32(reader["Inserted"]) : 0;
                                        int updated = reader["Updated"] != DBNull.Value ? Convert.ToInt32(reader["Updated"]) : 0;
                                        totalInserted += inserted;
                                        totalUpdated += updated;
                                    }
                                }
                            }

                            // clean temp table for next batch
                            using (var cmdClear = new SqlCommand("TRUNCATE TABLE #TmpRFID", sqlConn, tran))
                            {
                                cmdClear.ExecuteNonQuery();
                            }

                            index += batch.Count;
                            processed += batch.Count;
                            progress?.Report((int)((processed / (double)total) * 100));
                        }

                        tran.Commit();
                    }
                    catch
                    {
                        try { tran.Rollback(); } catch { }
                        throw;
                    }
                }
            }

            return (totalInserted, totalUpdated);
        }
    }
}
