using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class EnterpriseCrudService
    {
        private readonly DatabaseService _db = new DatabaseService();

        private string GetConnStr() => _db.GetConnectionString();

        #region Company CRUD

        public List<Company> GetCompaniesPaged(string search, string status, int pageIndex, int pageSize, out int totalCount)
        {
            totalCount = 0;
            var list = new List<Company>();
            string searchPattern = "%" + (search ?? "").Trim() + "%";

            string countSql = "SELECT COUNT(*) FROM dbo.Companies WHERE IsDeleted = 0";
            if (!string.IsNullOrEmpty(search))
                countSql += " AND (Code LIKE @search OR Name LIKE @search)";
            if (!string.IsNullOrEmpty(status) && status != "All")
                countSql += " AND Status = @status";

            string dataSql = @"
                SELECT Id, Code, Name, Address, Phone, Status, IsDeleted 
                FROM dbo.Companies 
                WHERE IsDeleted = 0";

            if (!string.IsNullOrEmpty(search))
                dataSql += " AND (Code LIKE @search OR Name LIKE @search)";
            if (!string.IsNullOrEmpty(status) && status != "All")
                dataSql += " AND Status = @status";

            dataSql += " ORDER BY Code OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();

                // 1. Get count
                using (var cmd = new SqlCommand(countSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@search", searchPattern);
                    if (!string.IsNullOrEmpty(status) && status != "All")
                        cmd.Parameters.AddWithValue("@status", status);
                    totalCount = (int)cmd.ExecuteScalar();
                }

                // 2. Get paged data
                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    if (!string.IsNullOrEmpty(search))
                        cmd.Parameters.AddWithValue("@search", searchPattern);
                    if (!string.IsNullOrEmpty(status) && status != "All")
                        cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@offset", pageIndex * pageSize);
                    cmd.Parameters.AddWithValue("@limit", pageSize);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new Company
                            {
                                Id = (int)r["Id"],
                                Code = r["Code"].ToString() ?? "",
                                Name = r["Name"].ToString() ?? "",
                                Address = r["Address"] == DBNull.Value ? null : r["Address"].ToString(),
                                Phone = r["Phone"] == DBNull.Value ? null : r["Phone"].ToString(),
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"]
                            });
                        }
                    }
                }
            }
            return list;
        }

        public List<Company> GetAllCompanies()
        {
            var list = new List<Company>();
            string sql = "SELECT * FROM dbo.Companies WHERE IsDeleted = 0 AND Status = 'Active' ORDER BY Name";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Company
                        {
                            Id = (int)r["Id"],
                            Code = r["Code"].ToString() ?? "",
                            Name = r["Name"].ToString() ?? "",
                            Address = r["Address"] == DBNull.Value ? null : r["Address"].ToString(),
                            Phone = r["Phone"] == DBNull.Value ? null : r["Phone"].ToString(),
                            Status = r["Status"].ToString() ?? "Active"
                        });
                    }
                }
            }
            return list;
        }

        public Company? GetCompanyById(int id)
        {
            Company? c = null;
            string sql = "SELECT * FROM dbo.Companies WHERE Id = @id AND IsDeleted = 0";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            c = new Company
                            {
                                Id = (int)r["Id"],
                                Code = r["Code"].ToString() ?? "",
                                Name = r["Name"].ToString() ?? "",
                                Address = r["Address"] == DBNull.Value ? null : r["Address"].ToString(),
                                Phone = r["Phone"] == DBNull.Value ? null : r["Phone"].ToString(),
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"]
                            };
                        }
                    }
                }
            }
            return c;
        }

        public bool ValidateDuplicateCompanyCode(string code, int? excludeId = null)
        {
            string sql = "SELECT COUNT(*) FROM dbo.Companies WHERE Code = @code AND IsDeleted = 0";
            if (excludeId.HasValue)
                sql += " AND Id <> @excludeId";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", code.Trim());
                    if (excludeId.HasValue)
                        cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        public int InsertCompany(Company c)
        {
            if (ValidateDuplicateCompanyCode(c.Code))
                throw new InvalidOperationException("Duplicate Company Code.");

            string sql = @"
                INSERT INTO dbo.Companies (Code, Name, Address, Phone, Status, IsDeleted)
                VALUES (@code, @name, @address, @phone, @status, 0);
                SELECT SCOPE_IDENTITY();";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", c.Code.Trim());
                    cmd.Parameters.AddWithValue("@name", c.Name.Trim());
                    cmd.Parameters.AddWithValue("@address", (object?)c.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@phone", (object?)c.Phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", c.Status ?? "Active");

                    var id = cmd.ExecuteScalar();
                    int newId = Convert.ToInt32(id);
                    LoggingService.Instance.LogCrud("CREATE_COMPANY", "Companies", newId.ToString(), null, c, "EnterpriseCrudService");
                    return newId;
                }
            }
        }

        public void UpdateCompany(Company c)
        {
            if (ValidateDuplicateCompanyCode(c.Code, c.Id))
                throw new InvalidOperationException("Duplicate Company Code.");

            string sql = @"
                UPDATE dbo.Companies 
                SET Code = @code, Name = @name, Address = @address, Phone = @phone, Status = @status
                WHERE Id = @id";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", c.Id);
                    cmd.Parameters.AddWithValue("@code", c.Code.Trim());
                    cmd.Parameters.AddWithValue("@name", c.Name.Trim());
                    cmd.Parameters.AddWithValue("@address", (object?)c.Address ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@phone", (object?)c.Phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", c.Status ?? "Active");
                    cmd.ExecuteNonQuery();

                    LoggingService.Instance.LogCrud("UPDATE_COMPANY", "Companies", c.Id.ToString(), null, c, "EnterpriseCrudService");
                }
            }
        }

        public bool DeleteCompany(int id, out string errorMessage)
        {
            errorMessage = "";

            // Constraint check: has active employees?
            string checkEmpSql = "SELECT COUNT(*) FROM dbo.Employees WHERE CompanyId = @id AND IsDeleted = 0";
            // Constraint check: has active cards?
            string checkCardSql = @"
                SELECT COUNT(*) FROM dbo.RFIDCards rc
                JOIN dbo.Employees e ON rc.EmployeeId = e.Id
                WHERE e.CompanyId = @id AND rc.TrangThai = 'Active' AND e.IsDeleted = 0";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(checkEmpSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    if ((int)cmd.ExecuteScalar() > 0)
                    {
                        errorMessage = "Cannot delete company because it contains active employees.";
                        return false;
                    }
                }

                using (var cmd = new SqlCommand(checkCardSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    if ((int)cmd.ExecuteScalar() > 0)
                    {
                        errorMessage = "Cannot delete company because it has active RFID cards linked to its employees.";
                        return false;
                    }
                }

                string sql = "UPDATE dbo.Companies SET IsDeleted = 1 WHERE Id = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    LoggingService.Instance.LogCrud("DELETE_COMPANY", "Companies", id.ToString(), null, null, "EnterpriseCrudService");
                }
            }
            return true;
        }

        #endregion

        #region Department CRUD

        public List<Department> GetDepartments(int? companyId)
        {
            var list = new List<Department>();
            string sql = @"
                SELECT d.Id, d.CompanyId, d.DepartmentName, d.Status, d.IsDeleted, c.Name AS CompanyName
                FROM dbo.Departments d
                JOIN dbo.Companies c ON d.CompanyId = c.Id
                WHERE d.IsDeleted = 0";

            if (companyId.HasValue)
                sql += " AND d.CompanyId = @companyId";

            sql += " ORDER BY c.Name, d.DepartmentName";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (companyId.HasValue)
                        cmd.Parameters.AddWithValue("@companyId", companyId.Value);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new Department
                            {
                                Id = (int)r["Id"],
                                CompanyId = (int)r["CompanyId"],
                                DepartmentName = r["DepartmentName"].ToString() ?? "",
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"],
                                CompanyName = r["CompanyName"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        public Department? GetDepartmentById(int id)
        {
            Department? d = null;
            string sql = @"
                SELECT d.Id, d.CompanyId, d.DepartmentName, d.Status, d.IsDeleted, c.Name AS CompanyName
                FROM dbo.Departments d
                JOIN dbo.Companies c ON d.CompanyId = c.Id
                WHERE d.Id = @id AND d.IsDeleted = 0";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            d = new Department
                            {
                                Id = (int)r["Id"],
                                CompanyId = (int)r["CompanyId"],
                                DepartmentName = r["DepartmentName"].ToString() ?? "",
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"],
                                CompanyName = r["CompanyName"].ToString()
                            };
                        }
                    }
                }
            }
            return d;
        }

        public int InsertDepartment(Department d)
        {
            string sql = @"
                INSERT INTO dbo.Departments (CompanyId, DepartmentName, Status, IsDeleted)
                VALUES (@companyId, @name, @status, 0);
                SELECT SCOPE_IDENTITY();";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@companyId", d.CompanyId);
                    cmd.Parameters.AddWithValue("@name", d.DepartmentName.Trim());
                    cmd.Parameters.AddWithValue("@status", d.Status ?? "Active");

                    var id = cmd.ExecuteScalar();
                    int newId = Convert.ToInt32(id);
                    LoggingService.Instance.LogCrud("CREATE_DEPARTMENT", "Departments", newId.ToString(), null, d, "EnterpriseCrudService");
                    return newId;
                }
            }
        }

        public void UpdateDepartment(Department d)
        {
            string sql = @"
                UPDATE dbo.Departments 
                SET CompanyId = @companyId, DepartmentName = @name, Status = @status
                WHERE Id = @id";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", d.Id);
                    cmd.Parameters.AddWithValue("@companyId", d.CompanyId);
                    cmd.Parameters.AddWithValue("@name", d.DepartmentName.Trim());
                    cmd.Parameters.AddWithValue("@status", d.Status ?? "Active");
                    cmd.ExecuteNonQuery();

                    LoggingService.Instance.LogCrud("UPDATE_DEPARTMENT", "Departments", d.Id.ToString(), null, d, "EnterpriseCrudService");
                }
            }
        }

        public bool DeleteDepartment(int id, out string errorMessage)
        {
            errorMessage = "";

            // Constraint check: has employees?
            string checkEmpSql = "SELECT COUNT(*) FROM dbo.Employees WHERE DepartmentId = @id AND IsDeleted = 0";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(checkEmpSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    if ((int)cmd.ExecuteScalar() > 0)
                    {
                        errorMessage = "Cannot delete department because it contains active employees.";
                        return false;
                    }
                }

                string sql = "UPDATE dbo.Departments SET IsDeleted = 1 WHERE Id = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    LoggingService.Instance.LogCrud("DELETE_DEPARTMENT", "Departments", id.ToString(), null, null, "EnterpriseCrudService");
                }
            }
            return true;
        }

        #endregion

        #region Position CRUD

        public List<Position> GetPositions()
        {
            var list = new List<Position>();
            string sql = "SELECT * FROM dbo.Positions WHERE IsDeleted = 0 ORDER BY PositionName";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Position
                        {
                            Id = (int)r["Id"],
                            PositionName = r["PositionName"].ToString() ?? "",
                            Status = r["Status"].ToString() ?? "Active",
                            IsDeleted = (bool)r["IsDeleted"]
                        });
                    }
                }
            }
            return list;
        }

        public Position? GetPositionById(int id)
        {
            Position? p = null;
            string sql = "SELECT * FROM dbo.Positions WHERE Id = @id AND IsDeleted = 0";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            p = new Position
                            {
                                Id = (int)r["Id"],
                                PositionName = r["PositionName"].ToString() ?? "",
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"]
                            };
                        }
                    }
                }
            }
            return p;
        }

        public int InsertPosition(Position p)
        {
            string sql = @"
                INSERT INTO dbo.Positions (PositionName, Status, IsDeleted)
                VALUES (@name, @status, 0);
                SELECT SCOPE_IDENTITY();";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", p.PositionName.Trim());
                    cmd.Parameters.AddWithValue("@status", p.Status ?? "Active");

                    var id = cmd.ExecuteScalar();
                    int newId = Convert.ToInt32(id);
                    LoggingService.Instance.LogCrud("CREATE_POSITION", "Positions", newId.ToString(), null, p, "EnterpriseCrudService");
                    return newId;
                }
            }
        }

        public void UpdatePosition(Position p)
        {
            string sql = @"
                UPDATE dbo.Positions 
                SET PositionName = @name, Status = @status
                WHERE Id = @id";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", p.Id);
                    cmd.Parameters.AddWithValue("@name", p.PositionName.Trim());
                    cmd.Parameters.AddWithValue("@status", p.Status ?? "Active");
                    cmd.ExecuteNonQuery();

                    LoggingService.Instance.LogCrud("UPDATE_POSITION", "Positions", p.Id.ToString(), null, p, "EnterpriseCrudService");
                }
            }
        }

        public bool DeletePosition(int id, out string errorMessage)
        {
            errorMessage = "";

            // Constraint check: has employees?
            string checkEmpSql = "SELECT COUNT(*) FROM dbo.Employees WHERE PositionId = @id AND IsDeleted = 0";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(checkEmpSql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    if ((int)cmd.ExecuteScalar() > 0)
                    {
                        errorMessage = "Cannot delete position because it is currently assigned to active employees.";
                        return false;
                    }
                }

                string sql = "UPDATE dbo.Positions SET IsDeleted = 1 WHERE Id = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                    LoggingService.Instance.LogCrud("DELETE_POSITION", "Positions", id.ToString(), null, null, "EnterpriseCrudService");
                }
            }
            return true;
        }

        #endregion

        #region Employee CRUD

        public List<Employee> GetEmployeesPaged(string search, int? companyId, int? departmentId, int? positionId, string status, int pageIndex, int pageSize, out int totalCount)
        {
            totalCount = 0;
            var list = new List<Employee>();
            string searchPattern = "%" + (search ?? "").Trim() + "%";

            string whereClause = "WHERE e.IsDeleted = 0";
            if (!string.IsNullOrEmpty(search))
            {
                whereClause += " AND (e.FullName LIKE @search OR e.EmployeeCode LIKE @search OR e.Phone LIKE @search OR c.Name LIKE @search)";
            }
            if (companyId.HasValue)
                whereClause += " AND e.CompanyId = @companyId";
            if (departmentId.HasValue)
                whereClause += " AND e.DepartmentId = @departmentId";
            if (positionId.HasValue)
                whereClause += " AND e.PositionId = @positionId";
            if (!string.IsNullOrEmpty(status) && status != "All")
                whereClause += " AND e.Status = @status";

            string countSql = $@"
                SELECT COUNT(*) 
                FROM dbo.Employees e
                JOIN dbo.Companies c ON e.CompanyId = c.Id
                JOIN dbo.Positions p ON e.PositionId = p.Id
                LEFT JOIN dbo.Departments d ON e.DepartmentId = d.Id
                {whereClause}";

            string dataSql = $@"
                SELECT e.Id, e.EmployeeCode, e.FullName, e.CompanyId, e.PositionId, e.Phone, e.Email, e.CCCD, e.Avatar, e.DepartmentId, e.Status, e.IsDeleted,
                       c.Name AS CompanyName, d.DepartmentName, p.PositionName, 
                       rc.CardUID, rc.TrangThai AS CardStatus, rc.NgayHetHan AS CardExpiration
                FROM dbo.Employees e
                JOIN dbo.Companies c ON e.CompanyId = c.Id
                JOIN dbo.Positions p ON e.PositionId = p.Id
                LEFT JOIN dbo.Departments d ON e.DepartmentId = d.Id
                LEFT JOIN dbo.RFIDCards rc ON rc.EmployeeId = e.Id AND rc.TrangThai = 'Active'
                {whereClause}
                ORDER BY e.EmployeeCode OFFSET @offset ROWS FETCH NEXT @limit ROWS ONLY";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();

                // 1. Get count
                using (var cmd = new SqlCommand(countSql, conn))
                {
                    if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@search", searchPattern);
                    if (companyId.HasValue) cmd.Parameters.AddWithValue("@companyId", companyId.Value);
                    if (departmentId.HasValue) cmd.Parameters.AddWithValue("@departmentId", departmentId.Value);
                    if (positionId.HasValue) cmd.Parameters.AddWithValue("@positionId", positionId.Value);
                    if (!string.IsNullOrEmpty(status) && status != "All") cmd.Parameters.AddWithValue("@status", status);

                    totalCount = (int)cmd.ExecuteScalar();
                }

                // 2. Get paged data
                using (var cmd = new SqlCommand(dataSql, conn))
                {
                    if (!string.IsNullOrEmpty(search)) cmd.Parameters.AddWithValue("@search", searchPattern);
                    if (companyId.HasValue) cmd.Parameters.AddWithValue("@companyId", companyId.Value);
                    if (departmentId.HasValue) cmd.Parameters.AddWithValue("@departmentId", departmentId.Value);
                    if (positionId.HasValue) cmd.Parameters.AddWithValue("@positionId", positionId.Value);
                    if (!string.IsNullOrEmpty(status) && status != "All") cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@offset", pageIndex * pageSize);
                    cmd.Parameters.AddWithValue("@limit", pageSize);

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new Employee
                            {
                                Id = (int)r["Id"],
                                EmployeeCode = r["EmployeeCode"].ToString() ?? "",
                                FullName = r["FullName"].ToString() ?? "",
                                CompanyId = (int)r["CompanyId"],
                                PositionId = (int)r["PositionId"],
                                Phone = r["Phone"] == DBNull.Value ? null : r["Phone"].ToString(),
                                Email = r["Email"] == DBNull.Value ? null : r["Email"].ToString(),
                                CCCD = r["CCCD"] == DBNull.Value ? null : r["CCCD"].ToString(),
                                Avatar = r["Avatar"] == DBNull.Value ? null : r["Avatar"].ToString(),
                                DepartmentId = r["DepartmentId"] == DBNull.Value ? null : (int?)r["DepartmentId"],
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"],
                                CompanyName = r["CompanyName"].ToString(),
                                DepartmentName = r["DepartmentName"] == DBNull.Value ? null : r["DepartmentName"].ToString(),
                                PositionName = r["PositionName"].ToString(),
                                CardUID = r["CardUID"] == DBNull.Value ? null : r["CardUID"].ToString(),
                                CardStatus = r["CardStatus"] == DBNull.Value ? null : r["CardStatus"].ToString(),
                                CardExpiration = r["CardExpiration"] == DBNull.Value ? null : (DateTime?)r["CardExpiration"]
                            });
                        }
                    }
                }
            }
            return list;
        }

        public Employee? GetEmployeeById(int id)
        {
            Employee? e = null;
            string sql = @"
                SELECT e.Id, e.EmployeeCode, e.FullName, e.CompanyId, e.PositionId, e.Phone, e.Email, e.CCCD, e.Avatar, e.DepartmentId, e.Status, e.IsDeleted,
                       c.Name AS CompanyName, d.DepartmentName, p.PositionName, 
                       rc.CardUID, rc.TrangThai AS CardStatus, rc.NgayHetHan AS CardExpiration
                FROM dbo.Employees e
                JOIN dbo.Companies c ON e.CompanyId = c.Id
                JOIN dbo.Positions p ON e.PositionId = p.Id
                LEFT JOIN dbo.Departments d ON e.DepartmentId = d.Id
                LEFT JOIN dbo.RFIDCards rc ON rc.EmployeeId = e.Id AND rc.TrangThai = 'Active'
                WHERE e.Id = @id AND e.IsDeleted = 0";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            e = new Employee
                            {
                                Id = (int)r["Id"],
                                EmployeeCode = r["EmployeeCode"].ToString() ?? "",
                                FullName = r["FullName"].ToString() ?? "",
                                CompanyId = (int)r["CompanyId"],
                                PositionId = (int)r["PositionId"],
                                Phone = r["Phone"] == DBNull.Value ? null : r["Phone"].ToString(),
                                Email = r["Email"] == DBNull.Value ? null : r["Email"].ToString(),
                                CCCD = r["CCCD"] == DBNull.Value ? null : r["CCCD"].ToString(),
                                Avatar = r["Avatar"] == DBNull.Value ? null : r["Avatar"].ToString(),
                                DepartmentId = r["DepartmentId"] == DBNull.Value ? null : (int?)r["DepartmentId"],
                                Status = r["Status"].ToString() ?? "Active",
                                IsDeleted = (bool)r["IsDeleted"],
                                CompanyName = r["CompanyName"].ToString(),
                                DepartmentName = r["DepartmentName"] == DBNull.Value ? null : r["DepartmentName"].ToString(),
                                PositionName = r["PositionName"].ToString(),
                                CardUID = r["CardUID"] == DBNull.Value ? null : r["CardUID"].ToString(),
                                CardStatus = r["CardStatus"] == DBNull.Value ? null : r["CardStatus"].ToString(),
                                CardExpiration = r["CardExpiration"] == DBNull.Value ? null : (DateTime?)r["CardExpiration"]
                            };
                        }
                    }
                }
            }
            return e;
        }

        public bool ValidateDuplicateEmployeeCode(string code, int? excludeId = null)
        {
            string sql = "SELECT COUNT(*) FROM dbo.Employees WHERE EmployeeCode = @code AND IsDeleted = 0";
            if (excludeId.HasValue)
                sql += " AND Id <> @excludeId";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", code.Trim());
                    if (excludeId.HasValue)
                        cmd.Parameters.AddWithValue("@excludeId", excludeId.Value);
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        public int InsertEmployee(Employee e)
        {
            if (ValidateDuplicateEmployeeCode(e.EmployeeCode))
                throw new InvalidOperationException("Duplicate Employee Code.");

            string sql = @"
                INSERT INTO dbo.Employees (EmployeeCode, FullName, CompanyId, PositionId, Phone, Email, CCCD, Avatar, DepartmentId, Status, IsDeleted)
                VALUES (@code, @name, @companyId, @positionId, @phone, @email, @cccd, @avatar, @deptId, @status, 0);
                SELECT SCOPE_IDENTITY();";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@code", e.EmployeeCode.Trim());
                    cmd.Parameters.AddWithValue("@name", e.FullName.Trim());
                    cmd.Parameters.AddWithValue("@companyId", e.CompanyId);
                    cmd.Parameters.AddWithValue("@positionId", e.PositionId);
                    cmd.Parameters.AddWithValue("@phone", (object?)e.Phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@email", (object?)e.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cccd", (object?)e.CCCD ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@avatar", (object?)e.Avatar ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@deptId", (object?)e.DepartmentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", e.Status ?? "Active");

                    var id = cmd.ExecuteScalar();
                    int newId = Convert.ToInt32(id);
                    LoggingService.Instance.LogCrud("CREATE_EMPLOYEE", "Employees", newId.ToString(), null, e, "EnterpriseCrudService");
                    return newId;
                }
            }
        }

        public void UpdateEmployee(Employee e)
        {
            if (ValidateDuplicateEmployeeCode(e.EmployeeCode, e.Id))
                throw new InvalidOperationException("Duplicate Employee Code.");

            string sql = @"
                UPDATE dbo.Employees 
                SET EmployeeCode = @code, FullName = @name, CompanyId = @companyId, PositionId = @positionId, 
                    Phone = @phone, Email = @email, CCCD = @cccd, Avatar = @avatar, DepartmentId = @deptId, Status = @status
                WHERE Id = @id";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", e.Id);
                    cmd.Parameters.AddWithValue("@code", e.EmployeeCode.Trim());
                    cmd.Parameters.AddWithValue("@name", e.FullName.Trim());
                    cmd.Parameters.AddWithValue("@companyId", e.CompanyId);
                    cmd.Parameters.AddWithValue("@positionId", e.PositionId);
                    cmd.Parameters.AddWithValue("@phone", (object?)e.Phone ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@email", (object?)e.Email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cccd", (object?)e.CCCD ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@avatar", (object?)e.Avatar ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@deptId", (object?)e.DepartmentId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", e.Status ?? "Active");
                    cmd.ExecuteNonQuery();

                    LoggingService.Instance.LogCrud("UPDATE_EMPLOYEE", "Employees", e.Id.ToString(), null, e, "EnterpriseCrudService");
                }
            }
        }

        public void DeleteEmployee(int id)
        {
            // Soft delete employee & auto-disable linked RFID cards
            string softDeleteEmpSql = "UPDATE dbo.Employees SET IsDeleted = 1 WHERE Id = @id";
            string disableCardsSql = "UPDATE dbo.RFIDCards SET TrangThai = 'Inactive' WHERE EmployeeId = @id";

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand(softDeleteEmpSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = new SqlCommand(disableCardsSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        LoggingService.Instance.LogCrud("DELETE_EMPLOYEE", "Employees", id.ToString(), null, null, "EnterpriseCrudService");
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        #endregion

        #region RFID Card Assignment CRUD

        public RFIDCard? GetRFIDCardByUidOrId(string uidOrId)
        {
            RFIDCard? card = null;
            string sql = @"
                SELECT Id, CardUID, BienSo, CardName, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy, NgayHetHan, GroupId, EmployeeId
                FROM dbo.RFIDCards 
                WHERE CardUID = @val";

            int parsedId;
            if (int.TryParse(uidOrId, out parsedId))
            {
                sql += " OR Id = @idVal";
            }

            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@val", uidOrId);
                    if (int.TryParse(uidOrId, out parsedId))
                        cmd.Parameters.AddWithValue("@idVal", parsedId);

                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            card = new RFIDCard
                            {
                                Id = (int)r["Id"],
                                UID = r["CardUID"].ToString() ?? "",
                                BienSo = r["BienSo"] == DBNull.Value ? "" : r["BienSo"].ToString() ?? "",
                                CardName = r["CardName"] == DBNull.Value ? "" : r["CardName"].ToString() ?? "",
                                LoaiVeId = (int)r["LoaiVeId"],
                                LoaiXeId = (int)r["LoaiXeId"],
                                TrangThai = r["TrangThai"].ToString() ?? "Active",
                                NgayTao = (DateTime)r["NgayDangKy"],
                                NgayHetHan = r["NgayHetHan"] == DBNull.Value ? null : (DateTime?)r["NgayHetHan"],
                                GroupId = r["GroupId"] == DBNull.Value ? null : (int?)r["GroupId"],
                                EmployeeId = r["EmployeeId"] == DBNull.Value ? null : (int?)r["EmployeeId"]
                            };
                            // Add EmployeeId via reflection or a custom dynamic property if needed, but we can do a secondary lookup or query.
                        }
                    }
                }
            }
            return card;
        }

        public void AssignRFIDCard(string cardUidOrId, int employeeId)
        {
            // 1. Deactivate any existing active cards for this employee to enforce 'One active card belongs to one employee'
            string deactivateOldCardsSql = "UPDATE dbo.RFIDCards SET EmployeeId = NULL, TrangThai = 'Inactive' WHERE EmployeeId = @empId AND TrangThai = 'Active'";

            // 2. Assign the new card. If it does not exist, auto-create it first!
            RFIDCard? card = GetRFIDCardByUidOrId(cardUidOrId);
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var trans = conn.BeginTransaction())
                {
                    try
                    {
                        // Deactivate old active card of employee
                        using (var cmd = new SqlCommand(deactivateOldCardsSql, conn, trans))
                        {
                            cmd.Parameters.AddWithValue("@empId", employeeId);
                            cmd.ExecuteNonQuery();
                        }

                        if (card == null)
                        {
                            // Auto-create card. Resolve default ticket type (LoaiVe) & vehicle type (LoaiXe)
                            int defaultLoaiVeId = 1;
                            int defaultLoaiXeId = 1;

                            using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM dbo.LoaiVe WHERE TrangThai='Active'", conn, trans))
                            {
                                var val = cmd.ExecuteScalar();
                                if (val != null) defaultLoaiVeId = Convert.ToInt32(val);
                            }
                            using (var cmd = new SqlCommand("SELECT TOP 1 Id FROM dbo.LoaiXe WHERE TrangThai='Active'", conn, trans))
                            {
                                var val = cmd.ExecuteScalar();
                                if (val != null) defaultLoaiXeId = Convert.ToInt32(val);
                            }

                            string insertCardSql = @"
                                INSERT INTO dbo.RFIDCards (CardUID, LoaiVeId, LoaiXeId, TrangThai, NgayDangKy, NgayHetHan, EmployeeId, CardName)
                                VALUES (@uid, @veId, @xeId, 'Active', GETUTCDATE(), DATEADD(year, 1, GETUTCDATE()), @empId, @cardName);
                                SELECT SCOPE_IDENTITY();";

                            using (var cmd = new SqlCommand(insertCardSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@uid", cardUidOrId.Trim());
                                cmd.Parameters.AddWithValue("@veId", defaultLoaiVeId);
                                cmd.Parameters.AddWithValue("@xeId", defaultLoaiXeId);
                                cmd.Parameters.AddWithValue("@empId", employeeId);
                                cmd.Parameters.AddWithValue("@cardName", "Employee Card " + cardUidOrId.Trim());
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Assign existing card
                            string assignSql = "UPDATE dbo.RFIDCards SET EmployeeId = @empId, TrangThai = 'Active' WHERE Id = @cardId";
                            using (var cmd = new SqlCommand(assignSql, conn, trans))
                            {
                                cmd.Parameters.AddWithValue("@empId", employeeId);
                                cmd.Parameters.AddWithValue("@cardId", card.Id);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        trans.Commit();
                        LoggingService.Instance.LogInfo("RFID_ASSIGN", "Assign", $"Assigned card {cardUidOrId} to employee ID {employeeId}");
                    }
                    catch
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public void RemoveRFIDCard(string cardUidOrId)
        {
            RFIDCard? card = GetRFIDCardByUidOrId(cardUidOrId);
            if (card == null) return;

            string sql = "UPDATE dbo.RFIDCards SET EmployeeId = NULL, TrangThai = 'Inactive' WHERE Id = @cardId";
            using (var conn = new SqlConnection(GetConnStr()))
            {
                conn.Open();
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@cardId", card.Id);
                    cmd.ExecuteNonQuery();
                    LoggingService.Instance.LogInfo("RFID_DEASSIGN", "Deassign", $"Removed assignment of card {cardUidOrId}");
                }
            }
        }

        public void TransferRFIDCard(string cardUidOrId, int employeeId)
        {
            // Transfering is equivalent to assigning the card to employeeId.
            // AssignRFIDCard already handles deactivating old cards of the target employee.
            AssignRFIDCard(cardUidOrId, employeeId);
        }

        #endregion
    }
}
