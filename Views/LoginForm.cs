using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using QuanLyGiuXe.Models;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace QuanLyGiuXe.Views
{
    public class LoginForm : Form
    {
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnLogin;
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
                // Fallback nếu có lỗi khi gọi DatabaseService
                connectionString = "Server=192.168.2.13,1433;Database=BaiXe;User Id=appuser;Password=123456;";
            }
        }

        private void InitializeComponent()
        {
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.lblMessage = new System.Windows.Forms.Label();
            
            System.Windows.Forms.Panel pnlHeader = new System.Windows.Forms.Panel();
            System.Windows.Forms.Label lblTitle = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblSubTitle = new System.Windows.Forms.Label();
            System.Windows.Forms.Panel pnlCard = new System.Windows.Forms.Panel();
            System.Windows.Forms.Label lblUserIcon = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblPassIcon = new System.Windows.Forms.Label();

            // Form Settings
            this.Text = "Hệ Thống Quản Lý Bãi Xe - Đăng Nhập";
            this.Size = new Size(450, 500);
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

            // Card Panel
            pnlCard.Size = new Size(360, 280);
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
            lblPassIcon.Location = new Point(30, 90);
            lblPassIcon.AutoSize = true;
            lblPassIcon.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            lblPassIcon.ForeColor = Color.FromArgb(64, 64, 64);

            this.txtPassword.Location = new Point(30, 115);
            this.txtPassword.Width = 300;
            this.txtPassword.Font = new Font("Segoe UI", 12);
            this.txtPassword.PasswordChar = '●';
            this.txtPassword.BorderStyle = BorderStyle.FixedSingle;

            // Login Button
            this.btnLogin.Text = "ĐĂNG NHẬP";
            this.btnLogin.Location = new Point(30, 180);
            this.btnLogin.Size = new Size(300, 45);
            this.btnLogin.BackColor = Color.FromArgb(0, 120, 215); // Modern Blue
            this.btnLogin.ForeColor = Color.White;
            this.btnLogin.FlatStyle = FlatStyle.Flat;
            this.btnLogin.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.btnLogin.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLogin.FlatAppearance.BorderSize = 0;
            this.btnLogin.Click += BtnLogin_Click;

            // Error Message
            this.lblMessage.Location = new Point(30, 235);
            this.lblMessage.Size = new Size(300, 30);
            this.lblMessage.ForeColor = Color.Red;
            this.lblMessage.TextAlign = ContentAlignment.MiddleCenter;
            this.lblMessage.Font = new Font("Segoe UI", 9, FontStyle.Italic);

            pnlCard.Controls.Add(lblUserIcon);
            pnlCard.Controls.Add(this.txtUsername);
            pnlCard.Controls.Add(lblPassIcon);
            pnlCard.Controls.Add(this.txtPassword);
            pnlCard.Controls.Add(this.btnLogin);
            pnlCard.Controls.Add(this.lblMessage);

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
            btnLogin.Text = "ĐANG XỬ LÝ...";
            lblMessage.Text = "⏳ Đang xác thực tài khoản...";
            lblMessage.ForeColor = Color.Orange;

            try
            {
                // Authenticate in background to keep UI responsive
                var userFound = await Task.Run(() => AuthenticateUser(user, pass));

                if (userFound != null)
                {
                    if (userFound.Status != "Active")
                    {
                        lblMessage.Text = "❌ Tài khoản đã bị khóa!";
                        lblMessage.ForeColor = Color.Red;
                        btnLogin.Enabled = true;
                        btnLogin.Text = "ĐĂNG NHẬP";
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

                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    lblMessage.Text = "❌ Sai tài khoản hoặc mật khẩu!";
                    lblMessage.ForeColor = Color.Red;
                    btnLogin.Enabled = true;
                    btnLogin.Text = "ĐĂNG NHẬP";
                }
            }
            catch (Exception ex)
            {
                lblMessage.Text = "❌ Lỗi: " + ex.Message;
                btnLogin.Enabled = true;
                btnLogin.Text = "ĐĂNG NHẬP";
            }
        }

        private dynamic? AuthenticateUser(string user, string pass)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT nv.Id, nv.Username, nv.Ten, nv.TrangThai, r.Name as RoleName 
                    FROM NhanVien nv
                    LEFT JOIN Roles r ON nv.RoleId = r.Id
                    WHERE nv.Username = @user AND nv.Password = @pass", conn))
                {
                    cmd.Parameters.AddWithValue("@user", user);
                    cmd.Parameters.AddWithValue("@pass", pass);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
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
