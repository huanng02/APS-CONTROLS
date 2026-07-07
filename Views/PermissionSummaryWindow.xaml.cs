using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class PermissionSummaryWindow : Window
    {
        public PermissionSummaryWindow()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            string connStr = ConnectionManager.Instance.CurrentConnectionString;
            if (string.IsNullOrWhiteSpace(connStr)) return;

            try
            {
                using (var conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // 1. Load Tab 1: Users, Roles & Allowed Lanes
                    var usersList = new List<dynamic>();
                    string sqlUsers = @"
                        SELECT nv.Username, nv.Ten, nv.TrangThai, r.Name as RoleName, nv.Id as UserId
                        FROM dbo.NhanVien nv
                        LEFT JOIN dbo.Roles r ON nv.RoleId = r.Id";
                    
                    var usersTemp = new List<dynamic>();
                    using (var cmd = new SqlCommand(sqlUsers, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            usersTemp.Add(new {
                                Username = reader["Username"]?.ToString() ?? string.Empty,
                                Ten = reader["Ten"]?.ToString() ?? string.Empty,
                                TrangThai = reader["TrangThai"]?.ToString() ?? string.Empty,
                                RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                                UserId = Convert.ToInt32(reader["UserId"])
                            });
                        }
                    }

                    // For each user, load allowed lanes from UserLanePermissions
                    foreach (var u in usersTemp)
                    {
                        var laneNames = new List<string>();
                        string sqlLanes = @"
                            SELECT l.LaneName 
                            FROM dbo.UserLanePermissions ulp
                            JOIN dbo.Lanes l ON ulp.LaneId = l.Id
                            WHERE ulp.UserId = @uid";
                        using (var cmd = new SqlCommand(sqlLanes, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", u.UserId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    laneNames.Add(reader["LaneName"]?.ToString() ?? string.Empty);
                                }
                            }
                        }

                        string lanesText = laneNames.Any() ? string.Join(", ", laneNames) : "Toàn bộ làn (Bypass/Mặc định)";
                        usersList.Add(new {
                            u.Username,
                            u.Ten,
                            u.TrangThai,
                            u.RoleName,
                            AllowedLanes = lanesText
                        });
                    }
                    gridUsers.ItemsSource = usersList;

                    // 2. Load Tab 2: Roles & Software Permissions
                    var permsList = new List<dynamic>();
                    string sqlPerms = @"
                        SELECT r.Name as RoleName, p.Code as PermissionCode, p.Name as PermissionName, p.Description
                        FROM dbo.RolePermissions rp
                        JOIN dbo.Roles r ON rp.RoleId = r.Id
                        JOIN dbo.Permissions p ON rp.PermissionId = p.Id
                        ORDER BY r.Name, p.Code";
                    using (var cmd = new SqlCommand(sqlPerms, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            permsList.Add(new {
                                RoleName = reader["RoleName"]?.ToString() ?? string.Empty,
                                PermissionCode = reader["PermissionCode"]?.ToString() ?? string.Empty,
                                PermissionName = reader["PermissionName"]?.ToString() ?? string.Empty,
                                Description = reader["Description"]?.ToString() ?? string.Empty
                            });
                        }
                    }
                    gridPermissions.ItemsSource = permsList;

                    // 3. Load Tab 3: RFID Groups & Physical Schedules
                    var physicalList = new List<dynamic>();
                    string sqlPhysical = @"
                        SELECT cg.GroupName, l.LaneName, s.ScheduleName, s.StartTime, s.EndTime, s.DaysOfWeek
                        FROM dbo.CardGroupLanePermissions cglp
                        JOIN dbo.CardGroups cg ON cglp.GroupId = cg.Id
                        JOIN dbo.Lanes l ON cglp.LaneId = l.Id
                        LEFT JOIN dbo.AccessSchedules s ON cglp.ScheduleId = s.Id";
                    using (var cmd = new SqlCommand(sqlPhysical, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string timeRange = "24/7 Unlimited";
                            if (reader["ScheduleName"] != DBNull.Value)
                            {
                                timeRange = $"{reader["StartTime"]} - {reader["EndTime"]}";
                            }
                            physicalList.Add(new {
                                GroupName = reader["GroupName"]?.ToString() ?? string.Empty,
                                LaneName = reader["LaneName"]?.ToString() ?? string.Empty,
                                ScheduleName = reader["ScheduleName"]?.ToString() ?? "Không giới hạn",
                                TimeRange = timeRange,
                                DaysOfWeek = reader["DaysOfWeek"]?.ToString() ?? "Tất cả các ngày"
                            });
                        }
                    }
                    gridPhysical.ItemsSource = physicalList;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu phân quyền: {ex.Message}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
