using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class ImportService
    {
        private readonly DatabaseService _db = new DatabaseService();
        private readonly ImportMappingService _mapper = new ImportMappingService();

        public enum DuplicateStrategy { Skip, Update, Cancel }

        // Validate rows, map LoaiXe/LoaiVe to IDs and mark statuses/messages on input models
        // Returns list of RFIDCard ready for insert/upsert depending on strategy
        public List<RFIDCard> ValidateAndMap(List<RFIDCardImportModel> rows, DuplicateStrategy strategy, out int totalErrors)
        {
            totalErrors = 0;
            if (rows == null) return new List<RFIDCard>();

            // build lookup maps
            var loaiXeMap = _mapper.LoadLoaiXeMap();
            var loaiVeMap = _mapper.LoadLoaiVeMap();

            // existing UIDs in DB (normalized to upper invariant trimmed)
            var existingUids = new HashSet<string>(_db.GetRFIDCards().Select(x => (x.UID ?? string.Empty).Trim().ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);

            // detect duplicates in file (normalized CardUID)
            var fileGroups = rows.GroupBy(r => NormalizeCardUID(r.CardUID)).ToDictionary(g => g.Key, g => g.Count());

            var result = new List<RFIDCard>();

            foreach (var r in rows)
            {
                r.Status = ImportStatus.UNKNOWN;
                r.StatusMessage = string.Empty;

                var rowLabel = $"Dòng {r.RowNumber}";

                var uidNorm = NormalizeCardUID(r.CardUID);
                if (string.IsNullOrWhiteSpace(uidNorm))
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = "CardUID bắt buộc";
                    totalErrors++;
                    continue;
                }

                if (fileGroups.TryGetValue(uidNorm, out var cnt) && cnt > 1)
                {
                    r.Status = ImportStatus.DUPLICATE_FILE;
                    r.StatusMessage = "Duplicate CardUID in file";
                    totalErrors++;
                    if (strategy == DuplicateStrategy.Cancel)
                    {
                        // stop whole import
                        throw new InvalidOperationException($"{rowLabel}: Duplicate CardUID '{r.CardUID}' in file. Import canceled.");
                    }
                    // if skip or update, mark and continue
                }

                var existsInDb = existingUids.Contains(uidNorm);
                if (existsInDb)
                {
                    r.Status = ImportStatus.DUPLICATE_DB;
                    r.StatusMessage = "Exists in DB";
                    if (strategy == DuplicateStrategy.Cancel)
                    {
                        throw new InvalidOperationException($"{rowLabel}: CardUID '{r.CardUID}' already exists in DB. Import canceled.");
                    }
                }

                // Map LoaiXe
                var lxId = _mapper.MapLoaiXe(r.LoaiXeTextRaw, loaiXeMap);
                if (!lxId.HasValue)
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = $"LoaiXe '{r.LoaiXeTextRaw}' không tồn tại";
                    totalErrors++;
                    continue;
                }

                // Map LoaiVe
                var lvId = _mapper.MapLoaiVe(r.LoaiVeTextRaw, loaiVeMap);
                if (!lvId.HasValue)
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = $"LoaiVe '{r.LoaiVeTextRaw}' không tồn tại";
                    totalErrors++;
                    continue;
                }

                // Dates: NgayHetHan may be null; if present and NgayDangKy > NgayHetHan -> invalid
                if (r.NgayDangKy.HasValue && r.NgayHetHan.HasValue && r.NgayDangKy > r.NgayHetHan)
                {
                    r.Status = ImportStatus.INVALID_DATA;
                    r.StatusMessage = "NgayDangKy > NgayHetHan";
                    totalErrors++;
                    continue;
                }

                // If we reach here, it's valid or duplicate depending on strategy
                if (r.Status == ImportStatus.UNKNOWN || r.Status == ImportStatus.DUPLICATE_DB || r.Status == ImportStatus.DUPLICATE_FILE)
                {
                    // Decide final status depending on strategy
                    if (r.Status == ImportStatus.DUPLICATE_DB)
                    {
                        if (strategy == DuplicateStrategy.Skip)
                        {
                            r.Status = ImportStatus.SKIPPED;
                            r.StatusMessage = "Skipped: exists in DB";
                            continue; // do not include for insert
                        }
                        else if (strategy == DuplicateStrategy.Update)
                        {
                            // will upsert later
                            r.Status = ImportStatus.VALID;
                            r.StatusMessage = "Will update";
                        }
                    }
                    else if (r.Status == ImportStatus.DUPLICATE_FILE)
                    {
                        // skip duplicates in file
                        r.Status = ImportStatus.INVALID_DATA;
                        r.StatusMessage = "Duplicate in file";
                        totalErrors++;
                        continue;
                    }
                    else
                    {
                        r.Status = ImportStatus.VALID;
                        r.StatusMessage = "OK";
                    }
                }

                // Prepare RFIDCard model for insert/update
                var model = new RFIDCard
                {
                    UID = r.CardUID?.Trim(),
                    BienSo = string.IsNullOrWhiteSpace(r.BienSo) ? string.Empty : r.BienSo.Trim(),
                    LoaiXeId = lxId ?? 0,
                    LoaiVeId = lvId ?? 0,
                    NgayTao = r.NgayDangKy ?? DateTime.Now,
                    NgayHetHan = r.NgayHetHan,
                    TrangThai = string.IsNullOrWhiteSpace(r.TrangThai) ? "Active" : r.TrangThai.Trim()
                };

                result.Add(model);
            }

            return result;
        }

        private static string NormalizeCardUID(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid)) return string.Empty;
            return uid.Trim().ToUpperInvariant();
        }

        // Execute bulk insert/upsert with progress. If updateExisting true, uses BulkUpsert; otherwise BulkInsert
        public (int Inserted, int Updated) ExecuteBulk(List<RFIDCard> models, IProgress<int>? progress = null, bool updateExisting = false)
        {
            if (models == null || !models.Any()) return (0, 0);

            if (updateExisting)
            {
                var res = _db.BulkUpsertRFIDCards(models, batchSize: 1000, progress: progress, updateExisting: true);
                return res;
            }
            else
            {
                // ensure none of models duplicates primary key in DB; caller should filter duplicates
                _db.BulkInsertRFIDCards(models);
                return (models.Count, 0);
            }
        }
    }
}
