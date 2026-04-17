using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using ClosedXML.Excel;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class ExcelExportService
    {
        /// <summary>
        /// Export RFIDCards to an .xlsx file. Returns the full path on success, null on failure.
        /// </summary>
        public string? ExportRFIDCardsToExcel(IEnumerable<RFIDCard> cards, string folder, bool openAfter = false)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (string.IsNullOrEmpty(folder)) throw new ArgumentException("Folder path is required", nameof(folder));

            try
            {
                Directory.CreateDirectory(folder);
                var filename = Path.Combine(folder, $"RFIDCards_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("RFIDCards");

                // Define headers (do NOT include internal Id column)
                var headers = new[] { "CardUID", "BienSo", "LoaiXeId", "LoaiVe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cell(1, i + 1).Value = headers[i];

                var headerRange = ws.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125); // blue
                headerRange.Style.Font.FontColor = XLColor.White;

                // load LoaiXe lookup from DB to avoid hardcoding
                var loaiXeMap = new Dictionary<int, string>();
                try
                {
                    var db = new DatabaseService();
                    var listLoaiXe = db.GetLoaiXe();
                    loaiXeMap = listLoaiXe.Where(x => x != null).ToDictionary(x => x.Id, x => x.TenLoai ?? string.Empty);
                }
                catch
                {
                    // fallback: empty map
                }

                int r = 2;
                foreach (var c in cards)
                {
                    // CardUID
                    ws.Cell(r, 1).Value = c?.UID ?? string.Empty;

                    // BienSo
                    ws.Cell(r, 2).Value = string.IsNullOrEmpty(c?.BienSo) ? string.Empty : c.BienSo;

                    // LoaiXe: try lookup from DB, fallback to numeric id if not found
                    string loaiXeText = string.Empty;
                    if (c != null)
                    {
                        if (c.LoaiXeId != 0 && loaiXeMap.TryGetValue(c.LoaiXeId, out var name) && !string.IsNullOrEmpty(name))
                            loaiXeText = name;
                        else
                            loaiXeText = c.LoaiXeId == 0 ? string.Empty : c.LoaiXeId.ToString();
                    }
                    ws.Cell(r, 3).Value = loaiXeText;

                    // LoaiVe mapping (keep simple mapping)
                    string loaiVeText = string.Empty;
                    if (c != null)
                    {
                        loaiVeText = c.LoaiVeId switch
                        {
                            1 => "Vé lượt",
                            2 => "Vé tháng",
                            _ => string.Empty
                        };
                    }
                    ws.Cell(r, 4).Value = loaiVeText;

                    // NgayDangKy: if empty or MinValue -> leave blank; else write DateTime value
                    if (c != null && c.NgayTao != DateTime.MinValue)
                    {
                        ws.Cell(r, 5).Value = c.NgayTao;
                        // include time for NgayDangKy like NgayHetHan
                        ws.Cell(r, 5).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                    }
                    else
                    {
                        ws.Cell(r, 5).Value = string.Empty;
                    }

                    // NgayHetHan: nullable
                    if (c != null && c.NgayHetHan.HasValue)
                    {
                        ws.Cell(r, 6).Value = c.NgayHetHan.Value;
                        ws.Cell(r, 6).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                    }
                    else
                    {
                        ws.Cell(r, 6).Value = string.Empty;
                    }

                    // TrangThai
                    ws.Cell(r, 7).Value = c?.TrangThai ?? string.Empty;

                    r++;
                }

                // Freeze header, autofilter and auto-fit
                ws.SheetView.FreezeRows(1);
                var used = ws.RangeUsed();
                used?.SetAutoFilter();
                ws.Columns().AdjustToContents();

                // Save
                wb.SaveAs(filename);

                if (openAfter)
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = filename,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch { /* ignore open errors */ }
                }

                return filename;
            }
            catch (Exception ex)
            {
                try { LoggingService.Instance.LogError("ExportExcelFailed", "ExcelExportService", ex.Message, ex); } catch { }
                return null;
            }
        }
    }
}
