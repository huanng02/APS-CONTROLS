using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class EmployeeImportService
    {
        private readonly DatabaseService _db = new DatabaseService();

        public static string NormalizeHeader(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = ImportExportService.RemoveDiacritics(input);
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToUpperInvariant(ch));
                }
            }
            return sb.ToString();
        }

        private List<string> GetActiveCompanies()
        {
            var list = new List<string>();
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Name FROM dbo.Companies WHERE IsDeleted = 0 AND Status = 'Active' ORDER BY Name", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) list.Add(r["Name"].ToString() ?? string.Empty);
                    }
                }
            }
            catch { }
            if (!list.Any()) list.Add("Công ty mặc định");
            return list;
        }

        private List<string> GetActiveDepartments()
        {
            var list = new List<string>();
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT DISTINCT DepartmentName FROM dbo.Departments WHERE IsDeleted = 0 AND Status = 'Active' ORDER BY DepartmentName", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) list.Add(r["DepartmentName"].ToString() ?? string.Empty);
                    }
                }
            }
            catch { }
            return list;
        }

        private List<string> GetActivePositions()
        {
            var list = new List<string>();
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT PositionName FROM dbo.Positions WHERE IsDeleted = 0 AND Status = 'Active' ORDER BY PositionName", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read()) list.Add(r["PositionName"].ToString() ?? string.Empty);
                    }
                }
            }
            catch { }
            if (!list.Any())
            {
                list.AddRange(new[] { "Director", "Manager", "Employee", "Security", "Visitor" });
            }
            return list;
        }

        private string GetCompanyNameById(int id)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Name FROM dbo.Companies WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        return cmd.ExecuteScalar()?.ToString()?.Trim() ?? string.Empty;
                    }
                }
            }
            catch { return string.Empty; }
        }

        private string GetDepartmentNameById(int id)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT DepartmentName FROM dbo.Departments WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        return cmd.ExecuteScalar()?.ToString()?.Trim() ?? string.Empty;
                    }
                }
            }
            catch { return string.Empty; }
        }

        private string GetSampleName(int idx)
        {
            var names = new[]
            {
                "Nguyễn Văn A",
                "Trần Thị B",
                "Lê Văn C",
                "Phạm Thị D",
                "Hoàng Văn E",
                "Huỳnh Thị F",
                "Phan Văn G",
                "Vũ Thị H",
                "Võ Văn I",
                "Đặng Thị K",
                "Bùi Văn L",
                "Đỗ Thị M",
                "Hồ Văn N",
                "Ngô Thị O",
                "Dương Văn P",
                "Lý Thị Q",
                "Trương Văn R",
                "Nguyễn Thị S",
                "Trần Văn T",
                "Lê Thị U"
            };
            return names[idx % names.Length];
        }

        public void GenerateTemplateFile(string outputPath, int? defaultCompanyId = null, int? defaultDepartmentId = null)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Template");
                var headers = new[]
                {
                    "Mã nhân viên (Bắt buộc)",
                    "Họ và tên (Bắt buộc)",
                    "Tên công ty",
                    "Tên phòng ban",
                    "Tên chức vụ",
                    "Số điện thoại",
                    "Email",
                    "Số CCCD"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    ws.Cell(1, i + 1).Value = headers[i];
                }

                var headerRange = ws.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(31, 73, 125);
                headerRange.Style.Font.FontColor = XLColor.White;

                // Load databases for data validation lists
                var companies = GetActiveCompanies();
                var departments = GetActiveDepartments();
                var positions = GetActivePositions();

                var wsList = wb.Worksheets.Add("DataLists");
                wsList.Cell(1, 1).Value = "Companies";
                for (int i = 0; i < companies.Count; i++) wsList.Cell(i + 2, 1).Value = companies[i];

                wsList.Cell(1, 2).Value = "Departments";
                for (int i = 0; i < departments.Count; i++) wsList.Cell(i + 2, 2).Value = departments[i];

                wsList.Cell(1, 3).Value = "Positions";
                for (int i = 0; i < positions.Count; i++) wsList.Cell(i + 2, 3).Value = positions[i];

                wsList.Hide(); // Hide database lists sheet to keep UI clean

                // Validation drop-down for Company (Column 3)
                if (companies.Any())
                {
                    var compVal = ws.Range(ws.Cell(2, 3), ws.Cell(1000, 3)).SetDataValidation();
                    compVal.AllowedValues = XLAllowedValues.List;
                    compVal.InCellDropdown = true;
                    compVal.List(wsList.Range(2, 1, companies.Count + 1, 1));
                }

                // Validation drop-down for Department (Column 4)
                if (departments.Any())
                {
                    var deptVal = ws.Range(ws.Cell(2, 4), ws.Cell(1000, 4)).SetDataValidation();
                    deptVal.AllowedValues = XLAllowedValues.List;
                    deptVal.InCellDropdown = true;
                    deptVal.List(wsList.Range(2, 2, departments.Count + 1, 2));
                }

                // Validation drop-down for Position (Column 5)
                if (positions.Any())
                {
                    var posVal = ws.Range(ws.Cell(2, 5), ws.Cell(1000, 5)).SetDataValidation();
                    posVal.AllowedValues = XLAllowedValues.List;
                    posVal.InCellDropdown = true;
                    posVal.List(wsList.Range(2, 3, positions.Count + 1, 3));
                }

                // Determine default company and department names
                string defaultCompName = "";
                if (defaultCompanyId.HasValue && defaultCompanyId.Value > 0)
                {
                    defaultCompName = GetCompanyNameById(defaultCompanyId.Value);
                }
                if (string.IsNullOrEmpty(defaultCompName))
                {
                    defaultCompName = companies.FirstOrDefault() ?? "Công ty mặc định";
                }

                string defaultDeptName = "";
                if (defaultDepartmentId.HasValue && defaultDepartmentId.Value > 0)
                {
                    defaultDeptName = GetDepartmentNameById(defaultDepartmentId.Value);
                }
                if (string.IsNullOrEmpty(defaultDeptName))
                {
                    defaultDeptName = departments.FirstOrDefault() ?? "Phòng Kỹ Thuật";
                }

                // Generate example rows for all active positions
                int startRow = 2;
                for (int idx = 0; idx < positions.Count; idx++)
                {
                    int currentRow = startRow + idx;
                    string pos = positions[idx];

                    // Generate Employee Code: NV001, NV002, ...
                    ws.Cell(currentRow, 1).Value = $"NV{(idx + 1):D3}";

                    // Generate Full Name: Nguyễn Văn A, Trần Thị B, ...
                    string sampleName = GetSampleName(idx);
                    ws.Cell(currentRow, 2).Value = sampleName;

                    // Company Name
                    ws.Cell(currentRow, 3).Value = defaultCompName;

                    // Department Name
                    string deptName = defaultDeptName;
                    if (departments.Any() && !defaultDepartmentId.HasValue)
                    {
                        deptName = departments[idx % departments.Count];
                    }
                    ws.Cell(currentRow, 4).Value = deptName;

                    // Position Name
                    ws.Cell(currentRow, 5).Value = pos;

                    // Phone Number: 0987654321, 0987654322, ...
                    ws.Cell(currentRow, 6).Value = $"09876543{(21 + idx):D2}";

                    // Email: vana@aps.com, tranb@aps.com, ...
                    string emailPrefix = ImportExportService.RemoveDiacritics(sampleName).Replace(" ", "").ToLower();
                    ws.Cell(currentRow, 7).Value = $"{emailPrefix}@aps.com";

                    // CCCD: 012345678901, 012345678902, ...
                    ws.Cell(currentRow, 8).Value = $"012345678{(901 + idx):D3}";
                }

                ws.SheetView.FreezeRows(1);
                ws.RangeUsed().SetAutoFilter();
                ws.Columns().AdjustToContents();

                wb.SaveAs(outputPath);
            }
        }

        public List<EmployeeImportPreviewRow> PreviewFromExcel(string path, int? defaultCompanyId = null, int? defaultDepartmentId = null)
        {
            var result = new List<EmployeeImportPreviewRow>();
            if (!File.Exists(path)) return result;

            var dictXe = BuildLoaiXeDict();
            var dictVe = BuildLoaiVeDict();
            var existingEmployees = BuildEmployeeDict();
            var existingCards = BuildCardDict();

            var uidsInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var wb = new XLWorkbook(path))
            {
                var ws = wb.Worksheets.First();
                var firstRow = ws.FirstRowUsed()?.RowNumber() ?? 1;
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                var headerRow = ws.Row(firstRow);

                var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= ws.LastColumnUsed().ColumnNumber(); c++)
                {
                    var cell = headerRow.Cell(c).GetString();
                    if (string.IsNullOrWhiteSpace(cell)) continue;
                    headers[NormalizeHeader(cell)] = c;
                }

                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    var row = ws.Row(r);

                    bool isEmpty = true;
                    for (int c = 1; c <= ws.LastColumnUsed().ColumnNumber(); c++)
                    {
                        if (!string.IsNullOrWhiteSpace(row.Cell(c).GetString()))
                        {
                            isEmpty = false;
                            break;
                        }
                    }
                    if (isEmpty) continue;

                    var ipr = new EmployeeImportPreviewRow { RowNumber = r };
                    try
                    {
                        ipr.EmployeeCode = GetCellString(row, headers, "Mã nhân viên (Bắt buộc)", "Mã nhân viên", "MaNhanVien", "EmployeeCode");
                        ipr.FullName = GetCellString(row, headers, "Họ và tên (Bắt buộc)", "Họ và tên", "HoVaTen", "FullName", "Tên nhân viên");
                        ipr.CompanyName = GetCellString(row, headers, "Tên công ty", "Công ty", "CompanyName", "CongTy");
                        ipr.DepartmentName = GetCellString(row, headers, "Tên phòng ban", "Phòng ban", "DepartmentName", "PhongBan");
                        ipr.PositionName = GetCellString(row, headers, "Tên chức vụ", "Chức vụ", "PositionName", "ChucVu");
                        ipr.Phone = GetCellString(row, headers, "Số điện thoại", "Điện thoại", "SĐT", "Phone", "SoDienThoai");
                        ipr.Email = GetCellString(row, headers, "Email");
                        ipr.CCCD = GetCellString(row, headers, "Số CCCD", "CCCD", "SoCCCD");

                        ipr.CardUID = GetCellString(row, headers, "Mã thẻ (Tùy chọn)", "Mã thẻ", "CardUID", "MaThe");
                        ipr.BienSo = GetCellString(row, headers, "Biển số xe (Tùy chọn)", "Biển số xe", "BienSo", "BienSoXe");
                        ipr.LoaiXe = GetCellString(row, headers, "Loại xe (Tùy chọn)", "Loại xe", "LoaiXe");
                        ipr.LoaiVe = GetCellString(row, headers, "Loại vé (Tùy chọn)", "Loại vé", "LoaiVe");
                        var ngayHhStr = GetCellString(row, headers, "Ngày hết hạn thẻ (Tùy chọn - dd/MM/yyyy)", "Ngày hết hạn thẻ", "Ngày hết hạn", "NgayHetHan");

                        ipr.EmployeeCode = ipr.EmployeeCode.Trim();
                        ipr.FullName = ipr.FullName.Trim();
                        
                        ipr.CompanyName = ipr.CompanyName.Trim();
                        if (string.IsNullOrWhiteSpace(ipr.CompanyName) && defaultCompanyId.HasValue)
                        {
                            ipr.CompanyName = GetCompanyNameById(defaultCompanyId.Value);
                        }

                        ipr.DepartmentName = ipr.DepartmentName.Trim();
                        if (string.IsNullOrWhiteSpace(ipr.DepartmentName) && defaultDepartmentId.HasValue)
                        {
                            ipr.DepartmentName = GetDepartmentNameById(defaultDepartmentId.Value);
                        }

                        ipr.PositionName = ipr.PositionName.Trim();
                        ipr.Phone = ipr.Phone.Trim();
                        ipr.Email = ipr.Email.Trim();
                        ipr.CCCD = ipr.CCCD.Trim();
                        ipr.CardUID = ipr.CardUID.Trim();
                        ipr.BienSo = ipr.BienSo.Trim();
                        ipr.LoaiXe = ipr.LoaiXe.Trim();
                        ipr.LoaiVe = ipr.LoaiVe.Trim();

                        if (!string.IsNullOrWhiteSpace(ngayHhStr))
                        {
                            if (DateTime.TryParse(ngayHhStr, out var d)) ipr.NgayHetHan = d;
                            else if (DateTime.TryParseExact(ngayHhStr, new[] { "dd/MM/yyyy", "yyyy-MM-dd" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) ipr.NgayHetHan = d;
                        }

                        if (string.IsNullOrWhiteSpace(ipr.EmployeeCode))
                        {
                            ipr.Status = "Error";
                            ipr.Message = "Mã nhân viên bắt buộc.";
                            result.Add(ipr);
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(ipr.FullName))
                        {
                            ipr.Status = "Error";
                            ipr.Message = "Họ và tên bắt buộc.";
                            result.Add(ipr);
                            continue;
                        }

                        if (codesInFile.Contains(ipr.EmployeeCode))
                        {
                            ipr.Status = "Error";
                            ipr.Message = "Mã nhân viên trùng lặp trong file.";
                            result.Add(ipr);
                            continue;
                        }
                        codesInFile.Add(ipr.EmployeeCode);

                        // RFID Card optional checks
                        if (!string.IsNullOrWhiteSpace(ipr.CardUID))
                        {
                            if (uidsInFile.Contains(ipr.CardUID))
                            {
                                ipr.Status = "Error";
                                ipr.Message = "Mã thẻ trùng lặp trong file.";
                                result.Add(ipr);
                                continue;
                            }
                            uidsInFile.Add(ipr.CardUID);

                            if (existingCards.TryGetValue(ipr.CardUID, out var cardInfo))
                            {
                                if (cardInfo.EmployeeId.HasValue)
                                {
                                    var assignedEmpCode = GetEmployeeCodeById(cardInfo.EmployeeId.Value);
                                    if (assignedEmpCode != null && !string.Equals(assignedEmpCode, ipr.EmployeeCode, StringComparison.OrdinalIgnoreCase))
                                    {
                                        ipr.Status = "Error";
                                        ipr.Message = $"Thẻ này đã gán cho nhân viên khác ({assignedEmpCode}).";
                                        result.Add(ipr);
                                        continue;
                                    }
                                }
                            }

                            // Verify LoaiXe
                            var normXe = ImportExportService.Normalize(ipr.LoaiXe);
                            if (!string.IsNullOrWhiteSpace(normXe) && !dictXe.ContainsKey(normXe))
                            {
                                int best = int.MaxValue; string bestKey = null;
                                foreach (var kv in dictXe)
                                {
                                    int d = ImportExportService.LevenshteinDistance(normXe, kv.Key);
                                    if (d < best) { best = d; bestKey = kv.Key; }
                                }
                                if (best <= 2 && bestKey != null)
                                {
                                    ipr.LoaiXe = dictXe[bestKey].Name;
                                    ipr.Status = "AutoFix";
                                    ipr.Message = $"Loại xe tự động khớp sang '{ipr.LoaiXe}'";
                                }
                                else
                                {
                                    ipr.Status = "Error";
                                    ipr.Message = "Loại xe không nhận diện được.";
                                    result.Add(ipr);
                                    continue;
                                }
                            }

                            // Verify LoaiVe
                            var normVe = ImportExportService.Normalize(ipr.LoaiVe);
                            if (!string.IsNullOrWhiteSpace(normVe) && !dictVe.ContainsKey(normVe))
                            {
                                int best = int.MaxValue; string bestKey = null;
                                foreach (var kv in dictVe)
                                {
                                    int d = ImportExportService.LevenshteinDistance(normVe, kv.Key);
                                    if (d < best) { best = d; bestKey = kv.Key; }
                                }
                                if (best <= 2 && bestKey != null)
                                {
                                    ipr.LoaiVe = dictVe[bestKey].Name;
                                    ipr.Status = string.IsNullOrEmpty(ipr.Status) ? "AutoFix" : ipr.Status + ";AutoFix";
                                    ipr.Message = (string.IsNullOrEmpty(ipr.Message) ? "" : ipr.Message + "; ") + $"Loại vé tự động khớp sang '{ipr.LoaiVe}'";
                                }
                                else
                                {
                                    ipr.Status = "Error";
                                    ipr.Message = "Loại vé không nhận diện được.";
                                    result.Add(ipr);
                                    continue;
                                }
                            }
                        }

                        if (existingEmployees.ContainsKey(ipr.EmployeeCode))
                        {
                            ipr.Status = string.IsNullOrEmpty(ipr.Status) ? "Exists" : ipr.Status + ";Exists";
                            ipr.Message = (string.IsNullOrEmpty(ipr.Message) ? "" : ipr.Message + "; ") + "Mã nhân viên đã tồn tại (sẽ cập nhật thông tin)";
                        }

                        if (string.IsNullOrEmpty(ipr.Status))
                        {
                            ipr.Status = "OK";
                        }
                    }
                    catch (Exception ex)
                    {
                        ipr.Status = "Error";
                        ipr.Message = ex.Message;
                    }
                    result.Add(ipr);
                }
            }

            return result;
        }

        private string GetCellString(IXLRow row, Dictionary<string, int> headers, params string[] keys)
        {
            foreach (var key in keys)
            {
                var normKey = NormalizeHeader(key);
                if (headers.TryGetValue(normKey, out var col))
                {
                    return row.Cell(col).GetString()?.Trim() ?? string.Empty;
                }
            }
            return string.Empty;
        }

        private string GetEmployeeCodeById(int id)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT EmployeeCode FROM dbo.Employees WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        return cmd.ExecuteScalar()?.ToString()?.Trim() ?? string.Empty;
                    }
                }
            }
            catch { return string.Empty; }
        }

        private Dictionary<string, (int Id, string Name)> BuildLoaiXeDict()
        {
            var dict = new Dictionary<string, (int Id, string Name)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id, TenLoai FROM dbo.LoaiXe", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            var name = reader["TenLoai"]?.ToString() ?? string.Empty;
                            var k = ImportExportService.Normalize(name);
                            if (!string.IsNullOrEmpty(k) && !dict.ContainsKey(k))
                                dict[k] = (id, name);
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        private Dictionary<string, (int Id, string Name)> BuildLoaiVeDict()
        {
            var dict = new Dictionary<string, (int Id, string Name)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id, TenLoai FROM dbo.LoaiVe", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            var name = reader["TenLoai"]?.ToString() ?? string.Empty;
                            var k = ImportExportService.Normalize(name);
                            if (!string.IsNullOrEmpty(k) && !dict.ContainsKey(k))
                                dict[k] = (id, name);
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        private Dictionary<string, int> BuildEmployeeDict()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id, EmployeeCode FROM dbo.Employees WHERE IsDeleted = 0", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            var code = reader["EmployeeCode"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(code))
                                dict[code] = id;
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        private Dictionary<string, (int Id, int? EmployeeId)> BuildCardDict()
        {
            var dict = new Dictionary<string, (int Id, int? EmployeeId)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(_db.GetConnectionString()))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id, CardUID, EmployeeId FROM dbo.RFIDCards", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            var uid = reader["CardUID"]?.ToString()?.Trim();
                            var empId = reader["EmployeeId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["EmployeeId"]);
                            if (!string.IsNullOrEmpty(uid) && !dict.ContainsKey(uid))
                                dict[uid] = (id, empId);
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        public void Import(List<EmployeeImportPreviewRow> rows, out int inserted, out int updated, int? defaultCompanyId = null, int? defaultDepartmentId = null)
        {
            inserted = 0;
            updated = 0;

            if (rows == null || !rows.Any()) return;

            var connStr = _db.GetConnectionString();
            var dictXe = BuildLoaiXeDict();
            var dictVe = BuildLoaiVeDict();

            int defaultLoaiXeId = dictXe.Values.FirstOrDefault().Id;
            int defaultLoaiVeId = dictVe.Values.FirstOrDefault().Id;

            using (var conn = new System.Data.SqlClient.SqlConnection(connStr))
            {
                conn.Open();

                foreach (var r in rows)
                {
                    if (r.Status.StartsWith("Error")) continue;

                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Resolve Company
                            int companyId;
                            var compName = r.CompanyName.Trim();
                            if (string.IsNullOrWhiteSpace(compName) && defaultCompanyId.HasValue)
                            {
                                companyId = defaultCompanyId.Value;
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(compName)) compName = "Công ty mặc định";
                                var compCode = MakeInitialsCode(compName);

                                using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id FROM dbo.Companies WHERE Name = @name AND IsDeleted = 0", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@name", compName);
                                    var val = cmd.ExecuteScalar();
                                    if (val != null)
                                    {
                                        companyId = Convert.ToInt32(val);
                                    }
                                    else
                                    {
                                        var baseCode = compCode;
                                        var attempt = 0;
                                        while (true)
                                        {
                                            var testCode = attempt == 0 ? baseCode : $"{baseCode}_{attempt}";
                                            using (var cmdChk = new System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM dbo.Companies WHERE Code = @code AND IsDeleted = 0", conn, trans))
                                            {
                                                cmdChk.Parameters.AddWithValue("@code", testCode);
                                                if ((int)cmdChk.ExecuteScalar() == 0)
                                                {
                                                    compCode = testCode;
                                                    break;
                                                }
                                            }
                                            attempt++;
                                        }

                                        using (var cmdIns = new System.Data.SqlClient.SqlCommand(
                                            "INSERT INTO dbo.Companies (Code, Name, Status, IsDeleted) VALUES (@code, @name, 'Active', 0); SELECT SCOPE_IDENTITY();", conn, trans))
                                        {
                                            cmdIns.Parameters.AddWithValue("@code", compCode);
                                            cmdIns.Parameters.AddWithValue("@name", compName);
                                            companyId = Convert.ToInt32(cmdIns.ExecuteScalar());
                                        }
                                    }
                                }
                            }

                            // 2. Resolve Department
                            int? departmentId = null;
                            var deptName = r.DepartmentName.Trim();
                            if (string.IsNullOrWhiteSpace(deptName) && defaultDepartmentId.HasValue)
                            {
                                departmentId = defaultDepartmentId.Value;
                            }
                            else if (!string.IsNullOrWhiteSpace(deptName))
                            {
                                using (var cmd = new System.Data.SqlClient.SqlCommand(
                                    "SELECT Id FROM dbo.Departments WHERE CompanyId = @compId AND DepartmentName = @name AND IsDeleted = 0", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@compId", companyId);
                                    cmd.Parameters.AddWithValue("@name", deptName);
                                    var val = cmd.ExecuteScalar();
                                    if (val != null)
                                    {
                                        departmentId = Convert.ToInt32(val);
                                    }
                                    else
                                    {
                                        using (var cmdIns = new System.Data.SqlClient.SqlCommand(
                                            "INSERT INTO dbo.Departments (CompanyId, DepartmentName, Status, IsDeleted) VALUES (@compId, @name, 'Active', 0); SELECT SCOPE_IDENTITY();", conn, trans))
                                        {
                                            cmdIns.Parameters.AddWithValue("@compId", companyId);
                                            cmdIns.Parameters.AddWithValue("@name", deptName);
                                            departmentId = Convert.ToInt32(cmdIns.ExecuteScalar());
                                        }
                                    }
                                }
                            }

                            // 3. Resolve Position
                            int positionId;
                            var posName = string.IsNullOrWhiteSpace(r.PositionName) ? "Employee" : r.PositionName.Trim();
                            using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id FROM dbo.Positions WHERE PositionName = @name AND IsDeleted = 0", conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@name", posName);
                                var val = cmd.ExecuteScalar();
                                if (val != null)
                                {
                                    positionId = Convert.ToInt32(val);
                                }
                                else
                                {
                                    using (var cmdIns = new System.Data.SqlClient.SqlCommand(
                                        "INSERT INTO dbo.Positions (PositionName, Status, IsDeleted) VALUES (@name, 'Active', 0); SELECT SCOPE_IDENTITY();", conn, trans))
                                    {
                                        cmdIns.Parameters.AddWithValue("@name", posName);
                                        positionId = Convert.ToInt32(cmdIns.ExecuteScalar());
                                    }
                                }
                            }

                            // 4. Resolve Employee
                            int employeeId;
                            bool isUpdate = false;
                            using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id FROM dbo.Employees WHERE EmployeeCode = @code AND IsDeleted = 0", conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@code", r.EmployeeCode.Trim());
                                var val = cmd.ExecuteScalar();
                                if (val != null)
                                {
                                    employeeId = Convert.ToInt32(val);
                                    isUpdate = true;
                                }
                                else
                                {
                                    using (var cmdDeleted = new System.Data.SqlClient.SqlCommand("SELECT Id FROM dbo.Employees WHERE EmployeeCode = @code AND IsDeleted = 1", conn, trans))
                                    {
                                        cmdDeleted.Parameters.AddWithValue("@code", r.EmployeeCode.Trim());
                                        var valDeleted = cmdDeleted.ExecuteScalar();
                                        if (valDeleted != null)
                                        {
                                            employeeId = Convert.ToInt32(valDeleted);
                                            isUpdate = true;
                                        }
                                        else
                                        {
                                            employeeId = 0;
                                        }
                                    }
                                }
                            }

                            if (isUpdate)
                            {
                                using (var cmdUpd = new System.Data.SqlClient.SqlCommand(
                                    @"UPDATE dbo.Employees 
                                      SET FullName = @name, CompanyId = @compId, PositionId = @posId, DepartmentId = @deptId,
                                          Phone = @phone, Email = @email, CCCD = @cccd, IsDeleted = 0, Status = 'Active'
                                      WHERE Id = @id", conn, trans))
                                {
                                    cmdUpd.Parameters.AddWithValue("@id", employeeId);
                                    cmdUpd.Parameters.AddWithValue("@name", r.FullName.Trim());
                                    cmdUpd.Parameters.AddWithValue("@compId", companyId);
                                    cmdUpd.Parameters.AddWithValue("@posId", positionId);
                                    cmdUpd.Parameters.AddWithValue("@deptId", (object?)departmentId ?? DBNull.Value);
                                    cmdUpd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(r.Phone) ? DBNull.Value : (object)r.Phone.Trim());
                                    cmdUpd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(r.Email) ? DBNull.Value : (object)r.Email.Trim());
                                    cmdUpd.Parameters.AddWithValue("@cccd", string.IsNullOrWhiteSpace(r.CCCD) ? DBNull.Value : (object)r.CCCD.Trim());
                                    cmdUpd.ExecuteNonQuery();
                                }
                                updated++;
                            }
                            else
                            {
                                using (var cmdIns = new System.Data.SqlClient.SqlCommand(
                                    @"INSERT INTO dbo.Employees (EmployeeCode, FullName, CompanyId, PositionId, DepartmentId, Phone, Email, CCCD, Status, IsDeleted)
                                      VALUES (@code, @name, @compId, @posId, @deptId, @phone, @email, @cccd, 'Active', 0);
                                      SELECT SCOPE_IDENTITY();", conn, trans))
                                {
                                    cmdIns.Parameters.AddWithValue("@code", r.EmployeeCode.Trim());
                                    cmdIns.Parameters.AddWithValue("@name", r.FullName.Trim());
                                    cmdIns.Parameters.AddWithValue("@compId", companyId);
                                    cmdIns.Parameters.AddWithValue("@posId", positionId);
                                    cmdIns.Parameters.AddWithValue("@deptId", (object?)departmentId ?? DBNull.Value);
                                    cmdIns.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(r.Phone) ? DBNull.Value : (object)r.Phone.Trim());
                                    cmdIns.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(r.Email) ? DBNull.Value : (object)r.Email.Trim());
                                    cmdIns.Parameters.AddWithValue("@cccd", string.IsNullOrWhiteSpace(r.CCCD) ? DBNull.Value : (object)r.CCCD.Trim());
                                    employeeId = Convert.ToInt32(cmdIns.ExecuteScalar());
                                }
                                inserted++;
                            }

                            // 5. RFID Card Assignment (Optional)
                            if (!string.IsNullOrWhiteSpace(r.CardUID))
                            {
                                var cardUid = r.CardUID.Trim();
                                int loaiXeId = defaultLoaiXeId;
                                int loaiVeId = defaultLoaiVeId;

                                var normXe = ImportExportService.Normalize(r.LoaiXe);
                                if (dictXe.TryGetValue(normXe, out var xeInfo))
                                {
                                    loaiXeId = xeInfo.Id;
                                }

                                var normVe = ImportExportService.Normalize(r.LoaiVe);
                                if (dictVe.TryGetValue(normVe, out var veInfo))
                                {
                                    loaiVeId = veInfo.Id;
                                }

                                using (var cmdDis = new System.Data.SqlClient.SqlCommand(
                                    "UPDATE dbo.RFIDCards SET EmployeeId = NULL WHERE CardUID = @uid AND EmployeeId <> @empId", conn, trans))
                                {
                                    cmdDis.Parameters.AddWithValue("@uid", cardUid);
                                    cmdDis.Parameters.AddWithValue("@empId", employeeId);
                                    cmdDis.ExecuteNonQuery();
                                }

                                using (var cmdDisOld = new System.Data.SqlClient.SqlCommand(
                                    "UPDATE dbo.RFIDCards SET EmployeeId = NULL, TrangThai = 'Inactive' WHERE EmployeeId = @empId AND CardUID <> @uid AND TrangThai = 'Active'", conn, trans))
                                {
                                    cmdDisOld.Parameters.AddWithValue("@empId", employeeId);
                                    cmdDisOld.Parameters.AddWithValue("@uid", cardUid);
                                    cmdDisOld.ExecuteNonQuery();
                                }

                                int cardId = 0;
                                using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT Id FROM dbo.RFIDCards WHERE CardUID = @uid", conn, trans))
                                {
                                    cmd.Parameters.AddWithValue("@uid", cardUid);
                                    var val = cmd.ExecuteScalar();
                                    if (val != null) cardId = Convert.ToInt32(val);
                                }

                                if (cardId > 0)
                                {
                                    using (var cmdUpdCard = new System.Data.SqlClient.SqlCommand(
                                        @"UPDATE dbo.RFIDCards 
                                          SET EmployeeId = @empId, TrangThai = 'Active', BienSo = @bienso, LoaiXeId = @xeId, LoaiVeId = @veId, NgayHetHan = @ngayhh
                                          WHERE Id = @id", conn, trans))
                                    {
                                        cmdUpdCard.Parameters.AddWithValue("@id", cardId);
                                        cmdUpdCard.Parameters.AddWithValue("@empId", employeeId);
                                        cmdUpdCard.Parameters.AddWithValue("@bienso", string.IsNullOrWhiteSpace(r.BienSo) ? DBNull.Value : (object)r.BienSo.Trim());
                                        cmdUpdCard.Parameters.AddWithValue("@xeId", loaiXeId);
                                        cmdUpdCard.Parameters.AddWithValue("@veId", loaiVeId);
                                        cmdUpdCard.Parameters.AddWithValue("@ngayhh", r.NgayHetHan.HasValue ? (object)r.NgayHetHan.Value : DBNull.Value);
                                        cmdUpdCard.ExecuteNonQuery();
                                    }
                                }
                                else
                                {
                                    using (var cmdInsCard = new System.Data.SqlClient.SqlCommand(
                                        @"INSERT INTO dbo.RFIDCards (CardUID, BienSo, CardName, LoaiXeId, LoaiVeId, TrangThai, NgayDangKy, NgayHetHan, EmployeeId)
                                          VALUES (@uid, @bienso, @name, @xeId, @veId, 'Active', @ngaydk, @ngayhh, @empId)", conn, trans))
                                    {
                                        cmdInsCard.Parameters.AddWithValue("@uid", cardUid);
                                        cmdInsCard.Parameters.AddWithValue("@bienso", string.IsNullOrWhiteSpace(r.BienSo) ? DBNull.Value : (object)r.BienSo.Trim());
                                        cmdInsCard.Parameters.AddWithValue("@name", "Thẻ " + r.FullName.Trim());
                                        cmdInsCard.Parameters.AddWithValue("@xeId", loaiXeId);
                                        cmdInsCard.Parameters.AddWithValue("@veId", loaiVeId);
                                        cmdInsCard.Parameters.AddWithValue("@ngaydk", DateTime.Today);
                                        cmdInsCard.Parameters.AddWithValue("@ngayhh", r.NgayHetHan.HasValue ? (object)r.NgayHetHan.Value : DBNull.Value);
                                        cmdInsCard.Parameters.AddWithValue("@empId", employeeId);
                                        cmdInsCard.ExecuteNonQuery();
                                    }
                                }
                            }

                            trans.Commit();
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback();
                            System.Diagnostics.Debug.WriteLine($"Error importing row {r.RowNumber}: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
        }

        private string MakeInitialsCode(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "CTY";
            var normalized = ImportExportService.RemoveDiacritics(name);
            var words = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var w in words)
            {
                if (w.Length > 0 && char.IsLetterOrDigit(w[0]))
                {
                    sb.Append(char.ToUpperInvariant(w[0]));
                }
            }
            var code = sb.ToString();
            return string.IsNullOrWhiteSpace(code) ? "CTY" : code;
        }
    }
}
