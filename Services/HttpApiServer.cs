using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public class HttpApiServer
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly EnterpriseCrudService _service = new EnterpriseCrudService();
        private int _port = 5050;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenAsync(_cts.Token));
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("API_SERVER", "Stop", "Error stopping API server", ex);
            }
        }

        private async Task ListenAsync(CancellationToken ct)
        {
            // Bind to port 5050. If taken, try subsequent ports
            bool bound = false;
            while (!bound && _port < 5060)
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://localhost:{_port}/");
                    _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                    _listener.Start();
                    bound = true;
                    LoggingService.Instance.LogInfo("API_SERVER", "Start", $"HTTP API Server started on http://localhost:{_port}/");
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogWarning("API_SERVER", "Start", $"Failed to bind to port {_port}: {ex.Message}. Trying next port...");
                    _port++;
                }
            }

            if (!bound)
            {
                LoggingService.Instance.LogError("API_SERVER", "Start", "Failed to start HTTP API Server on any port.");
                return;
            }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener!.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), ct);
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                    break;
                }
                catch (Exception ex)
                {
                    LoggingService.Instance.LogError("API_SERVER", "Listen", "Error accepting request", ex);
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // CORS headers
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            string rawPath = request.Url?.AbsolutePath ?? "";
            string method = request.HttpMethod.ToUpper();

            try
            {
                object? result = null;
                int statusCode = 200;

                // Match API routes
                if (method == "GET" && rawPath == "/api/companies")
                {
                    var query = ParseQueryString(request.Url?.Query);
                    string search = query.GetValueOrDefault("search", "");
                    string status = query.GetValueOrDefault("status", "All");
                    int page = int.TryParse(query.GetValueOrDefault("page", "1"), out int p) ? p - 1 : 0;
                    if (page < 0) page = 0;
                    int limit = int.TryParse(query.GetValueOrDefault("limit", "10"), out int l) ? l : 10;
                    if (limit <= 0) limit = 10;

                    var list = _service.GetCompaniesPaged(search, status, page, limit, out int total);
                    result = new { items = list, totalCount = total, pageIndex = page, pageSize = limit };
                }
                else if (method == "GET" && Regex.IsMatch(rawPath, @"^/api/companies/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    var item = _service.GetCompanyById(id);
                    if (item == null)
                    {
                        statusCode = 404;
                        result = new { message = "Company not found" };
                    }
                    else
                    {
                        var depts = _service.GetDepartments(id);
                        result = new { company = item, departments = depts };
                    }
                }
                else if (method == "POST" && rawPath == "/api/companies")
                {
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Company>(body);
                    if (item == null || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name))
                    {
                        statusCode = 400;
                        result = new { message = "Code and Name are required" };
                    }
                    else if (_service.ValidateDuplicateCompanyCode(item.Code))
                    {
                        statusCode = 400;
                        result = new { message = "Duplicate Company Code" };
                    }
                    else
                    {
                        int id = _service.InsertCompany(item);
                        item.Id = id;
                        result = item;
                    }
                }
                else if (method == "PUT" && Regex.IsMatch(rawPath, @"^/api/companies/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Company>(body);
                    if (item == null || string.IsNullOrWhiteSpace(item.Code) || string.IsNullOrWhiteSpace(item.Name))
                    {
                        statusCode = 400;
                        result = new { message = "Code and Name are required" };
                    }
                    else if (_service.ValidateDuplicateCompanyCode(item.Code, id))
                    {
                        statusCode = 400;
                        result = new { message = "Duplicate Company Code" };
                    }
                    else
                    {
                        item.Id = id;
                        _service.UpdateCompany(item);
                        result = item;
                    }
                }
                else if (method == "DELETE" && Regex.IsMatch(rawPath, @"^/api/companies/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    if (_service.DeleteCompany(id, out string errMsg))
                    {
                        result = new { message = "Company deleted successfully (soft delete)" };
                    }
                    else
                    {
                        statusCode = 400;
                        result = new { message = errMsg };
                    }
                }
                else if (method == "GET" && rawPath == "/api/departments")
                {
                    var query = ParseQueryString(request.Url?.Query);
                    int? companyId = null;
                    if (query.TryGetValue("companyId", out string? cIdStr) && int.TryParse(cIdStr, out int cId))
                        companyId = cId;

                    result = _service.GetDepartments(companyId);
                }
                else if (method == "POST" && rawPath == "/api/departments")
                {
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Department>(body);
                    if (item == null || item.CompanyId <= 0 || string.IsNullOrWhiteSpace(item.DepartmentName))
                    {
                        statusCode = 400;
                        result = new { message = "CompanyId and DepartmentName are required" };
                    }
                    else
                    {
                        int id = _service.InsertDepartment(item);
                        item.Id = id;
                        result = item;
                    }
                }
                else if (method == "PUT" && Regex.IsMatch(rawPath, @"^/api/departments/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Department>(body);
                    if (item == null || item.CompanyId <= 0 || string.IsNullOrWhiteSpace(item.DepartmentName))
                    {
                        statusCode = 400;
                        result = new { message = "CompanyId and DepartmentName are required" };
                    }
                    else
                    {
                        item.Id = id;
                        _service.UpdateDepartment(item);
                        result = item;
                    }
                }
                else if (method == "DELETE" && Regex.IsMatch(rawPath, @"^/api/departments/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    if (_service.DeleteDepartment(id, out string errMsg))
                    {
                        result = new { message = "Department deleted successfully (soft delete)" };
                    }
                    else
                    {
                        statusCode = 400;
                        result = new { message = errMsg };
                    }
                }
                else if (method == "GET" && rawPath == "/api/positions")
                {
                    result = _service.GetPositions();
                }
                else if (method == "POST" && rawPath == "/api/positions")
                {
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Position>(body);
                    if (item == null || string.IsNullOrWhiteSpace(item.PositionName))
                    {
                        statusCode = 400;
                        result = new { message = "PositionName is required" };
                    }
                    else
                    {
                        int id = _service.InsertPosition(item);
                        item.Id = id;
                        result = item;
                    }
                }
                else if (method == "PUT" && Regex.IsMatch(rawPath, @"^/api/positions/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Position>(body);
                    if (item == null || string.IsNullOrWhiteSpace(item.PositionName))
                    {
                        statusCode = 400;
                        result = new { message = "PositionName is required" };
                    }
                    else
                    {
                        item.Id = id;
                        _service.UpdatePosition(item);
                        result = item;
                    }
                }
                else if (method == "DELETE" && Regex.IsMatch(rawPath, @"^/api/positions/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    if (_service.DeletePosition(id, out string errMsg))
                    {
                        result = new { message = "Position deleted successfully (soft delete)" };
                    }
                    else
                    {
                        statusCode = 400;
                        result = new { message = errMsg };
                    }
                }
                else if (method == "GET" && rawPath == "/api/employees")
                {
                    var query = ParseQueryString(request.Url?.Query);
                    string search = query.GetValueOrDefault("search", "");
                    string status = query.GetValueOrDefault("status", "All");
                    int? companyId = int.TryParse(query.GetValueOrDefault("companyId", ""), out int cid) ? (int?)cid : null;
                    int? departmentId = int.TryParse(query.GetValueOrDefault("departmentId", ""), out int did) ? (int?)did : null;
                    int? positionId = int.TryParse(query.GetValueOrDefault("positionId", ""), out int pid) ? (int?)pid : null;
                    int page = int.TryParse(query.GetValueOrDefault("page", "1"), out int p) ? p - 1 : 0;
                    if (page < 0) page = 0;
                    int limit = int.TryParse(query.GetValueOrDefault("limit", "10"), out int l) ? l : 10;
                    if (limit <= 0) limit = 10;

                    var list = _service.GetEmployeesPaged(search, companyId, departmentId, positionId, status, page, limit, out int total);
                    result = new { items = list, totalCount = total, pageIndex = page, pageSize = limit };
                }
                else if (method == "GET" && Regex.IsMatch(rawPath, @"^/api/employees/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    var item = _service.GetEmployeeById(id);
                    if (item == null)
                    {
                        statusCode = 404;
                        result = new { message = "Employee not found" };
                    }
                    else
                    {
                        result = item;
                    }
                }
                else if (method == "POST" && rawPath == "/api/employees")
                {
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Employee>(body);
                    if (item == null || string.IsNullOrWhiteSpace(item.EmployeeCode) || string.IsNullOrWhiteSpace(item.FullName) || item.CompanyId <= 0 || item.PositionId <= 0)
                    {
                        statusCode = 400;
                        result = new { message = "EmployeeCode, FullName, CompanyId, and PositionId are required" };
                    }
                    else if (_service.ValidateDuplicateEmployeeCode(item.EmployeeCode))
                    {
                        statusCode = 400;
                        result = new { message = "Duplicate Employee Code" };
                    }
                    else
                    {
                        int id = _service.InsertEmployee(item);
                        item.Id = id;
                        result = item;
                    }
                }
                else if (method == "PUT" && Regex.IsMatch(rawPath, @"^/api/employees/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    string body = await ReadBodyAsync(request);
                    var item = JsonConvert.DeserializeObject<Employee>(body);
                    if (item == null || string.IsNullOrWhiteSpace(item.EmployeeCode) || string.IsNullOrWhiteSpace(item.FullName) || item.CompanyId <= 0 || item.PositionId <= 0)
                    {
                        statusCode = 400;
                        result = new { message = "EmployeeCode, FullName, CompanyId, and PositionId are required" };
                    }
                    else if (_service.ValidateDuplicateEmployeeCode(item.EmployeeCode, id))
                    {
                        statusCode = 400;
                        result = new { message = "Duplicate Employee Code" };
                    }
                    else
                    {
                        item.Id = id;
                        _service.UpdateEmployee(item);
                        result = item;
                    }
                }
                else if (method == "DELETE" && Regex.IsMatch(rawPath, @"^/api/employees/\d+$"))
                {
                    int id = int.Parse(rawPath.Split('/')[3]);
                    _service.DeleteEmployee(id);
                    result = new { message = "Employee deleted successfully (soft delete) and linked cards disabled" };
                }
                else if (method == "POST" && Regex.IsMatch(rawPath, @"^/api/rfid/[^/]+/assign/\d+$"))
                {
                    var segments = rawPath.Split('/');
                    string cardId = Uri.UnescapeDataString(segments[3]);
                    int employeeId = int.Parse(segments[5]);

                    _service.AssignRFIDCard(cardId, employeeId);
                    result = new { message = $"Card {cardId} assigned to employee {employeeId} successfully" };
                }
                else if (method == "DELETE" && Regex.IsMatch(rawPath, @"^/api/rfid/[^/]+/employee$"))
                {
                    var segments = rawPath.Split('/');
                    string cardId = Uri.UnescapeDataString(segments[3]);

                    _service.RemoveRFIDCard(cardId);
                    result = new { message = $"Card {cardId} unassigned from employee successfully" };
                }
                else
                {
                    statusCode = 404;
                    result = new { message = "API Route Not Found" };
                }

                // Send response
                response.StatusCode = statusCode;
                response.ContentType = "application/json";
                string json = JsonConvert.SerializeObject(result, Formatting.Indented);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("API_SERVER", "HandleRequest", $"Error processing path {rawPath}", ex);
                response.StatusCode = 500;
                response.ContentType = "application/json";
                string errJson = JsonConvert.SerializeObject(new { message = "Internal Server Error", details = ex.Message });
                byte[] buffer = Encoding.UTF8.GetBytes(errJson);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                response.Close();
            }
        }

        private async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                return await reader.ReadToEndAsync();
            }
        }

        private Dictionary<string, string> ParseQueryString(string? query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return dict;

            string cleaned = query.StartsWith("?") ? query.Substring(1) : query;
            string[] pairs = cleaned.Split('&');

            foreach (var p in pairs)
            {
                string[] parts = p.Split('=');
                if (parts.Length == 2)
                {
                    dict[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                }
                else if (parts.Length == 1)
                {
                    dict[Uri.UnescapeDataString(parts[0])] = "";
                }
            }
            return dict;
        }
    }
}
