using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Services
{
    public class ExcelImportService
    {
        // Read file into list of import models (async, streaming)
        public async Task<List<RFIDCardImportModel>> ReadExcelAsync(string path, IProgress<int>? progress = null, CancellationToken? token = null)
        {
            return await Task.Run(() =>
            {
                var list = new List<RFIDCardImportModel>();
                using (var wb = new XLWorkbook(path))
                {
                    var ws = wb.Worksheet(1);
                    var used = ws.RangeUsed();
                    if (used == null) return list;

                    var rows = used.RowsUsed().Skip(1);
                    int total = rows.Count();
                    int idx = 0;
                    foreach (var r in rows)
                    {
                        token?.ThrowIfCancellationRequested();
                        var model = new RFIDCardImportModel();
                        model.RowNumber = r.RowNumber();
                        // defensively get cells by index; if missing treat as empty
                        model.CardUID = r.Cell(1).GetString().Trim();
                        model.BienSo = r.Cell(2).GetString().Trim();
                        // added CardName in column 3, shift LoaiXe/LoaiVe to columns 4/5
                        model.CardName = r.Cell(3).GetString().Trim();
                        model.LoaiXeTextRaw = r.Cell(4).GetString().Trim();
                        model.LoaiVeTextRaw = r.Cell(5).GetString().Trim();
                        // shifted: NgayDangKy at col 6, NgayHetHan col7, TrangThai col8
                        model.NgayDangKy = ParseDateOrNull(r.Cell(6).GetString());
                        model.NgayHetHan = ParseDateOrNull(r.Cell(7).GetString());
                        model.TrangThai = r.Cell(8).GetString().Trim();

                        model.Status = ImportStatus.UNKNOWN;
                        // Normalize raw texts for later mapping
                        model.LoaiXeTextRaw = ImportNormalization.Normalize(model.LoaiXeTextRaw);
                        model.LoaiVeTextRaw = ImportNormalization.Normalize(model.LoaiVeTextRaw);
                        list.Add(model);

                        idx++;
                        progress?.Report((idx * 100) / Math.Max(1, total));
                    }
                }

                return list;
            });
        }

        private static int? ParseIntOrNull(string s)
        {
            if (int.TryParse(s, out int v)) return v;
            return null;
        }

        private static DateTime? ParseDateOrNull(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            // try parse with invariant and common formats
            var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "yyyy-MM-dd HH:mm", "M/d/yyyy", "M/d/yyyy HH:mm" };
            if (DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime d)) return d;
            if (DateTime.TryParse(s, out d)) return d;
            return null;
        }
    }
}
