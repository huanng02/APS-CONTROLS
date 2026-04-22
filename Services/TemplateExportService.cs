using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;

namespace QuanLyGiuXe.Services
{
    public class TemplateExportService
    {
        /// <summary>
        /// Create template folder on Desktop containing Template.xlsx, Sample.xlsx and Guide.txt
        /// Returns created folder path or null on failure
        /// </summary>
        // activeLoaiVeId: if provided (>0) the template will be for that specific LoaiVe tab
        // if null or 0 -> generate template for "All" mode which requires LoaiVeId column
        public async Task<string?> CreateTemplateOnDesktopAsync(int? activeLoaiVeId = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    var folderName = $"RFID_Import_Template_{DateTime.Now:yyyyMMdd_HHmmss}";
                    var folderPath = Path.Combine(desktop, folderName);

                    Directory.CreateDirectory(folderPath);

                    // Note: Template.xlsx creation removed per request; only sample files and guide will be generated.

                    // Sample files: create two versions - single LoaiVe sample and ALL (multiple LoaiVe) sample
                    var samplePathSingle = Path.Combine(folderPath, "Sample_SingleLoaiVe.xlsx");
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Sample");
                        // For single-LoaiVe (downloaded from a specific tab) we omit LoaiVe column
                        // include CardName as column 3
                        var headers = new[] { "CardUID", "BienSo", "CardName", "LoaiXe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                        for (int i = 0; i < headers.Length; i++)
                            ws.Cell(1, i + 1).Value = headers[i];

                        var headerRange = ws.Range(1, 1, 1, headers.Length);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
                        headerRange.Style.Font.FontColor = XLColor.White;

                        // examples (no LoaiVe column because import will assign LoaiVe from selected tab)
                        ws.Cell(2, 1).Value = "UID001";
                        ws.Cell(2, 2).Value = "59A-12345";
                        ws.Cell(2, 3).Value = "Card A";
                        ws.Cell(2, 4).Value = "Xe máy";
                        ws.Cell(2, 5).Value = new DateTime(2026, 4, 15);
                        ws.Cell(2, 5).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(2, 6).Value = string.Empty;
                        ws.Cell(2, 7).Value = "Active";

                        ws.Cell(3, 1).Value = "UID002";
                        ws.Cell(3, 2).Value = "51B-67890";
                        ws.Cell(3, 3).Value = "Card B";
                        ws.Cell(3, 4).Value = "Ô tô";
                        ws.Cell(3, 5).Value = new DateTime(2026, 4, 15);
                        ws.Cell(3, 5).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(3, 6).Value = new DateTime(2026, 5, 15);
                        ws.Cell(3, 6).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(3, 7).Value = "Active";

                        ws.SheetView.FreezeRows(1);
                        ws.RangeUsed().SetAutoFilter();
                        ws.Columns().AdjustToContents();

                        try
                        {
                            int colLoaiXe = Array.IndexOf(headers, "LoaiXe");
                            if (colLoaiXe >= 0)
                            {
                                int c = colLoaiXe + 1;
                                var dvXe = ws.Range(ws.Cell(2, c), ws.Cell(1000, c)).SetDataValidation();
                                dvXe.AllowedValues = XLAllowedValues.List;
                                dvXe.InCellDropdown = true;
                                dvXe.List("Xe máy,Ô tô");
                            }
                        }
                        catch { }

                        var savedSample = FileSaveHelpers.SaveWorkbookSafe(wb, samplePathSingle);
                        if (!string.Equals(savedSample, samplePathSingle, StringComparison.OrdinalIgnoreCase))
                        {
                            try { LoggingService.Instance.LogInfo("TemplateExport", "TemplateExportService", $"Sample saved to '{savedSample}' because destination was locked"); } catch { }
                        }
                    }

                    // Sample for ALL mode that includes LoaiVeId column

                    var samplePathAll = Path.Combine(folderPath, "Sample_AllLoaiVe.xlsx");
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("SampleAll");
                        // include CardName as column 3
                        var headers = new[] { "CardUID", "BienSo", "CardName", "LoaiXe", "LoaiVe", "LoaiVeId", "NgayDangKy", "NgayHetHan", "TrangThai" };
                        for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];

                        // provide multiple rows each with explicit LoaiVeId (example ids)
                        ws.Cell(2, 1).Value = "UID001";
                        ws.Cell(2, 2).Value = "59A-12345";
                        ws.Cell(2, 3).Value = "Card A";
                        ws.Cell(2, 4).Value = "Xe máy";
                        ws.Cell(2, 5).Value = "Vé lượt";
                        ws.Cell(2, 6).Value = 1; // example LoaiVeId for Vé lượt
                        ws.Cell(2, 7).Value = new DateTime(2026, 4, 15);
                        ws.Cell(2, 7).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(2, 8).Value = string.Empty;
                        ws.Cell(2, 9).Value = "Active";

                        ws.Cell(3, 1).Value = "UID002";
                        ws.Cell(3, 2).Value = "51B-67890";
                        ws.Cell(3, 3).Value = "Card B";
                        ws.Cell(3, 4).Value = "Ô tô";
                        ws.Cell(3, 5).Value = "Vé tháng";
                        ws.Cell(3, 6).Value = 2; // example LoaiVeId for Vé tháng
                        ws.Cell(3, 7).Value = new DateTime(2026, 4, 15);
                        ws.Cell(3, 7).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(3, 8).Value = new DateTime(2026, 5, 15);
                        ws.Cell(3, 8).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(3, 9).Value = "Active";

                        ws.SheetView.FreezeRows(1);
                        ws.RangeUsed().SetAutoFilter();
                        ws.Columns().AdjustToContents();

                        var savedSampleAll = FileSaveHelpers.SaveWorkbookSafe(wb, samplePathAll);
                        if (!string.Equals(savedSampleAll, samplePathAll, StringComparison.OrdinalIgnoreCase))
                        {
                            try { LoggingService.Instance.LogInfo("TemplateExport", "TemplateExportService", $"SampleAll saved to '{savedSampleAll}' because destination was locked"); } catch { }
                        }
                    }

                    // Guide.txt
                    var guidePath = Path.Combine(folderPath, "Guide.txt");
                    var sb = new StringBuilder();
                    sb.AppendLine("Hướng dẫn Import RFID Cards");
                    sb.AppendLine();
                    sb.AppendLine("CardUID: bắt buộc, không trùng");
                    sb.AppendLine("BienSo: có thể để trống");
                    sb.AppendLine("LoaiXe: tên loại xe, ví dụ 'Xe máy' hoặc 'Ô tô'");
                    sb.AppendLine("LoaiVe: \"Vé lượt\" hoặc \"Vé tháng\"");
                    sb.AppendLine("NgayDangKy: yyyy-MM-dd (có thể trống)");
                    sb.AppendLine("NgayHetHan: chỉ dùng cho vé tháng (có thể trống)");
                    sb.AppendLine("TrangThai: Active / Inactive");
                    sb.AppendLine();
                    sb.AppendLine("Quy tắc:");
                    sb.AppendLine("- Không xóa header");
                    sb.AppendLine("- Không để trống CardUID");
                    sb.AppendLine("- Không trùng CardUID");
                    sb.AppendLine("- Format ngày đúng chuẩn (yyyy-MM-dd or dd/MM/yyyy)");

                    File.WriteAllText(guidePath, sb.ToString());

                    // open folder
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch { }

                    return folderPath;
                }
                catch (Exception ex)
                {
                    try { LoggingService.Instance.LogError("TemplateExportFailed", "TemplateExportService", ex.Message, ex); } catch { }
                    return null;
                }
            });
        }
    }
}
