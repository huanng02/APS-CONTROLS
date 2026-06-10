using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Tests
{
    public static class EnterpriseCrudTests
    {
        public static void Run()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("RUNNING ENTERPRISE CRUD INTEGRATION TESTS");
            Console.WriteLine("=================================================");

            // 1. Setup connection & run schema migrations
            try
            {
                using (var conn = ConnectionManager.Instance.GetOpenConnection())
                {
                    // Connection successful, we can run migrations and tests
                }
                DatabaseService.EnsureMigrationsAppliedAsync().Wait();
                Console.WriteLine("Database migrations validated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ WARNING: Could not connect to SQL Server. Skipping database integration tests.");
                Console.WriteLine("Details: " + ex.Message);
                Console.WriteLine("=================================================");
                Console.WriteLine("ENTERPRISE CRUD INTEGRATION TESTS SKIPPED (NO DB)");
                Console.WriteLine("=================================================");
                return;
            }

            // 2. Clean up any previous test runs
            CleanUpTestData();
            Console.WriteLine("Cleanup of test data complete.");

            var service = new EnterpriseCrudService();

            // 3. Test Company CRUD
            Console.WriteLine("Testing Company CRUD...");
            var comp = new Company
            {
                Code = "TEST_CMP_1",
                Name = "TEST Company One",
                Address = "123 Main Street",
                Phone = "555-0101",
                Status = "Active"
            };
            int compId = service.InsertCompany(comp);
            Assert(compId > 0, "InsertCompany should return valid Id");
            comp.Id = compId;

            // Validate Duplicate
            bool isDuplicate = service.ValidateDuplicateCompanyCode("TEST_CMP_1");
            Assert(isDuplicate, "Company code TEST_CMP_1 should be recognized as duplicate");

            // Read
            var fetchedComp = service.GetCompanyById(compId);
            Assert(fetchedComp != null, "GetCompanyById returned null");
            Assert(fetchedComp.Name == "TEST Company One", "Fetched company name mismatch");

            // Update
            comp.Name = "TEST Company One Updated";
            service.UpdateCompany(comp);
            fetchedComp = service.GetCompanyById(compId);
            Assert(fetchedComp.Name == "TEST Company One Updated", "UpdateCompany failed");

            // 4. Test Department CRUD
            Console.WriteLine("Testing Department CRUD...");
            var dept = new Department
            {
                CompanyId = compId,
                DepartmentName = "TEST Dept One",
                Status = "Active"
            };
            int deptId = service.InsertDepartment(dept);
            Assert(deptId > 0, "InsertDepartment should return valid Id");
            dept.Id = deptId;

            var fetchedDept = service.GetDepartmentById(deptId);
            Assert(fetchedDept != null, "GetDepartmentById returned null");
            Assert(fetchedDept.DepartmentName == "TEST Dept One", "Fetched department name mismatch");

            dept.DepartmentName = "TEST Dept One Updated";
            service.UpdateDepartment(dept);
            fetchedDept = service.GetDepartmentById(deptId);
            Assert(fetchedDept.DepartmentName == "TEST Dept One Updated", "UpdateDepartment failed");

            // 5. Test Position CRUD
            Console.WriteLine("Testing Position CRUD...");
            var pos = new Position
            {
                PositionName = "TEST Position One",
                Status = "Active"
            };
            int posId = service.InsertPosition(pos);
            Assert(posId > 0, "InsertPosition should return valid Id");
            pos.Id = posId;

            var fetchedPos = service.GetPositionById(posId);
            Assert(fetchedPos != null, "GetPositionById returned null");
            Assert(fetchedPos.PositionName == "TEST Position One", "Fetched position name mismatch");

            pos.PositionName = "TEST Position One Updated";
            service.UpdatePosition(pos);
            fetchedPos = service.GetPositionById(posId);
            Assert(fetchedPos.PositionName == "TEST Position One Updated", "UpdatePosition failed");

            // 6. Test Employee CRUD
            Console.WriteLine("Testing Employee CRUD...");
            var emp = new Employee
            {
                EmployeeCode = "TEST_EMP_1",
                FullName = "TEST Employee One",
                CompanyId = compId,
                PositionId = posId,
                DepartmentId = deptId,
                Phone = "555-0202",
                Email = "test1@example.com",
                CCCD = "123456789",
                Status = "Active"
            };
            int empId = service.InsertEmployee(emp);
            Assert(empId > 0, "InsertEmployee should return valid Id");
            emp.Id = empId;

            // Duplicate code validation
            bool isEmpDup = service.ValidateDuplicateEmployeeCode("TEST_EMP_1");
            Assert(isEmpDup, "Employee code TEST_EMP_1 should be duplicate");

            var fetchedEmp = service.GetEmployeeById(empId);
            Assert(fetchedEmp != null, "GetEmployeeById returned null");
            Assert(fetchedEmp.FullName == "TEST Employee One", "Fetched employee name mismatch");

            // 7. Test RFID Card Assignment & Transfer
            Console.WriteLine("Testing RFID Card Assignment & Transfer...");
            service.AssignRFIDCard("TEST_CARD_123", empId);

            var card = service.GetRFIDCardByUidOrId("TEST_CARD_123");
            Assert(card != null, "Assigned card was not found/created");
            Assert(card.EmployeeId == empId, "Card was not linked to Employee 1");
            Assert(card.TrangThai == "Active", "Card status is not Active");

            fetchedEmp = service.GetEmployeeById(empId);
            Assert(fetchedEmp.CardUID == "TEST_CARD_123", "Employee details should include linked CardUID");

            // Transfer to Employee 2
            var emp2 = new Employee
            {
                EmployeeCode = "TEST_EMP_2",
                FullName = "TEST Employee Two",
                CompanyId = compId,
                PositionId = posId,
                DepartmentId = deptId,
                Status = "Active"
            };
            int emp2Id = service.InsertEmployee(emp2);
            emp2.Id = emp2Id;

            service.AssignRFIDCard("TEST_CARD_123", emp2Id);

            card = service.GetRFIDCardByUidOrId("TEST_CARD_123");
            Assert(card.EmployeeId == emp2Id, "Card was not transferred to Employee 2");

            fetchedEmp = service.GetEmployeeById(empId);
            Assert(string.IsNullOrEmpty(fetchedEmp.CardUID), "Card should be unlinked from Employee 1");

            // 8. Test Constraints & Soft Delete
            Console.WriteLine("Testing Delete Constraints...");

            // Company delete should fail
            bool compDelResult = service.DeleteCompany(compId, out string compErr);
            Assert(!compDelResult, "Should prevent company delete if employees/departments exist");
            Assert(compErr.Contains("Cannot delete company"), "Error message mismatch on company delete");

            // Department delete should fail
            bool deptDelResult = service.DeleteDepartment(deptId, out string deptErr);
            Assert(!deptDelResult, "Should prevent department delete if employees exist");

            // Position delete should fail
            bool posDelResult = service.DeletePosition(posId, out string posErr);
            Assert(!posDelResult, "Should prevent position delete if employees assigned");

            // Soft delete Employee 2
            Console.WriteLine("Testing Employee Soft Delete & Card Auto-Deactivation...");
            service.DeleteEmployee(emp2Id);

            var deletedEmp = service.GetEmployeeById(emp2Id);
            Assert(deletedEmp == null, "Soft deleted employee should not be fetchable via GetEmployeeById");

            // Verify in DB directly
            using (var conn = ConnectionManager.Instance.GetOpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT IsDeleted FROM dbo.Employees WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", emp2Id);
                bool isDeleted = (bool)cmd.ExecuteScalar();
                Assert(isDeleted, "IsDeleted field in DB should be true");
            }

            // Verify card is deactivated
            card = service.GetRFIDCardByUidOrId("TEST_CARD_123");
            Assert(card.TrangThai == "Inactive", "Card linked to soft-deleted employee was not disabled");

            // Cleanup & delete organizational entities
            emp.DepartmentId = null;
            emp.PositionId = 1; // Default
            service.UpdateEmployee(emp);

            bool deptDelSuccess = service.DeleteDepartment(deptId, out string deptErr2);
            Assert(deptDelSuccess, $"Should delete department now: {deptErr2}");

            bool posDelSuccess = service.DeletePosition(posId, out string posErr2);
            Assert(posDelSuccess, $"Should delete position now: {posErr2}");

            service.DeleteEmployee(empId);

            bool compDelSuccess = service.DeleteCompany(compId, out string compErr2);
            Assert(compDelSuccess, $"Should delete company now: {compErr2}");

            // 9. Test HTTP API Server
            Console.WriteLine("Testing HTTP API Server...");
            var apiServer = new HttpApiServer();
            try
            {
                apiServer.Start();
                Task.Delay(1000).Wait(); // Wait for server to bind

                using (var http = new HttpClient())
                {
                    // 1. List positions
                    var res = http.GetAsync("http://localhost:5050/api/positions").Result;
                    Assert(res.IsSuccessStatusCode, "GET /api/positions failed");
                    string json = res.Content.ReadAsStringAsync().Result;
                    Assert(json.Contains("Director") || json.Contains("Visitor"), "GET /api/positions output should contain seeded positions");

                    // 2. Create company via API
                    var newCompObj = new { Code = "TEST_API_CMP", Name = "TEST API Company", Status = "Active" };
                    var content = new StringContent(JsonConvert.SerializeObject(newCompObj), Encoding.UTF8, "application/json");
                    var postRes = http.PostAsync("http://localhost:5050/api/companies", content).Result;
                    Assert(postRes.IsSuccessStatusCode, "POST /api/companies failed");
                    string postJson = postRes.Content.ReadAsStringAsync().Result;
                    var postResult = JsonConvert.DeserializeAnonymousType(postJson, new { Id = 0, Code = "" });
                    Assert(postResult != null && postResult.Id > 0, "POST /api/companies should return created company with ID");

                    // 3. Duplicate code check via API
                    var postResDup = http.PostAsync("http://localhost:5050/api/companies", content).Result;
                    Assert(postResDup.StatusCode == System.Net.HttpStatusCode.BadRequest, "POST /api/companies should fail with 400 on duplicate code");
                    string dupJson = postResDup.Content.ReadAsStringAsync().Result;
                    Assert(dupJson.Contains("Duplicate Company Code"), "Error body mismatch on duplicate code via API");

                    // Cleanup the API company
                    if (postResult != null && postResult.Id > 0)
                    {
                        var delRes = http.DeleteAsync($"http://localhost:5050/api/companies/{postResult.Id}").Result;
                        Assert(delRes.IsSuccessStatusCode, "DELETE /api/companies/{id} failed");
                    }
                }
            }
            finally
            {
                apiServer.Stop();
            }

            // Cleanup any residual test data
            CleanUpTestData();

            Console.WriteLine("=================================================");
            Console.WriteLine("ALL ENTERPRISE CRUD INTEGRATION TESTS PASSED!");
            Console.WriteLine("=================================================");
        }

        private static void CleanUpTestData()
        {
            using (var conn = ConnectionManager.Instance.GetOpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                // De-associate test employees from cards
                cmd.CommandText = "UPDATE dbo.RFIDCards SET EmployeeId = NULL WHERE EmployeeId IN (SELECT Id FROM dbo.Employees WHERE EmployeeCode LIKE 'TEST_%')";
                cmd.ExecuteNonQuery();

                // Delete test cards
                cmd.CommandText = "DELETE FROM dbo.RFIDCards WHERE CardUID LIKE 'TEST_CARD%'";
                cmd.ExecuteNonQuery();

                // Delete test employees
                cmd.CommandText = "DELETE FROM dbo.Employees WHERE EmployeeCode LIKE 'TEST_%' OR EmployeeCode = 'TEST_API_CMP'";
                cmd.ExecuteNonQuery();

                // Delete test departments
                cmd.CommandText = "DELETE FROM dbo.Departments WHERE DepartmentName LIKE 'TEST_%'";
                cmd.ExecuteNonQuery();

                // Delete test positions
                cmd.CommandText = "DELETE FROM dbo.Positions WHERE PositionName LIKE 'TEST_%'";
                cmd.ExecuteNonQuery();

                // Delete test companies
                cmd.CommandText = "DELETE FROM dbo.Companies WHERE Code LIKE 'TEST_%' OR Code = 'TEST_API_CMP'";
                cmd.ExecuteNonQuery();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + message);
            }
        }
    }
}
