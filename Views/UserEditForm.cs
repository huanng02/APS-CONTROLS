using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using QuanLyGiuXe.Models;
using WFTextBox   = System.Windows.Forms.TextBox;
using WFButton    = System.Windows.Forms.Button;
using WFLabel     = System.Windows.Forms.Label;
using WFComboBox  = System.Windows.Forms.ComboBox;
using WFControl   = System.Windows.Forms.Control;
using WFPanel     = System.Windows.Forms.Panel;
using WFMessageBox        = System.Windows.Forms.MessageBox;
using WFMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using WFMessageBoxIcon    = System.Windows.Forms.MessageBoxIcon;
using WFDialogResult      = System.Windows.Forms.DialogResult;
using WFComboBoxStyle     = System.Windows.Forms.ComboBoxStyle;
using WFFormStartPosition = System.Windows.Forms.FormStartPosition;
using WFFormBorderStyle   = System.Windows.Forms.FormBorderStyle;
using WFPadding           = System.Windows.Forms.Padding;

namespace QuanLyGiuXe.Views
{
    public sealed class UserEditForm : Form
    {
        // ── palette ───────────────────────────────────────────────────────
        private static readonly Color ClrPrimary  = Color.FromArgb(79, 70, 229);
        private static readonly Color ClrHeaderBg = Color.FromArgb(30, 27, 75);
        private static readonly Color ClrBg       = Color.FromArgb(245, 247, 250);
        private static readonly Color ClrSurface  = Color.White;
        private static readonly Color ClrBorder   = Color.FromArgb(209, 213, 219);
        private static readonly Color ClrLabel    = Color.FromArgb(55, 65, 81);
        private static readonly Color ClrMuted    = Color.FromArgb(107, 114, 128);

        // ── state ─────────────────────────────────────────────────────────
        private readonly bool _isCreate;

        // ── controls ─────────────────────────────────────────────────────
        private readonly WFTextBox  _txtTen      = new();
        private readonly WFTextBox  _txtUsername  = new();
        private readonly WFTextBox  _txtPassword  = new();
        private readonly WFComboBox _cboRole      = new();
        private readonly WFComboBox _cboStatus    = new();
        private readonly WFButton   _btnSave      = new();
        private readonly WFButton   _btnCancel    = new();

        public UserUpsertModel Result { get; private set; } = new();

        public UserEditForm(bool isCreate, List<RoleOption> roles, UserListItem? editingUser = null)
        {
            _isCreate = isCreate;
            InitializeComponent();
            BindRoles(roles);
            BindStatus();
            FillData(editingUser);
        }

        private void InitializeComponent()
        {
            Text            = _isCreate ? "Thêm người dùng" : "Chỉnh sửa người dùng";
            StartPosition   = WFFormStartPosition.CenterParent;
            FormBorderStyle = WFFormBorderStyle.None;   // custom chrome
            MaximizeBox     = false;
            MinimizeBox     = false;
            Size            = new Size(480, _isCreate ? 430 : 390);
            Font            = new Font("Segoe UI", 10);
            BackColor       = ClrBg;

            // ── title bar ─────────────────────────────────────────────────
            var titleBar = new WFPanel
            {
                Dock      = DockStyle.Top,
                Height    = 56,
                BackColor = ClrHeaderBg
            };
            var icon = new WFLabel
            {
                Text      = _isCreate ? "＋" : "✏",
                Font      = new Font("Segoe UI", 14),
                ForeColor = Color.FromArgb(167, 139, 250),
                Size      = new Size(40, 56),
                Location  = new Point(16, 0),
                TextAlign = ContentAlignment.MiddleCenter
            };
            var lblTitle = new WFLabel
            {
                Text      = _isCreate ? "Thêm người dùng mới" : "Chỉnh sửa thông tin",
                Font      = new Font("Segoe UI Semibold", 12, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(58, 17)
            };
            // close button
            var btnClose = new WFButton
            {
                Text      = "✕",
                Size      = new Size(36, 36),
                Location  = new Point(Width - 46, 10),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(167, 139, 250),
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 11),
                Cursor    = System.Windows.Forms.Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 255, 255, 255);
            btnClose.Click += (_, _) => DialogResult = WFDialogResult.Cancel;

            titleBar.Controls.AddRange(new WFControl[] { icon, lblTitle, btnClose });

            // drag form via title bar
            bool dragging = false;
            Point dragStart = default;
            titleBar.MouseDown += (_, e) => { dragging = true; dragStart = e.Location; };
            titleBar.MouseMove += (_, e) => { if (dragging) Location = new Point(Location.X + e.X - dragStart.X, Location.Y + e.Y - dragStart.Y); };
            titleBar.MouseUp   += (_, _) => dragging = false;

            // ── body ──────────────────────────────────────────────────────
            var body = new WFPanel
            {
                Dock      = DockStyle.Fill,
                BackColor = ClrBg,
                Padding   = new WFPadding(28, 20, 28, 20)
            };

            int y = 0;
            AddFormRow(body, "Họ & Tên *",    _txtTen,     ref y);
            AddFormRow(body, "Username *",     _txtUsername, ref y);
            if (_isCreate) AddFormRow(body, "Password *", _txtPassword, ref y);
            AddFormRow(body, "Vai trò *",      _cboRole,    ref y);
            AddFormRow(body, "Trạng thái *",   _cboStatus,  ref y);

            _txtPassword.PasswordChar = '*';
            _cboRole.DropDownStyle    = WFComboBoxStyle.DropDownList;
            _cboStatus.DropDownStyle  = WFComboBoxStyle.DropDownList;

            if (!_isCreate)
            {
                _txtUsername.ReadOnly  = true;
                _txtUsername.BackColor = Color.FromArgb(243, 244, 246);
                _txtUsername.ForeColor = ClrMuted;
            }

            // ── footer bar ────────────────────────────────────────────────
            var footer = new WFPanel
            {
                Dock      = DockStyle.Bottom,
                Height    = 64,
                BackColor = ClrSurface,
                Padding   = new WFPadding(20, 12, 20, 12)
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(ClrBorder);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            StyleFooterButton(_btnSave, "💾  Lưu lại", ClrPrimary);
            _btnSave.Location = new Point(footer.Width - 268, 12);
            _btnSave.Click   += Save_Click;
            _btnSave.Anchor   = AnchorStyles.Right | AnchorStyles.Top;

            StyleFooterButton(_btnCancel, "Hủy", Color.FromArgb(209, 213, 219), foreColor: ClrLabel);
            _btnCancel.Location = new Point(footer.Width - 136, 12);
            _btnCancel.Click   += (_, _) => DialogResult = WFDialogResult.Cancel;
            _btnCancel.Anchor   = AnchorStyles.Right | AnchorStyles.Top;

            footer.Controls.AddRange(new WFControl[] { _btnSave, _btnCancel });

            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(titleBar);

            // border
            Paint += (s, e) =>
            {
                using var pen = new Pen(ClrBorder);
                e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, Width - 1, Height - 1));
            };
        }

        // ── form row builder ──────────────────────────────────────────────
        private static void AddFormRow(WFPanel parent, string labelText, WFControl input, ref int y)
        {
            var lbl = new WFLabel
            {
                Text      = labelText,
                Font      = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                ForeColor = ClrLabel,
                AutoSize  = true,
                Location  = new Point(0, y)
            };

            input.Location  = new Point(0, y + 22);
            input.Size      = new Size(parent.Width - parent.Padding.Horizontal, 36);
            input.Font      = new Font("Segoe UI", 10);
            input.Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            if (input is WFTextBox tb) tb.BorderStyle = BorderStyle.FixedSingle;

            parent.Controls.Add(lbl);
            parent.Controls.Add(input);
            y += 68;
        }

        private static void StyleFooterButton(WFButton btn, string text, Color bg, Color? foreColor = null)
        {
            btn.Text      = text;
            btn.Size      = new Size(120, 40);
            btn.BackColor = bg;
            btn.ForeColor = foreColor ?? Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize          = bg == Color.FromArgb(209, 213, 219) ? 1 : 0;
            btn.FlatAppearance.BorderColor         = ClrBorder;
            btn.FlatAppearance.MouseOverBackColor  = ControlPaint.Light(bg, 0.15f);
            btn.Cursor    = System.Windows.Forms.Cursors.Hand;
            btn.Font      = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
        }

        // ── data binding (unchanged logic) ────────────────────────────────
        private void BindRoles(List<RoleOption> roles)
        {
            _cboRole.DataSource    = roles;
            _cboRole.DisplayMember = nameof(RoleOption.Name);
            _cboRole.ValueMember   = nameof(RoleOption.Id);
        }

        private void BindStatus()
        {
            _cboStatus.Items.Clear();
            _cboStatus.Items.AddRange(new object[] { "Active", "Disabled" });
            _cboStatus.SelectedIndex = 0;
        }

        private void FillData(UserListItem? user)
        {
            if (user == null) return;
            _txtTen.Text          = user.Ten;
            _txtUsername.Text     = user.Username;
            _cboRole.SelectedValue = user.RoleId;
            _cboStatus.SelectedItem = user.TrangThai;
        }

        private void Save_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtTen.Text)      ||
                string.IsNullOrWhiteSpace(_txtUsername.Text)  ||
                _cboRole.SelectedValue == null                ||
                _cboStatus.SelectedItem == null)
            {
                WFMessageBox.Show("Vui lòng nhập đầy đủ thông tin.", "Validation", WFMessageBoxButtons.OK, WFMessageBoxIcon.Warning);
                return;
            }
            if (_isCreate && _txtPassword.Text.Trim().Length < 6)
            {
                WFMessageBox.Show("Password phải có ít nhất 6 ký tự.", "Validation", WFMessageBoxButtons.OK, WFMessageBoxIcon.Warning);
                return;
            }
            Result = new UserUpsertModel
            {
                Ten       = _txtTen.Text.Trim(),
                Username  = _txtUsername.Text.Trim(),
                Password  = _isCreate ? _txtPassword.Text.Trim() : string.Empty,
                RoleId    = Convert.ToInt32(_cboRole.SelectedValue),
                TrangThai = _cboStatus.SelectedItem?.ToString() ?? "Active"
            };
            DialogResult = WFDialogResult.OK;
        }
    }
}
