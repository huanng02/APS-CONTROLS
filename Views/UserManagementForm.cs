using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using WFTextBox = System.Windows.Forms.TextBox;
using WFButton = System.Windows.Forms.Button;
using WFLabel = System.Windows.Forms.Label;
using WFComboBox = System.Windows.Forms.ComboBox;
using WFPanel = System.Windows.Forms.Panel;
using WFFlowLayoutPanel = System.Windows.Forms.FlowLayoutPanel;
using WFMessageBox = System.Windows.Forms.MessageBox;
using WFMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using WFMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using WFDialogResult = System.Windows.Forms.DialogResult;
using WFComboBoxStyle = System.Windows.Forms.ComboBoxStyle;
using WFFormStartPosition = System.Windows.Forms.FormStartPosition;
using WFPadding = System.Windows.Forms.Padding;
using WFControl = System.Windows.Forms.Control;
using WFCursors = System.Windows.Forms.Cursors;

namespace QuanLyGiuXe.Views
{
    // ─── Color Palette ───────────────────────────────────────────────────────
    // Background  : #F5F7FA  (light canvas)
    // Surface     : #FFFFFF  (cards / panels)
    // Primary     : #4F46E5  (indigo-600)
    // Success     : #10B981  (emerald-500)
    // Danger      : #EF4444  (red-500)
    // Warning     : #F59E0B  (amber-500)
    // Muted       : #6B7280  (gray-500)
    // Header BG   : #1E1B4B  (indigo-950)
    // Header FG   : #FFFFFF
    // Alt row     : #F8F9FF
    // Hover row   : #EDE9FE  (violet-100)
    // Selected    : #C7D2FE  (indigo-200)
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class UserManagementForm : Form
    {
        // ── services & state ──────────────────────────────────────────────
        private readonly UserManagementService _service = new();
        private readonly BindingSource _binding = new();
        private List<RoleOption> _roles = new();
        private CancellationTokenSource? _searchDebounceCts;
        private int _hoverRowIndex = -1;

        // ── controls ─────────────────────────────────────────────────────
        private readonly DataGridView _grid = new();
        private readonly WFLabel _lblTotal = new();
        private readonly WFLabel _lblTotalCount = new();
        private readonly WFTextBox _txtSearch = new();
        private readonly WFComboBox _cboRoleFilter = new();
        private readonly WFComboBox _cboStatusFilter = new();
        private readonly WFButton _btnAdd = new();
        private readonly WFButton _btnEdit = new();
        private readonly WFButton _btnDisable = new();
        private readonly WFButton _btnResetPassword = new();
        private readonly WFButton _btnRefresh = new();

        // ── palette constants ─────────────────────────────────────────────
        private static readonly Color ClrBg        = Color.FromArgb(248, 249, 252); // BgLightColor
        private static readonly Color ClrSurface   = Color.White;
        private static readonly Color ClrPrimary   = Color.FromArgb(30, 79, 163);   // APSBlueColor
        private static readonly Color ClrSuccess   = Color.FromArgb(25, 135, 84);   // SuccessColor
        private static readonly Color ClrDanger    = Color.FromArgb(220, 53, 69);   // DangerColor
        private static readonly Color ClrWarning   = Color.FromArgb(255, 193, 7);   // WarningColor
        private static readonly Color ClrMuted     = Color.FromArgb(173, 181, 189); // TextMutedColor
        private static readonly Color ClrHeaderBg  = Color.FromArgb(30, 79, 163);   // APSBlueColor
        private static readonly Color ClrAltRow    = Color.FromArgb(248, 249, 252);
        private static readonly Color ClrHoverRow  = Color.FromArgb(241, 245, 251);
        private static readonly Color ClrSelected  = Color.FromArgb(227, 236, 247);

        public UserManagementForm()
        {
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await InitializeDataAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI BUILD
        // ══════════════════════════════════════════════════════════════════
        private void InitializeComponent()
        {
            Text = "Quản lý người dùng";
            StartPosition = WFFormStartPosition.CenterScreen;
            Size = new Size(1280, 780);
            MinimumSize = new Size(1024, 620);
            Font = new Font("Segoe UI", 10);
            BackColor = ClrBg;

            var header     = BuildTopHeader();
            var toolbar    = BuildToolbar();
            var gridCard   = BuildGridCard();

            Controls.Add(gridCard);    // Fill
            Controls.Add(toolbar);    // Top (added before header so docking stacks)
            Controls.Add(header);     // Top
        }

        // ── top header strip ──────────────────────────────────────────────
        private WFControl BuildTopHeader()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 85, // Tăng nhẹ height để các label "thở" tốt hơn
                BackColor = ClrHeaderBg,
                Padding = new Padding(25, 0, 25, 0)
            };

            // --- Bên trái: Tiêu đề & Subtitle ---
            var lblTitle = new WFLabel
            {
                Text = "Quản lý Người dùng",
                Font = new Font("Segoe UI", 17, FontStyle.Bold), // Tăng nhẹ size cho sang
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 18)
            };


            // --- BÊN PHẢI: BADGE (FlowLayout tối ưu) ---
            var pnlBadge = new WFFlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                WrapContents = false,
                // Không dùng Anchor ở đây vì chúng ta sẽ tính toán Left trong sự kiện Resize
            };

            var lblText = new WFLabel
            {
                Text = "Tổng người dùng",
                ForeColor = Color.FromArgb(167, 139, 250),
                Font = new Font("Segoe UI", 10, FontStyle.Bold), // Để Bold nhẹ cho chuyên nghiệp
                AutoSize = true,
                // Chỉnh Margin Top (18) để đẩy chữ xuống ngang hàng baseline với số 6 to
                Margin = new Padding(0, 22, 8, 0)
            };

            _lblTotalCount.Text = "6";
            _lblTotalCount.ForeColor = Color.White;
            _lblTotalCount.Font = new Font("Segoe UI", 26, FontStyle.Bold); // Số to rõ ràng
            _lblTotalCount.AutoSize = true;
            _lblTotalCount.Margin = new Padding(0, 10, 0, 0); // Căn lề trên cho số

            pnlBadge.Controls.Add(lblText);
            pnlBadge.Controls.Add(_lblTotalCount);

            // Thêm vào panel chính
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(pnlBadge);

            // Hàm căn chỉnh vị trí tự động
            void AlignBadge()
            {
                pnlBadge.Top = (pnl.Height - pnlBadge.Height) / 2; // Căn giữa chiều dọc header
                pnlBadge.Left = pnl.Width - pnlBadge.Width - 30;   // Cách lề phải 30px
            }

            // Cập nhật vị trí khi resize và ngay khi vừa tạo xong
            pnl.Resize += (s, e) => AlignBadge();

            // Gọi một lần sau khi gán mọi thứ để Badge nằm đúng chỗ ngay lập tức
            AlignBadge();

            return pnl;
        }

        // ── toolbar (search + filter + buttons) ───────────────────────────
        private WFControl BuildToolbar()
        {
            var toolbar = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = ClrSurface,
                Padding = new WFPadding(20, 14, 20, 14)
            };

            toolbar.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(229, 231, 235));
                e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            // =========================
            // BUTTON PANEL
            // =========================
            var pnlButtons = new WFFlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 650,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = ClrSurface,
                Padding = new WFPadding(0, 20, 0, 0)
            };

            StyleButton(_btnAdd, "＋ Thêm", ClrPrimary,
                async (_, _) => await AddUserAsync());

            StyleButton(_btnEdit, "✏ Sửa", ClrSuccess,
                async (_, _) => await EditSelectedUserAsync());

            StyleButton(_btnDisable, "⊘ Disable", ClrDanger,
                async (_, _) => await DisableSelectedUserAsync());

            StyleButton(_btnResetPassword, "🔑 Reset",
                ClrWarning,
                async (_, _) => await ResetPasswordAsync());

            StyleButton(_btnRefresh, "↺ Refresh",
                ClrMuted,
                async (_, _) => await LoadUsersAsync());

            pnlButtons.Controls.Add(_btnAdd);
            pnlButtons.Controls.Add(_btnEdit);
            pnlButtons.Controls.Add(_btnDisable);
            pnlButtons.Controls.Add(_btnResetPassword);
            pnlButtons.Controls.Add(_btnRefresh);

            // =========================
            // LEFT PANEL
            // =========================
            var pnlLeft = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrSurface
            };

            // SEARCH
            var searchContainer = MakeRoundedInputContainer(300);
            searchContainer.Location = new Point(0, 28);

            var searchIcon = new WFLabel
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 10),
                Size = new Size(26, 36),
                Location = new Point(6, 4),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _txtSearch.PlaceholderText = "Tìm tên hoặc username...";
            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.Font = new Font("Segoe UI", 10);
            _txtSearch.Location = new Point(36, 10);
            _txtSearch.Size = new Size(240, 25);

            _txtSearch.TextChanged += async (_, _) =>
                await DebouncedSearchAsync();

            searchContainer.Controls.Add(searchIcon);
            searchContainer.Controls.Add(_txtSearch);

            // ROLE LABEL
            var lblRole = MakeFilterLabel("Vai trò");
            lblRole.Location = new Point(330, 6);

            // ROLE COMBO
            _cboRoleFilter.Location = new Point(330, 28);
            _cboRoleFilter.Size = new Size(170, 40);
            _cboRoleFilter.DropDownStyle = WFComboBoxStyle.DropDownList;
            _cboRoleFilter.FlatStyle = FlatStyle.Flat;
            _cboRoleFilter.Font = new Font("Segoe UI", 10);

            _cboRoleFilter.SelectedIndexChanged += async (_, _) =>
                await LoadUsersAsync();

            // STATUS LABEL
            var lblStatus = MakeFilterLabel("Trạng thái");
            lblStatus.Location = new Point(520, 6);

            // STATUS COMBO
            _cboStatusFilter.Location = new Point(520, 28);
            _cboStatusFilter.Size = new Size(170, 40);
            _cboStatusFilter.DropDownStyle = WFComboBoxStyle.DropDownList;
            _cboStatusFilter.FlatStyle = FlatStyle.Flat;
            _cboStatusFilter.Font = new Font("Segoe UI", 10);

            _cboStatusFilter.Items.Clear();
            _cboStatusFilter.Items.AddRange(new object[]
            {
        "All",
        "Active",
        "Disabled"
            });

            _cboStatusFilter.SelectedIndex = 0;

            _cboStatusFilter.SelectedIndexChanged += async (_, _) =>
                await LoadUsersAsync();

            pnlLeft.Controls.Add(searchContainer);
            pnlLeft.Controls.Add(lblRole);
            pnlLeft.Controls.Add(_cboRoleFilter);
            pnlLeft.Controls.Add(lblStatus);
            pnlLeft.Controls.Add(_cboStatusFilter);

            toolbar.Controls.Add(pnlButtons);
            toolbar.Controls.Add(pnlLeft);

            return toolbar;
        }

        // ── grid card ─────────────────────────────────────────────────────
        private WFControl BuildGridCard()
        {
            var outer = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrBg,
                Padding = new WFPadding(20, 16, 20, 20)
            };

            var card = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrSurface,
                Padding = new WFPadding(0)
            };
            card.Paint += PaintCardShadow;

            // ── configure grid ─────────────────────────────────────────
            _grid.Dock = DockStyle.Fill;
            _grid.BackgroundColor = ClrSurface;
            _grid.BorderStyle = BorderStyle.None;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.ReadOnly = true;
            _grid.EnableHeadersVisualStyles = false;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = Color.FromArgb(243, 244, 246);

            // ĐÂY LÀ DÒNG QUAN TRỌNG ĐỂ FULL WIDTH
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // header style
            _grid.ColumnHeadersDefaultCellStyle.BackColor = ClrHeaderBg;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(196, 181, 253);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new WFPadding(8, 0, 0, 0);
            _grid.ColumnHeadersHeight = 44;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // row style
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            _grid.DefaultCellStyle.Padding = new WFPadding(8, 0, 0, 0);
            _grid.DefaultCellStyle.SelectionBackColor = ClrSelected;
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 27, 75);
            _grid.RowTemplate.Height = 46;

            // ── columns (Sử dụng FillWeight thay vì Width để chia tỷ lệ % linh hoạt) ──
            _grid.Columns.Clear();

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Id",
                HeaderText = "ID",
                FillWeight = 40
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Ten",
                HeaderText = "Họ & Tên",
                FillWeight = 180
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Username",
                HeaderText = "Username",
                FillWeight = 120
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RoleName",
                HeaderText = "Vai trò",
                FillWeight = 110
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TrangThai",
                HeaderText = "Trạng thái",
                FillWeight = 90
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedAt",
                HeaderText = "Ngày tạo",
                FillWeight = 130,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm" }
            });

            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "LastLogin",
                HeaderText = "Đăng nhập cuối",
                FillWeight = 130,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy HH:mm", NullValue = "—" }
            });

            _grid.DataSource = _binding;
            _grid.CellDoubleClick += async (_, _) => await EditSelectedUserAsync();
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellMouseEnter += Grid_CellMouseEnter;
            _grid.CellMouseLeave += Grid_CellMouseLeave;

            card.Controls.Add(_grid);
            outer.Controls.Add(card);
            return outer;
        }

        // ══════════════════════════════════════════════════════════════════
        //  DATA LOGIC  (unchanged)
        // ══════════════════════════════════════════════════════════════════
        private async Task InitializeDataAsync()
        {
            try
            {
                _roles = await _service.GetRolesAsync();
                _cboRoleFilter.Items.Clear();
                _cboRoleFilter.Items.Add("All");
                foreach (var role in _roles)
                    _cboRoleFilter.Items.Add(role);
                _cboRoleFilter.DisplayMember = nameof(RoleOption.Name);
                _cboRoleFilter.SelectedIndex = 0;
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                WFMessageBox.Show($"Không thể tải dữ liệu: {ex.Message}", "Error", WFMessageBoxButtons.OK, WFMessageBoxIcon.Error);
            }
        }

        private async Task DebouncedSearchAsync()
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts = new CancellationTokenSource();
            var token = _searchDebounceCts.Token;
            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested) await LoadUsersAsync();
            }
            catch (TaskCanceledException) { }
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                UseWaitCursor = true;
                int? roleId = _cboRoleFilter.SelectedItem is RoleOption role ? role.Id : null;
                string status = _cboStatusFilter.SelectedItem?.ToString() ?? "All";
                string search = _txtSearch.Text.Trim();
                var users = await _service.SearchUsersAsync(CurrentUser.Id, search, roleId, status);
                _binding.DataSource     = users;
                _lblTotalCount.Text     = users.Count.ToString();
            }
            catch (Exception ex)
            {
                WFMessageBox.Show($"Lỗi tải danh sách: {ex.Message}", "Error", WFMessageBoxButtons.OK, WFMessageBoxIcon.Error);
            }
            finally { UseWaitCursor = false; }
        }

        private UserListItem? GetSelectedUser() => _grid.CurrentRow?.DataBoundItem as UserListItem;

        private async Task AddUserAsync()
        {
            var dialog = new UserAddEditWindow(isCreate: true, roles: _roles);
            if (dialog.ShowDialog() != true) return;
            var result = await _service.CreateUserAsync(dialog.Result, CurrentUser.Id);
            WFMessageBox.Show(result.Message, result.Success ? "Success" : "Validation",
                WFMessageBoxButtons.OK, result.Success ? WFMessageBoxIcon.Information : WFMessageBoxIcon.Warning);
            if (result.Success) await LoadUsersAsync();
        }

        private async Task EditSelectedUserAsync()
        {
            var user = GetSelectedUser();
            if (user == null) { WFMessageBox.Show("Vui lòng chọn user cần sửa.", "Info", WFMessageBoxButtons.OK, WFMessageBoxIcon.Information); return; }
            var dialog = new UserAddEditWindow(isCreate: false, roles: _roles, editingUser: user);
            if (dialog.ShowDialog() != true) return;
            var result = await _service.UpdateUserAsync(user.Id, dialog.Result.Ten, dialog.Result.RoleId, dialog.Result.TrangThai, CurrentUser.Id);
            WFMessageBox.Show(result.Message, result.Success ? "Success" : "Validation",
                WFMessageBoxButtons.OK, result.Success ? WFMessageBoxIcon.Information : WFMessageBoxIcon.Warning);
            if (result.Success) await LoadUsersAsync();
        }

        private async Task DisableSelectedUserAsync()
        {
            var user = GetSelectedUser();
            if (user == null) { WFMessageBox.Show("Vui lòng chọn user cần disable.", "Info", WFMessageBoxButtons.OK, WFMessageBoxIcon.Information); return; }
            if (WFMessageBox.Show($"Disable user '{user.Username}'?", "Xác nhận", WFMessageBoxButtons.YesNo, WFMessageBoxIcon.Warning) != WFDialogResult.Yes) return;
            var result = await _service.DisableUserAsync(user.Id, CurrentUser.Id);
            WFMessageBox.Show(result.Message, result.Success ? "Success" : "Error", WFMessageBoxButtons.OK,
                result.Success ? WFMessageBoxIcon.Information : WFMessageBoxIcon.Error);
            if (result.Success) await LoadUsersAsync();
        }

        private async Task ResetPasswordAsync()
        {
            var user = GetSelectedUser();
            if (user == null) { WFMessageBox.Show("Vui lòng chọn user cần reset.", "Info", WFMessageBoxButtons.OK, WFMessageBoxIcon.Information); return; }
            if (WFMessageBox.Show($"Reset password '{user.Username}' về mặc định 123456?", "Xác nhận", WFMessageBoxButtons.YesNo, WFMessageBoxIcon.Question) != WFDialogResult.Yes) return;
            var result = await _service.ResetPasswordAsync(user.Id, CurrentUser.Id);
            WFMessageBox.Show(result.Message, result.Success ? "Success" : "Error", WFMessageBoxButtons.OK,
                result.Success ? WFMessageBoxIcon.Information : WFMessageBoxIcon.Error);
        }

        // ══════════════════════════════════════════════════════════════════
        //  GRID EVENTS
        // ══════════════════════════════════════════════════════════════════
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row  = _grid.Rows[e.RowIndex];
            if (row.DataBoundItem is not UserListItem item) return;

            if (e.RowIndex == _hoverRowIndex)
            {
                row.DefaultCellStyle.BackColor  = ClrHoverRow;
                row.DefaultCellStyle.ForeColor  = Color.FromArgb(30, 27, 75);
                return;
            }

            bool isActive   = string.Equals(item.TrangThai, "Active",   StringComparison.OrdinalIgnoreCase);
            bool isDisabled = string.Equals(item.TrangThai, "Disabled", StringComparison.OrdinalIgnoreCase);

            row.DefaultCellStyle.BackColor = isActive   ? Color.FromArgb(236, 253, 245)
                                           : isDisabled ? Color.FromArgb(255, 241, 242)
                                           : (e.RowIndex % 2 == 1 ? ClrAltRow : ClrSurface);
            row.DefaultCellStyle.ForeColor = isDisabled ? Color.FromArgb(156, 163, 175) : Color.FromArgb(17, 24, 39);
        }

        private void Grid_CellMouseEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            _hoverRowIndex = e.RowIndex;
            _grid.InvalidateRow(e.RowIndex);
        }

        private void Grid_CellMouseLeave(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            int old = _hoverRowIndex;
            _hoverRowIndex = -1;
            _grid.InvalidateRow(e.RowIndex);
            if (old >= 0 && old != e.RowIndex) _grid.InvalidateRow(old);
        }

        // ══════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════
        private static WFPanel MakeRoundedInputContainer(int width)
        {
            var p = new WFPanel
            {
                Size      = new Size(width, 44),
                BackColor = Color.FromArgb(249, 250, 251),
                Cursor    = WFCursors.IBeam
            };
            p.Paint += (s, e) =>
            {
                var g  = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(Color.FromArgb(209, 213, 219));
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 8);
                g.DrawPath(pen, path);
            };
            return p;
        }

        private static WFLabel MakeFilterLabel(string text) => new WFLabel
        {
            Text      = text,
            Font      = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(107, 114, 128),
            AutoSize  = true
        };

        private static void StyleButton(WFButton btn, string text, Color bg, EventHandler handler)
        {
            btn.Text      = text;
            btn.Size      = new Size(118, 40);
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize  = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bg, 0.2f);
            btn.Cursor    = WFCursors.Hand;
            btn.Font      = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            btn.Click    += handler;
        }

        private static void PaintCardShadow(object? sender, PaintEventArgs e)
        {
            if (sender is not WFPanel p) return;
            using var pen = new Pen(Color.FromArgb(229, 231, 235));
            e.Graphics.DrawRectangle(pen, new Rectangle(0, 0, p.Width - 1, p.Height - 1));
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d    = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
