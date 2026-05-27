using System;
using System.Drawing;
using System.Windows.Forms;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private System.Windows.Forms.Button btnNhanVien;
        private System.Windows.Forms.Button btnBaoCao;
        private System.Windows.Forms.Button btnThuTien;
        private System.Windows.Forms.Button btnVanHanh;

        private void InitializeComponent()
        {
            System.Windows.Forms.Label lblWelcome = new System.Windows.Forms.Label();
            System.Windows.Forms.Label lblRole = new System.Windows.Forms.Label();
            System.Windows.Forms.Button btnLogout = new System.Windows.Forms.Button();
            
            this.btnNhanVien = new System.Windows.Forms.Button();
            this.btnBaoCao = new System.Windows.Forms.Button();
            this.btnThuTien = new System.Windows.Forms.Button();
            this.btnVanHanh = new System.Windows.Forms.Button();

            this.Text = "Hệ Thống Quản Lý Bãi Xe - Main Menu";
            this.Size = new System.Drawing.Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Welcome Info
            lblWelcome.Text = $"Chào mừng: {CurrentUser.Ten}";
            lblWelcome.Font = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold);
            lblWelcome.Location = new System.Drawing.Point(20, 20);
            lblWelcome.AutoSize = true;

            lblRole.Text = $"Quyền hạn: {CurrentUser.Role}";
            lblRole.Location = new System.Drawing.Point(20, 50);
            lblRole.AutoSize = true;

            // Menu Buttons
            int startY = 100;
            int gap = 40;

            btnNhanVien.Text = "👥 Quản Lý Nhân Viên";
            btnNhanVien.Location = new System.Drawing.Point(20, startY);
            btnNhanVien.Size = new System.Drawing.Size(200, 30);
            btnNhanVien.Click += BtnNhanVien_Click;

            btnBaoCao.Text = "📊 Báo cáo doanh thu";
            btnBaoCao.Location = new System.Drawing.Point(20, startY + gap);
            btnBaoCao.Size = new System.Drawing.Size(200, 30);

            btnThuTien.Text = "💰 Quản lý thu tiền";
            btnThuTien.Location = new System.Drawing.Point(20, startY + (gap * 2));
            btnThuTien.Size = new System.Drawing.Size(200, 30);

            btnVanHanh.Text = "⚙ Vận hành hệ thống";
            btnVanHanh.Location = new System.Drawing.Point(20, startY + (gap * 3));
            btnVanHanh.Size = new System.Drawing.Size(200, 30);

            btnLogout.Text = "🚪 Đăng Xuất";
            btnLogout.Location = new System.Drawing.Point(20, startY + (gap * 4) + 20);
            btnLogout.Size = new System.Drawing.Size(200, 30);
            btnLogout.Click += (s, e) => {
                CurrentUser.Clear();
                this.Close();
            };

            this.Controls.Add(lblWelcome);
            this.Controls.Add(lblRole);
            this.Controls.Add(btnNhanVien);
            this.Controls.Add(btnBaoCao);
            this.Controls.Add(btnThuTien);
            this.Controls.Add(btnVanHanh);
            this.Controls.Add(btnLogout);

            ApplyPermissions();
        }

        private void ApplyPermissions()
        {
            string role = CurrentUser.Role?.ToUpper() ?? "";

            // 1. Nhân viên -> Chỉ SUPERADMIN, ADMIN
            btnNhanVien.Visible = (role == "SUPERADMIN" || role == "ADMIN");

            // 2. Báo cáo -> SUPERADMIN, ADMIN, MANAGER, CASHIER, VIEWER
            btnBaoCao.Visible = (role == "SUPERADMIN" || role == "ADMIN" || role == "MANAGER" || role == "CASHIER" || role == "VIEWER");

            // 3. Thu tiền -> SUPERADMIN, ADMIN, MANAGER, CASHIER
            btnThuTien.Visible = (role == "SUPERADMIN" || role == "ADMIN" || role == "MANAGER" || role == "CASHIER");

            // 4. Vận hành -> SUPERADMIN, ADMIN, MANAGER, OPERATOR, TECHNICIAN
            btnVanHanh.Visible = (role == "SUPERADMIN" || role == "ADMIN" || role == "MANAGER" || role == "OPERATOR" || role == "TECHNICIAN");
        }

        private void BtnNhanVien_Click(object? sender, EventArgs e)
        {
            using var frm = new UserManagementForm();
            frm.ShowDialog(this);
        }
    }
}
