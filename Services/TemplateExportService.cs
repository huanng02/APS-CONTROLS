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
        public async Task<string?> CreateTemplateOnDesktopAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    var folderName = $"RFID_Import_Template_{DateTime.Now:yyyyMMdd_HHmmss}";
                    var folderPath = Path.Combine(desktop, folderName);

                    Directory.CreateDirectory(folderPath);

                    // Template.xlsx
                    var templatePath = Path.Combine(folderPath, "Template.xlsx");
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Template");
                        var headers = new[] { "CardUID", "BienSo", "LoaiXe", "LoaiVe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                        for (int i = 0; i < headers.Length; i++)
                            ws.Cell(1, i + 1).Value = headers[i];

                        var headerRange = ws.Range(1, 1, 1, headers.Length);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
                        headerRange.Style.Font.FontColor = XLColor.White;

                        ws.SheetView.FreezeRows(1);
                        ws.RangeUsed().SetAutoFilter();
                        ws.Columns().AdjustToContents();

                        // Add simple data validation dropdown for LoaiVe column (D) and LoaiXe column (C)
                        try
                        {
                            var dvVe = ws.Range("D2:D1000").SetDataValidation();
                            dvVe.AllowedValues = XLAllowedValues.List;
                            dvVe.InCellDropdown = true;
                            dvVe.List("Vé lượt,Vé tháng");

                            var dvXe = ws.Range("C2:C1000").SetDataValidation();
                            dvXe.AllowedValues = XLAllowedValues.List;
                            dvXe.InCellDropdown = true;
                            dvXe.List("Xe máy,Ô tô");
                        }
                        catch { }

                        var savedTemplate = FileSaveHelpers.SaveWorkbookSafe(wb, templatePath);
                        if (!string.Equals(savedTemplate, templatePath, StringComparison.OrdinalIgnoreCase))
                        {
                            try { LoggingService.Instance.LogInfo("TemplateExport", "TemplateExportService", $"Template saved to '{savedTemplate}' because destination was locked"); } catch { }
                        }
                    }

                    // Sample.xlsx
                    var samplePath = Path.Combine(folderPath, "Sample.xlsx");
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("Sample");
                        var headers = new[] { "CardUID", "BienSo", "LoaiXe", "LoaiVe", "NgayDangKy", "NgayHetHan", "TrangThai" };
                        for (int i = 0; i < headers.Length; i++)
                            ws.Cell(1, i + 1).Value = headers[i];

                        var headerRange = ws.Range(1, 1, 1, headers.Length);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
                        headerRange.Style.Font.FontColor = XLColor.White;

                        // examples
                        ws.Cell(2, 1).Value = "UID001";
                        ws.Cell(2, 2).Value = "59A-12345";
                        ws.Cell(2, 3).Value = "Xe máy";
                        ws.Cell(2, 4).Value = "Vé lượt";
                        ws.Cell(2, 5).Value = new DateTime(2026, 4, 15);
                        ws.Cell(2, 5).Style.DateFormat.Format = "dd/MM/yyyy";
                        ws.Cell(2, 6).Value = string.Empty;
                        ws.Cell(2, 7).Value = "Active";

                        ws.Cell(3, 1).Value = "UID002";
                        ws.Cell(3, 2).Value = "51B-67890";
                        ws.Cell(3, 3).Value = "Ô tô";
                        ws.Cell(3, 4).Value = "Vé tháng";
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
                            var dvVe = ws.Range("D2:D1000").SetDataValidation();
                            dvVe.AllowedValues = XLAllowedValues.List;
                            dvVe.InCellDropdown = true;
                            dvVe.List("Vé lượt,Vé tháng");

                            var dvXe = ws.Range("C2:C1000").SetDataValidation();
                            dvXe.AllowedValues = XLAllowedValues.List;
                            dvXe.InCellDropdown = true;
                            dvXe.List("Xe máy,Ô tô");
                        }
                        catch { }

                        var savedSample = FileSaveHelpers.SaveWorkbookSafe(wb, samplePath);
                        if (!string.Equals(savedSample, samplePath, StringComparison.OrdinalIgnoreCase))
                        {
                            try { LoggingService.Instance.LogInfo("TemplateExport", "TemplateExportService", $"Sample saved to '{savedSample}' because destination was locked"); } catch { }
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
