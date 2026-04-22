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
                    sb.AppendLine("HƯỚNG DẪN IMPORT RFID CARDS");
                    sb.AppendLine();
                    sb.AppendLine("1) Các file có sẵn trong thư mục:");
                    sb.AppendLine("   - Sample_SingleLoaiVe.xlsx : mẫu dùng khi bạn đang ở 1 tab loại vé cụ thể (không có cột LoaiVe/LoaiVeId)");
                    sb.AppendLine("   - Sample_AllLoaiVe.xlsx    : mẫu dùng khi import ở tab 'Tất cả' (có cột LoaiVe và LoaiVeId)");
                    sb.AppendLine();
                    sb.AppendLine("2) Các cột trong file (Single tab):");
                    sb.AppendLine("   CardUID (bắt buộc), BienSo (tùy chọn), CardName (tên hiển thị thẻ), LoaiXe, NgayDangKy, NgayHetHan, TrangThai");
                    sb.AppendLine();
                    sb.AppendLine("3) Các cột trong file (All tab):");
                    sb.AppendLine("   CardUID (bắt buộc), BienSo, CardName, LoaiXe, LoaiVe, LoaiVeId (bắt buộc), NgayDangKy, NgayHetHan, TrangThai");
                    sb.AppendLine();
                    sb.AppendLine("4) Quy tắc theo tab:");
                    sb.AppendLine("   - Nếu bạn đang ở 1 tab loại vé cụ thể: hệ thống sẽ TỰ GÁN LoaiVe theo tab, bạn KHÔNG CẦN nhập LoaiVe/LoaiVeId trong Excel.");
                    sb.AppendLine("   - Nếu bạn ở tab 'Tất cả' (All): file phải có cột LoaiVeId (ticketTypeId) chứa giá trị số tương ứng trong DB. Nếu thiếu hoặc không hợp lệ -> file/rows sẽ bị reject.");
                    sb.AppendLine();
                    sb.AppendLine("5) Các lưu ý khác:");
                    sb.AppendLine("   - CardUID: bắt buộc, duy nhất. Không để trống hoặc trùng.");
                    sb.AppendLine("   - CardName: tên hiển thị của thẻ (có thể để trống). Nếu DB chưa có cột CardName, hệ thống sẽ bỏ qua cột này.");
                    sb.AppendLine("   - LoaiXe: nhập tên (ví dụ 'Xe máy' hoặc 'Ô tô') hoặc mã tương ứng; hệ thống sẽ cố map tự động.");
                    sb.AppendLine("   - Ngày: dùng định dạng yyyy-MM-dd hoặc dd/MM/yyyy.");
                    sb.AppendLine("   - TrangThai: 'Active' để import, các giá trị khác sẽ bị bỏ qua (Skipped).");
                    sb.AppendLine();
                    sb.AppendLine("6) Thao tác đề xuất:");
                    sb.AppendLine("   - Nếu không chắc LoaiVeId, tải Sample_SingleLoaiVe.xlsx khi ở tab LoaiVe cụ thể hoặc Sample_AllLoaiVe.xlsx khi ở All.");
                    sb.AppendLine("   - Luôn Preview trước khi Import để kiểm tra lỗi.");

                    File.WriteAllText(guidePath, sb.ToString(), Encoding.UTF8);

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
