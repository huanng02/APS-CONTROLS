using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using BCrypt.Net;

namespace QuanLyGiuXe.Views
{
    public class LoginForm : Form
    {
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Button btnDbConfig;
        private System.Windows.Forms.Label lblMessage;
        
        // Connection string - should be in config, but provided here for demo or loaded from existing appsettings
        private string connectionString = "";

        public LoginForm()
        {
            InitializeComponent();
            LoadConnectionString();
        }

        private void LoadConnectionString()
        {
            try
            {
                var dbService = new QuanLyGiuXe.Services.DatabaseService();
                connectionString = dbService.GetConnectionString();
            }
            catch (Exception ex)
            {
                // Fallback to ConnectionManager directly if DatabaseService fails
                connectionString = ConnectionManager.Instance.CurrentConnectionString;
            }
        }

        private void InitializeComponent()
        {
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.btnDbConfig = new System.Windows.Forms.Button();
            this.lblMessage = new System.Windows.Forms.Label();
            
            System.Windows.Forms.Panel pnlHeader = new System.Windows.Forms.Panel();
            System.Windows.Forms.Label lblTitle = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSubTitle = new System.Windows.Forms.Label();
            System.Windows.Forms.Panel pnlCard = new System.Windows.Forms.Panel();
            System.Windows.Forms.Label lblUserIcon = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblPassIcon = new System.Windows.Forms.Label();

            // Form Settings
            this.Text = "Hệ Thống Quản Lý Bãi Xe - Đăng Nhập";
            this.Size = new Size(450, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(240, 242, 245); // Soft Gray BG

            // Header Section
            pnlHeader.Dock = DockStyle.Top;
            pnlHeader.Height = 120;
            pnlHeader.BackColor = Color.FromArgb(47, 54, 64); // Dark Navy

            lblTitle.Text = "APS PARKING SYSTEM";
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblTitle.Size = new Size(450, 40);
            lblTitle.Location = new Point(0, 30);

            lblSubTitle.Text = "Vui lòng đăng nhập để tiếp tục";
            lblSubTitle.ForeColor = Color.FromArgb(200, 200, 200);
            lblSubTitle.Font = new Font("Segoe UI", 10);
            lblSubTitle.TextAlign = ContentAlignment.MiddleCenter;
            lblSubTitle.Size = new Size(450, 25);
            lblSubTitle.Location = new Point(0, 70);

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubTitle);

            // Card Panel - Y positions inside card:
            // lblUserIcon:   Y=20
            // txtUsername:    Y=45  (h=30)
            // lblPassIcon:   Y=85
            // txtPassword:   Y=110 (h=30)
            // btnLogin:      Y=160 (h=45)
            // lblMessage:    Y=215 (h=40)
            // btnDbConfig:   Y=260 (h=28)
            // Total needed:  ~300
            pnlCard.Size = new Size(360, 300);
            pnlCard.Location = new Point(45, 140);
            pnlCard.BackColor = Color.White;
            pnlCard.Padding = new Padding(30);

            // Username
            lblUserIcon.Text = "👤 Tên đăng nhập";
            lblUserIcon.Location = new Point(30, 20);
            lblUserIcon.AutoSize = true;
            lblUserIcon.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblUserIcon.ForeColor = Color.FromArgb(64, 64, 64);

            this.txtUsername.Location = new Point(30, 45);
            this.txtUsername.Width = 300;
            this.txtUsername.Font = new Font("Segoe UI", 12);
            this.txtUsername.BorderStyle = BorderStyle.FixedSingle;

            // Password
            lblPassIcon.Text = "🔒 Mật khẩu";
            lblPassIcon.Location = new Point(30, 85);
            lblPassIcon.AutoSize = true;
            lblPassIcon.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblPassIcon.ForeColor = Color.FromArgb(64, 64, 64);

            this.txtPassword.Location = new Point(30, 110);
            this.txtPassword.Width = 300;
            this.txtPassword.Font = new Font("Segoe UI", 12);
            this.txtPassword.PasswordChar = '●';
            this.txtPassword.BorderStyle = BorderStyle.FixedSingle;

            // Login Button
            this.btnLogin.Text = "ĐĂNG NHẬP";
            this.btnLogin.Location = new Point(30, 160);
            this.btnLogin.Size = new Size(300, 45);
            this.btnLogin.BackColor = Color.FromArgb(0, 120, 215); // Modern Blue
            this.btnLogin.ForeColor = Color.White;
            this.btnLogin.FlatStyle = FlatStyle.Flat;
            this.btnLogin.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.btnLogin.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogin.FlatAppearance.BorderSize = 0;
            this.btnLogin.Click += BtnLogin_Click;

            // Error Message
            this.lblMessage.Location = new Point(30, 215);
            this.lblMessage.Size = new Size(300, 35);
            this.lblMessage.ForeColor = Color.Red;
            this.lblMessage.TextAlign = ContentAlignment.MiddleCenter;
            this.lblMessage.Font = new Font("Segoe UI", 9, FontStyle.Italic);

            // DB Config Button
            this.btnDbConfig.Text = "⚙ Cấu hình CSDL";
            this.btnDbConfig.Location = new Point(30, 258);
            this.btnDbConfig.Size = new Size(300, 28);
            this.btnDbConfig.FlatStyle = FlatStyle.Flat;
            this.btnDbConfig.FlatAppearance.BorderSize = 0;
            this.btnDbConfig.ForeColor = Color.Gray;
            this.btnDbConfig.Font = new Font("Segoe UI", 8);
            this.btnDbConfig.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDbConfig.TextAlign = ContentAlignment.MiddleCenter;
            this.btnDbConfig.Click += BtnDbConfig_Click;

            pnlCard.Controls.Add(lblUserIcon);
            pnlCard.Controls.Add(this.txtUsername);
            pnlCard.Controls.Add(lblPassIcon);
            pnlCard.Controls.Add(this.txtPassword);
            pnlCard.Controls.Add(this.btnLogin);
            pnlCard.Controls.Add(this.lblMessage);
            pnlCard.Controls.Add(this.btnDbConfig);

            // Shadow paint for the card
            this.Paint += (s, e) => {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(200, 200, 200)), pnlCard.Left + 5, pnlCard.Top + 5, pnlCard.Width, pnlCard.Height);
            };

            this.Controls.Add(pnlCard);
            this.Controls.Add(pnlHeader);
            this.AcceptButton = btnLogin;
        }

        private async void BtnLogin_Click(object? sender, EventArgs e)
        {
            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                lblMessage.Text = "Vui lòng nhập đầy đủ thông tin!";
                return;
            }

            // Visual feedback
            btnLogin.Enabled = false;
            btnDbConfig.Enabled = false;
            btnLogin.Text = "ĐANG XỬ LÝ...";
            lblMessage.Text = "⏳ Đang kiểm tra kết nối CSDL...";
            lblMessage.ForeColor = Color.Orange;

            try
            {
                // Step 1: Kiểm tra kết nối DB trước khi xác thực
                bool dbOk = await Task.Run(() =>
                {
                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            return true;
                        }
                    }
                    catch
                    {
                        return false;
                    }
                });

                if (!dbOk)
                {
                    lblMessage.Text = "❌ Không kết nối được CSDL!\nNhấn \"⚙ Cấu hình CSDL\" để đổi IP.";
                    lblMessage.ForeColor = Color.Red;
                    btnLogin.Enabled = true;
                    btnDbConfig.Enabled = true;
                    btnLogin.Text = "ĐĂNG NHẬP";
                    return;
                }

                // Step 2: Xác thực tài khoản
                lblMessage.Text = "⏳ Đang xác thực tài khoản...";
                var userFound = await Task.Run(() => AuthenticateUser(user, pass));

                if (userFound != null)
                {
                    if (userFound.Status != "Active")
                    {
                        lblMessage.Text = "❌ Tài khoản đã bị khóa!";
                        lblMessage.ForeColor = Color.Red;
                        btnLogin.Enabled = true;
                        btnDbConfig.Enabled = true;
                        btnLogin.Text = "ĐĂNG NHẬP";
                        try { LoggingService.Instance.LogSecurity("LOGIN_FAILED", "Auth", "{\"Reason\":\"AccountLocked\"}", userId: userFound.Id.ToString(), username: user); } catch { }
                        return;
                    }

                    lblMessage.Text = "✔ Thành công! Đang khởi động hệ thống...";
                    lblMessage.ForeColor = Color.Green;
                    
                    // Small delay so user sees the success message
                    await Task.Delay(400);

                    CurrentUser.Id = userFound.Id;
                    CurrentUser.Username = userFound.Username;
                    CurrentUser.Ten = userFound.Ten;
                    CurrentUser.Role = userFound.Role;

                    try { LoggingService.Instance.LogSecurity("LOGIN_SUCCESS", "Auth", null, userId: CurrentUser.Id.ToString(), username: CurrentUser.Username); } catch { }
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    lblMessage.Text = "❌ Sai tài khoản hoặc mật khẩu!";
                    lblMessage.ForeColor = Color.Red;
                    btnLogin.Enabled = true;
                    btnDbConfig.Enabled = true;
                    btnLogin.Text = "ĐĂNG NHẬP";
                    // AuthenticateUser already logs failed attempts; avoid duplicate logging here
                }
            }
            catch (Exception ex)
            {
                lblMessage.ForeColor = Color.Red;
                btnLogin.Enabled = true;
                btnDbConfig.Enabled = true;
                btnLogin.Text = "ĐĂNG NHẬP";
                
                if (ex.Message.Contains("network-related") || ex.Message.Contains("timeout") || ex.Message.Contains("server was not found") || ex is SqlException)
                {
                    lblMessage.Text = "❌ Lỗi kết nối CSDL!\nNhấn \"⚙ Cấu hình CSDL\" để đổi IP.";
                }
                else
                {
                    lblMessage.Text = "❌ Lỗi: " + ex.Message;
                }
            }
        }

        private void BtnDbConfig_Click(object? sender, EventArgs e)
        {
            var configWindow = new ConnectDatabaseWindow();
            // ConnectDatabaseWindow is WPF, so we use ElementHost or just ShowDialog if it works
            // In many hybrid apps, WPF windows can be shown from WinForms.
            configWindow.ShowDialog();
            
            // Reload connection string after config window closes
            LoadConnectionString();
            lblMessage.Text = "⚙ Đã cập nhật cấu hình CSDL.";
            lblMessage.ForeColor = Color.Blue;
        }

        private dynamic? AuthenticateUser(string user, string pass)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT nv.Id, nv.Username, nv.Ten, nv.TrangThai, r.Name as RoleName, nv.Password as StoredPassword
                    FROM NhanVien nv
                    LEFT JOIN Roles r ON nv.RoleId = r.Id
                    WHERE nv.Username = @user", conn))
                {
                    cmd.Parameters.AddWithValue("@user", user);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var stored = reader["StoredPassword"]?.ToString() ?? string.Empty;

                            bool verified = false;

                            if (stored.StartsWith("$2"))
                            {
                                try { verified = BCrypt.Net.BCrypt.Verify(pass, stored); }
                                catch { verified = false; }
                            }
                            else
                            {
                                // legacy plaintext compare using fixed time
                                var a = System.Text.Encoding.UTF8.GetBytes(pass ?? string.Empty);
                                var b = System.Text.Encoding.UTF8.GetBytes(stored);
                                verified = (a.Length == b.Length) && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
                            }

                            if (!verified)
                            {
                                try { LoggingService.Instance.LogSecurity("LOGIN_FAILED", "Auth", "{\"Reason\":\"InvalidCredentials\",\"Username\":\"" + user + "\"}", userId: null, username: user); } catch { }
                                return null;
                            }

                            // If stored password was legacy plaintext, migrate to bcrypt now
                            if (!stored.StartsWith("$2"))
                            {
                                try
                                {
                                    string newHash = BCrypt.Net.BCrypt.HashPassword(pass);
                                    using (var upCmd = new SqlCommand("UPDATE NhanVien SET [Password] = @pwd WHERE Id = @id", conn))
                                    {
                                        upCmd.Parameters.AddWithValue("@pwd", newHash);
                                        upCmd.Parameters.AddWithValue("@id", reader["Id"]);
                                        upCmd.ExecuteNonQuery();
                                    }
                                }
                                catch { /* non-fatal migration failure */ }
                            }

                            return new {
                                Id = (int)reader["Id"],
                                Username = reader["Username"].ToString(),
                                Ten = reader["Ten"].ToString(),
                                Role = reader["RoleName"]?.ToString() ?? "",
                                Status = reader["TrangThai"]?.ToString() ?? ""
                            };
                        }
                    }
                }
            }
            return null;
        }
    }
}
