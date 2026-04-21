using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class ImportExportService
    {
        private readonly DatabaseService _db = new DatabaseService();

        // Normalize: Trim, ToUpper, collapse spaces, remove diacritics
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.Trim();
            // collapse multiple spaces
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            s = RemoveDiacritics(s);
            s = s.ToUpperInvariant();
            return s;
        }

        // Remove diacritics
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        // Levenshtein
        public static int LevenshteinDistance(string a, string b)
        {
            if (a == null) a = string.Empty;
            if (b == null) b = string.Empty;
            a = a;
            b = b;
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }
            return d[a.Length, b.Length];
        }

        // Build mapping dictionaries from DB
        private Dictionary<string, int> BuildLoaiXeDict()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var list = _db.GetLoaiXe();
            foreach (var it in list)
            {
                var k = Normalize(it.TenLoai);
                if (!dict.ContainsKey(k)) dict[k] = it.Id;
            }
            return dict;
        }
        private Dictionary<string, int> BuildLoaiVeDict()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var list = _db.GetLoaiVe();
            foreach (var it in list)
            {
                var k = Normalize(it.TenLoai);
                if (!dict.ContainsKey(k)) dict[k] = it.Id;
            }
            return dict;
        }

        // Preview excel rows and return list of ImportPreviewRow
        // activeLoaiVeId: when provided (>0) means the import UI was opened from a specific LoaiVe tab
        // - if activeLoaiVeId > 0: all preview rows will be assigned this LoaiVe and any file LoaiVe that differs will be treated as Error
        // - if activeLoaiVeId == 0 (All tab): file must contain LoaiVe; rows without LoaiVe will be marked Error
        public List<ImportPreviewRow> PreviewFromExcel(string path, int? activeLoaiVeId = null)
        {
            var result = new List<ImportPreviewRow>();
            if (!File.Exists(path)) return result;

            var dictXe = BuildLoaiXeDict();
            var dictVe = BuildLoaiVeDict();

            using (var wb = new XLWorkbook(path))
            {
                var ws = wb.Worksheets.First();
                var firstRow = ws.FirstRowUsed().RowNumber();
                var lastRow = ws.LastRowUsed().RowNumber();
                var headerRow = ws.Row(firstRow);

                // find column indexes by header names
                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= ws.LastColumnUsed().ColumnNumber(); c++)
                {
                    var cell = headerRow.Cell(c).GetString();
                    if (string.IsNullOrWhiteSpace(cell)) continue;
                    headers[Normalize(cell)] = c;
                }

                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    var ipr = new ImportPreviewRow { RowNumber = r };
                    try
                    {
                        string card = GetCellString(row, headers, "CARDUID");
                        string bien = GetCellString(row, headers, "BIENSO");
                        string loaixe = GetCellString(row, headers, "LOAIXE");
                        string loaive = GetCellString(row, headers, "LOAIVE");
                        string ngaydk = GetCellString(row, headers, "NGAYDANGKY");
                        string ngayhh = GetCellString(row, headers, "NGAYHETHAN");
                        string tt = GetCellString(row, headers, "TRANGTHAI");

                        ipr.CardUID = string.IsNullOrWhiteSpace(card) ? string.Empty : card.Trim();
                        ipr.BienSo = string.IsNullOrWhiteSpace(bien) ? string.Empty : bien.Trim();
                        ipr.LoaiXe = loaixe ?? string.Empty;
                        ipr.LoaiVe = loaive ?? string.Empty;

                        if (DateTime.TryParse(ngaydk, out var ndk)) ipr.NgayDangKy = ndk;
                        else ipr.NgayDangKy = null;
                        if (DateTime.TryParse(ngayhh, out var nhh)) ipr.NgayHetHan = nhh;
                        else ipr.NgayHetHan = null;
                        ipr.TrangThai = string.IsNullOrWhiteSpace(tt) ? "Active" : tt.Trim();

                        // validations
                        if (string.IsNullOrWhiteSpace(ipr.CardUID))
                        {
                            ipr.Status = "Error"; ipr.Message = "CardUID is required";
                            result.Add(ipr); continue;
                        }

                        // Only import Active rows
                        if (!string.Equals(ipr.TrangThai?.Trim(), "Active", StringComparison.OrdinalIgnoreCase))
                        {
                            ipr.Status = "Skipped";
                            ipr.Message = "Not Active";
                            result.Add(ipr); continue;
                        }

                        // map LoaiXe
                        var nx = ImportNormalization.Normalize(ipr.LoaiXe);
                        if (dictXe.TryGetValue(nx, out var lxid))
                        {
                            ipr.MappedLoaiXeId = lxid;
                        }
                        else
                        {
                            // fuzzy
                            int best = int.MaxValue; string bestKey = null; int bestId = 0;
                            foreach (var kv in dictXe)
                            {
                                int d = LevenshteinDistance(nx, kv.Key);
                                if (d < best) { best = d; bestKey = kv.Key; bestId = kv.Value; }
                            }
                            if (best <= 2 && bestKey != null)
                            {
                                ipr.MappedLoaiXeId = bestId;
                                ipr.Status = "AutoFix"; ipr.Message = $"LoaiXe auto-mapped to '{bestKey}' (dist={best})";
                            }
                            else
                            {
                                ipr.Status = "Error"; ipr.Message = "LoaiXe not recognized";
                                result.Add(ipr); continue;
                            }
                        }

                        // map LoaiVe (file-provided)
                        var nv = ImportNormalization.Normalize(ipr.LoaiVe);
                        if (!string.IsNullOrWhiteSpace(nv) && dictVe.TryGetValue(nv, out var lvid))
                        {
                            ipr.MappedLoaiVeId = lvid;
                        }
                        else if (!string.IsNullOrWhiteSpace(nv))
                        {
                            int best = int.MaxValue; string bestKey = null; int bestId = 0;
                            foreach (var kv in dictVe)
                            {
                                int d = LevenshteinDistance(nv, kv.Key);
                                if (d < best) { best = d; bestKey = kv.Key; bestId = kv.Value; }
                            }
                            if (best <= 2 && bestKey != null)
                            {
                                ipr.MappedLoaiVeId = bestId;
                                ipr.Status = string.IsNullOrEmpty(ipr.Status) ? "AutoFix" : ipr.Status + ";AutoFix";
                                ipr.Message = (string.IsNullOrEmpty(ipr.Message) ? "" : ipr.Message + "; ") + $"LoaiVe auto-mapped to '{bestKey}' (dist={best})";
                            }
                            else
                            {
                                // keep as unrecognized for now; will be handled below depending on activeLoaiVeId
                                ipr.MappedLoaiVeId = null;
                            }
                        }

                        // If LoaiVe mapped and NgayDangKy/NgayHetHan missing, provide sensible defaults:
                        // - if NgayDangKy missing -> set to today
                        // - if LoaiVe indicates monthly (contains "THANG") and NgayHetHan missing -> set NgayHetHan = NgayDangKy + 1 month
                        try
                        {
                            if (ipr.MappedLoaiVeId.HasValue)
                            {
                                if (!ipr.NgayDangKy.HasValue)
                                    ipr.NgayDangKy = DateTime.Today;

                                var loaiVeCheck = ImportNormalization.Normalize(ipr.LoaiVe ?? string.Empty);
                                if (string.IsNullOrWhiteSpace(ipr.NgayHetHan?.ToString()) && ipr.NgayHetHan == null)
                                {
                                    if (!string.IsNullOrEmpty(loaiVeCheck) && (loaiVeCheck.Contains("THANG") || loaiVeCheck.Contains("VE THANG") || loaiVeCheck.Contains("MONTH")))
                                    {
                                        ipr.NgayHetHan = ipr.NgayDangKy?.AddMonths(1);
                                    }
                                }
                            }
                        }
                        catch { }

                        // If import window was opened from a specific LoaiVe tab, enforce business rule:
                        if (activeLoaiVeId.HasValue && activeLoaiVeId.Value > 0)
                        {
                            // If file provided a LoaiVe different from the active tab -> mark Error
                            if (ipr.MappedLoaiVeId.HasValue && ipr.MappedLoaiVeId.Value != activeLoaiVeId.Value)
                            {
                                ipr.Status = "Error";
                                ipr.Message = "File LoaiVe does not match selected tab" + (string.IsNullOrEmpty(ipr.Message) ? "" : "; " + ipr.Message);
                                result.Add(ipr); continue;
                            }

                            // assign active tab LoaiVe to all rows
                            ipr.MappedLoaiVeId = activeLoaiVeId.Value;
                            // if previously unrecognized, mark AutoFix/Note
                            if (string.IsNullOrEmpty(ipr.Message)) ipr.Message = $"Assigned LoaiVe from selected tab (Id={activeLoaiVeId.Value})";
                            else ipr.Message = $"Assigned LoaiVe from selected tab (Id={activeLoaiVeId.Value}); " + ipr.Message;
                        }
                        else
                        {
                            // active tab is ALL: require file to specify a recognizable LoaiVe
                            if (!ipr.MappedLoaiVeId.HasValue)
                            {
                                ipr.Status = "Error";
                                ipr.Message = "Missing or unrecognized LoaiVe and importing from 'All' tab requires LoaiVe in file" + (string.IsNullOrEmpty(ipr.Message) ? "" : "; " + ipr.Message);
                                result.Add(ipr); continue;
                            }
                        }

                        // duplicate in DB
                        var exists = _db.IsRFIDUidExists(ipr.CardUID);
                        if (exists)
                        {
                            ipr.Status = string.IsNullOrEmpty(ipr.Status) ? "Exists" : ipr.Status + ";Exists";
                            ipr.Message = (string.IsNullOrEmpty(ipr.Message) ? "" : ipr.Message + "; ") + "CardUID exists in DB";
                        }

                        if (string.IsNullOrEmpty(ipr.Status)) { ipr.Status = "OK"; ipr.Message = ""; }
                        result.Add(ipr);
                    }
                    catch (Exception ex)
                    {
                        ipr.Status = "Error"; ipr.Message = ex.Message;
                        result.Add(ipr);
                    }
                }
            }

            return result;
        }

        private static string GetCellString(IXLRow row, Dictionary<string, int> headers, string key)
        {
            if (!headers.TryGetValue(key, out var col)) return string.Empty;
            return row.Cell(col).GetString();
        }

        // Build DataTable from preview rows (only OK/AutoFix/Exists if update) for bulk insert
        public DataTable BuildDataTableForBulk(IEnumerable<ImportPreviewRow> rows, bool updateExisting = false)
        {
            var dt = new DataTable();
            dt.Columns.Add("CardUID", typeof(string));
            dt.Columns.Add("BienSo", typeof(string));
            dt.Columns.Add("LoaiVeId", typeof(int));
            dt.Columns.Add("LoaiXeId", typeof(int));
            dt.Columns.Add("NgayDangKy", typeof(DateTime));
            dt.Columns.Add("NgayHetHan", typeof(DateTime));
            dt.Columns.Add("TrangThai", typeof(string));

            foreach (var r in rows)
            {
                if (r.Status.StartsWith("Error")) continue;
                // if Exists and not updateExisting, skip
                if (r.Status.Contains("Exists") && !updateExisting) continue;

                var row = dt.NewRow();
                row["CardUID"] = r.CardUID ?? string.Empty;
                // Business rule: for vãng lai / vé lượt, do NOT import BienSo
                var nv = ImportNormalization.Normalize(r.LoaiVe ?? string.Empty);
                bool isVangLai = !string.IsNullOrEmpty(nv) && (nv.Contains("VANG LAI") || nv.Contains("VE LUOT") || nv.Contains("VELUOT") || nv.Contains("VANGLAI"));
                row["BienSo"] = (isVangLai) ? (object)DBNull.Value : (string.IsNullOrWhiteSpace(r.BienSo) ? (object)DBNull.Value : r.BienSo);
                row["LoaiVeId"] = r.MappedLoaiVeId.HasValue ? (object)r.MappedLoaiVeId.Value : DBNull.Value;
                row["LoaiXeId"] = r.MappedLoaiXeId.HasValue ? (object)r.MappedLoaiXeId.Value : DBNull.Value;
                row["NgayDangKy"] = r.NgayDangKy ?? (object)DBNull.Value;
                row["NgayHetHan"] = r.NgayHetHan ?? (object)DBNull.Value;
                row["TrangThai"] = string.IsNullOrWhiteSpace(r.TrangThai) ? "Active" : r.TrangThai;
                dt.Rows.Add(row);
            }

            return dt;
        }

        // Perform bulk import using DatabaseService.BulkUpsertRFIDCards via converting DataTable to RFIDCard list
        // Returns (Inserted, Updated)
        public (int Inserted, int Updated) BulkImport(DataTable table, bool updateExisting = false)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var models = new List<RFIDCard>();
            foreach (DataRow dr in table.Rows)
            {
                var m = new RFIDCard
                {
                    UID = dr.Table.Columns.Contains("CardUID") ? (dr["CardUID"]?.ToString() ?? string.Empty) : string.Empty,
                    BienSo = dr.Table.Columns.Contains("BienSo") ? (dr["BienSo"]?.ToString() ?? string.Empty) : string.Empty,
                    LoaiVeId = dr.Table.Columns.Contains("LoaiVeId") ? Convert.ToInt32(dr["LoaiVeId"]) : 0,
                    LoaiXeId = dr.Table.Columns.Contains("LoaiXeId") ? Convert.ToInt32(dr["LoaiXeId"]) : 0,
                    // If source date missing, default to today so DB has a sensible registration date
                    NgayTao = dr.Table.Columns.Contains("NgayDangKy") && dr["NgayDangKy"] != DBNull.Value ? Convert.ToDateTime(dr["NgayDangKy"]) : DateTime.Today,
                    NgayHetHan = dr.Table.Columns.Contains("NgayHetHan") && dr["NgayHetHan"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(dr["NgayHetHan"]) : null,
                    TrangThai = dr.Table.Columns.Contains("TrangThai") ? (dr["TrangThai"]?.ToString() ?? string.Empty) : string.Empty
                };
                models.Add(m);
            }

            if (!models.Any()) return (0, 0);

            // call DB upsert which handles insert/update in batches
            var res = _db.BulkUpsertRFIDCards(models, batchSize: 1000, progress: null, updateExisting: updateExisting);
            return res;
        }

        // Export DB to Excel (formatting)
        // ExportToExcel: if activeLoaiVeId has a value (>0) export only cards with that LoaiVeId
        public void ExportToExcel(string path, int? activeLoaiVeId = null)
        {
            // Column mapping (fixed schema)
            const int COL_UID = 1;
            const int COL_BIENSO = 2;
            const int COL_LOAIXE = 3;
            const int COL_LOAIVE = 4;
            const int COL_NGAYDANGKY = 5;
            const int COL_NGAYHETHAN = 6;
            const int COL_TRANGTHAI = 7;

            // Get raw RFIDCard records from DB and resolve display names for LoaiXe/LoaiVe
            var raw = _db.GetRFIDCards();
            // If a specific LoaiVe tab is active, filter to that LoaiVeId
            if (activeLoaiVeId.HasValue && activeLoaiVeId.Value > 0)
            {
                raw = raw.Where(x => x.LoaiVeId == activeLoaiVeId.Value).ToList();
            }
            var loaiXeLookup = _db.GetLoaiXe().ToDictionary(x => x.Id, x => x.TenLoai ?? string.Empty);
            var loaiVeLookup = _db.GetLoaiVe().ToDictionary(x => x.Id, x => x.TenLoai ?? string.Empty);
            // Project to an export-friendly anonymous type with consistent fields
            var list = raw.Select(it => new
            {
                CardUID = it.UID,
                BienSo = it.BienSo,
                LoaiXe = it.LoaiXeId > 0 && loaiXeLookup.TryGetValue(it.LoaiXeId, out var lx) ? lx : string.Empty,
                LoaiVe = it.LoaiVeId > 0 && loaiVeLookup.TryGetValue(it.LoaiVeId, out var lv) ? lv : string.Empty,
                NgayDangKy = it.NgayTao == DateTime.MinValue ? (DateTime?)null : it.NgayTao,
                NgayHetHan = it.NgayHetHan,
                TrangThai = it.TrangThai
            }).ToList();

            // Presentation-only defaults: if DB has no NgayDangKy but LoaiVe is monthly, fill display dates for export
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                try
                {
                    var nv = ImportNormalization.Normalize(item.LoaiVe ?? string.Empty);
                    bool isMonthly = !string.IsNullOrEmpty(nv) && (nv.Contains("THANG") || nv.Contains("VE THANG") || nv.Contains("MONTH"));
                    if (isMonthly)
                    {
                        DateTime? dk = item.NgayDangKy;
                        DateTime? hh = item.NgayHetHan;
                        if (!dk.HasValue)
                        {
                            dk = DateTime.Today;
                        }
                        if (!hh.HasValue)
                        {
                            hh = dk?.AddMonths(1);
                        }

                        list[i] = new
                        {
                            CardUID = item.CardUID,
                            BienSo = item.BienSo,
                            LoaiXe = item.LoaiXe,
                            LoaiVe = item.LoaiVe,
                            NgayDangKy = dk,
                            NgayHetHan = hh,
                            TrangThai = item.TrangThai
                        };
                    }
                }
                catch { }
            }
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("RFIDCards");
                var headers = new[] { "CardUID", "BienSo", "LoaiXe", "LoaiVe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

                int r = 2;
                foreach (var it in list)
                {
                    // Always set each cell to preserve schema. Use empty string for nulls.
                    ws.Cell(r, COL_UID).Value = it.CardUID ?? string.Empty;
                    ws.Cell(r, COL_BIENSO).Value = string.IsNullOrWhiteSpace(it.BienSo) ? string.Empty : it.BienSo;
                    ws.Cell(r, COL_LOAIXE).Value = it.LoaiXe ?? string.Empty;
                    ws.Cell(r, COL_LOAIVE).Value = it.LoaiVe ?? string.Empty;

                    // NgayDangKy
                    var cNgayDK = ws.Cell(r, COL_NGAYDANGKY);
                    if (it.NgayDangKy != null && it.NgayDangKy != DateTime.MinValue)
                    {
                        cNgayDK.Value = it.NgayDangKy.Value;
                        cNgayDK.Style.DateFormat.Format = "dd/MM/yyyy";
                    }
                    else
                    {
                        // explicitly set empty cell to preserve column
                        cNgayDK.Value = string.Empty;
                    }

                    // NgayHetHan
                    var cNgayHH = ws.Cell(r, COL_NGAYHETHAN);
                    if (it.NgayHetHan != null && it.NgayHetHan != DateTime.MinValue)
                    {
                        cNgayHH.Value = it.NgayHetHan.Value;
                        cNgayHH.Style.DateFormat.Format = "dd/MM/yyyy";
                    }
                    else
                    {
                        cNgayHH.Value = string.Empty;
                    }

                    ws.Cell(r, COL_TRANGTHAI).Value = it.TrangThai ?? string.Empty;
                    r++;
                }

                // format header
                var hdr = ws.Range(1, 1, 1, headers.Length);
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E90FF");
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Font.Bold = true;

                ws.SheetView.FreezeRows(1);
                ws.Range(1, 1, Math.Max(1, r - 1), headers.Length).SetAutoFilter();
                ws.Columns().AdjustToContents();

                // save workbook safely
                var saved = FileSaveHelpers.SaveWorkbookSafe(wb, path);
                if (!string.Equals(saved, path, StringComparison.OrdinalIgnoreCase))
                {
                    try { LoggingService.Instance.LogInfo("Export", "ImportExportService", $"Export saved to '{saved}' because destination was locked"); } catch { }
                }
            }
        }

        // Generate template folder with three files
        public void GenerateTemplate(string folder)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            var templateEmpty = Path.Combine(folder, "TEMPLATE_TRONG.xlsx");
            var templateSample = Path.Combine(folder, "TEMPLATE_MAU.xlsx");
            var guide = Path.Combine(folder, "HUONG_DAN.txt");

            // empty template
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Template");
                var headers = new[] { "CardUID", "BienSo", "LoaiXe", "LoaiVe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                var hdr = ws.Range(1, 1, 1, headers.Length);
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E90FF");
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Font.Bold = true;
                ws.SheetView.FreezeRows(1);
                ws.RangeUsed().SetAutoFilter();
                ws.Columns().AdjustToContents();
                wb.SaveAs(templateEmpty);
            }

            // sample template
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Mau");
                var headers = new[] { "CardUID", "BienSo", "LoaiXe", "LoaiVe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(2, 1).Value = "UID001";
                ws.Cell(2, 2).Value = "59A12345";
                ws.Cell(2, 3).Value = "XE MAY";
                ws.Cell(2, 4).Value = "VE LUOT";
                ws.Cell(2, 5).Value = DateTime.Today.ToString("dd/MM/yyyy");
                ws.Cell(2, 6).Value = "";
                ws.Cell(2, 7).Value = "ACTIVE";

                ws.Cell(3, 1).Value = "UID002";
                ws.Cell(3, 2).Value = "51B67890";
                ws.Cell(3, 3).Value = "OTO";
                ws.Cell(3, 4).Value = "VE THANG";
                ws.Cell(3, 5).Value = DateTime.Today.ToString("dd/MM/yyyy");
                ws.Cell(3, 6).Value = DateTime.Today.AddDays(30).ToString("dd/MM/yyyy");
                ws.Cell(3, 7).Value = "ACTIVE";

                var hdr = ws.Range(1, 1, 1, headers.Length);
                hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E90FF");
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Font.Bold = true;
                ws.SheetView.FreezeRows(1);
                ws.RangeUsed().SetAutoFilter();
                ws.Columns().AdjustToContents();
                wb.SaveAs(templateSample);
            }

            // guide
            var sb = new StringBuilder();
            sb.AppendLine("Hướng dẫn Import RFIDCards:");
            sb.AppendLine("- Không nhập khoảng trắng dư");
            sb.AppendLine("- LoaiXe: XE MAY, OTO");
            sb.AppendLine("- LoaiVe: VE LUOT, VE THANG");
            sb.AppendLine("- Có thể nhập: \"xe may\", \"Xe Máy\" (hệ thống tự hiểu)");
            File.WriteAllText(guide, sb.ToString(), Encoding.UTF8);
        }
    }
}
