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
using WFPanelPaintEventArgs = System.Windows.Forms.PaintEventArgs;

namespace QuanLyGiuXe.Views
{
    public sealed class UserManagementForm : Form
    {
        // ── Services & State ──────────────────────────────────────────────
        private readonly UserManagementService _service = new();
        private readonly BindingSource _binding = new();
        private List<RoleOption> _roles = new();
        private CancellationTokenSource? _searchDebounceCts;
        private int _hoverRowIndex = -1;

        // ── UI Controls ───────────────────────────────────────────────────
        private readonly DataGridView _grid = new();
        private readonly WFLabel _lblTotalCount = new();
        private readonly WFTextBox _txtSearch = new();
        private readonly WFComboBox _cboRoleFilter = new();
        private readonly WFComboBox _cboStatusFilter = new();
        private readonly WFButton _btnAdd = new();
        private readonly WFButton _btnEdit = new();
        private readonly WFButton _btnDisable = new();
        private readonly WFButton _btnResetPassword = new();
        private readonly WFButton _btnRefresh = new();
        private readonly WFButton _btnPermissionMatrix = new();
        private readonly WFButton _btnAuditHistory = new();

        // ── Color Palette ─────────────────────────────────────────────────
        private static readonly Color ClrBg        = Color.FromArgb(9, 11, 20); // Cyber space dark background
        private static readonly Color ClrSurface   = Color.FromArgb(17, 24, 39); // Slate 900 card surface
        private static readonly Color ClrInputBg   = Color.FromArgb(30, 41, 59); // Slate 800 inputs
        private static readonly Color ClrPrimary   = Color.FromArgb(99, 102, 241); // Indigo 500
        private static readonly Color ClrSuccess   = Color.FromArgb(16, 185, 129); // Emerald 500
        private static readonly Color ClrDanger    = Color.FromArgb(239, 68, 68); // Rose 500
        private static readonly Color ClrWarning   = Color.FromArgb(245, 158, 11); // Amber 500
        private static readonly Color ClrText      = Color.FromArgb(243, 244, 246); // Cool gray/white
        private static readonly Color ClrTextMuted = Color.FromArgb(156, 163, 175); // Muted slate gray
        private static readonly Color ClrBorder    = Color.FromArgb(31, 41, 55); // Slate 800 borders
        private static readonly Color ClrHeaderBg  = Color.FromArgb(15, 23, 42); // Slate 950 deep header bg
        private static readonly Color ClrAltRow    = Color.FromArgb(24, 33, 52); // Alternating row color
        private static readonly Color ClrHoverRow  = Color.FromArgb(30, 41, 59); // Hover row color
        private static readonly Color ClrSelected  = Color.FromArgb(79, 70, 229); // Indigo 500 selection

        public UserManagementForm()
        {
            DoubleBuffered = true;
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
            Text = "Quản lý người dùng (User Management)";
            StartPosition = WFFormStartPosition.CenterScreen;
            Size = new Size(1366, 780);
            MinimumSize = new Size(1024, 620);
            Font = new Font("Segoe UI", 9.5f);
            BackColor = ClrBg;

            var header     = BuildTopHeader();
            var toolbar    = BuildToolbar();
            var gridCard   = BuildGridCard();

            // CRITICAL: WinForms dock order — add Fill FIRST, then Top panels
            // (last added Top panel appears highest)
            Controls.Add(gridCard);    // Fill
            Controls.Add(toolbar);     // Top
            Controls.Add(header);      // Top (topmost)
        }

        // ── top header strip ──────────────────────────────────────────────
        private WFControl BuildTopHeader()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = ClrHeaderBg,
                Padding = new Padding(25, 0, 25, 0)
            };

            pnl.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, pnl.Width, pnl.Height);
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(99, 102, 241), ClrBg, 0F))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
                using (var pen = new Pen(ClrBorder, 1))
                {
                    e.Graphics.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
                }
            };

            var lblTitle = new WFLabel
            {
                Text = "👤 QUẢN LÝ NGƯỜI DÙNG",
                Font = new Font("Segoe UI Semibold", 17, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 18),
                BackColor = Color.Transparent
            };

            var lblSubtitle = new WFLabel
            {
                Text = "Xem danh sách, chỉnh sửa thông tin, đặt lại mật khẩu và cấu hình phân quyền người dùng",
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 220, 255),
                AutoSize = true,
                Location = new Point(27, 50),
                BackColor = Color.Transparent
            };

            // Modern Pill Count Badge
            var pnlBadge = new WFPanel
            {
                Size = new Size(180, 50),
                BackColor = Color.Transparent
            };
            pnlBadge.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(new Rectangle(0, 0, pnlBadge.Width - 1, pnlBadge.Height - 1), 24))
                {
                    using (var brush = new SolidBrush(Color.FromArgb(30, 41, 59)))
                    {
                        g.FillPath(brush, path);
                    }
                    using (var pen = new Pen(ClrBorder, 1.5f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            };

            var lblText = new WFLabel
            {
                Text = "TỔNG SỐ USER:",
                ForeColor = ClrTextMuted,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(15, 18),
                BackColor = Color.Transparent
            };

            _lblTotalCount.Text = "0";
            _lblTotalCount.ForeColor = Color.White;
            _lblTotalCount.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _lblTotalCount.AutoSize = true;
            _lblTotalCount.Location = new Point(115, 11);
            _lblTotalCount.BackColor = Color.Transparent;

            pnlBadge.Controls.Add(lblText);
            pnlBadge.Controls.Add(_lblTotalCount);

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblSubtitle);
            pnl.Controls.Add(pnlBadge);

            // Align Badge horizontally on Resize
            void AlignBadge()
            {
                pnlBadge.Top = (pnl.Height - pnlBadge.Height) / 2;
                pnlBadge.Left = pnl.Width - pnlBadge.Width - 30;
            }

            pnl.Resize += (s, e) => AlignBadge();
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
                Padding = new WFPadding(0)
            };

            toolbar.Paint += (s, e) =>
            {
                using var pen = new Pen(ClrBorder);
                e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            // ROW 1: FILTERS (Top) — use TableLayoutPanel for reflow
            var pnlLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = ClrSurface,
                ColumnCount = 5,
                RowCount = 1,
                Padding = new WFPadding(10, 8, 10, 0),
                Margin = new WFPadding(0)
            };

            // Column proportions: Search, RoleLbl+Combo, StatusLbl+Combo
            pnlLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));  // Search
            pnlLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55f)); // Role label
            pnlLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));  // Role combo
            pnlLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65f)); // Status label
            pnlLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));  // Status combo

            pnlLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // SEARCH
            var searchContainer = MakeRoundedInputContainer(300);
            searchContainer.Dock = DockStyle.Fill;
            searchContainer.Margin = new WFPadding(2);

            var searchIcon = new WFLabel
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 10),
                Size = new Size(26, 36),
                Location = new Point(6, 4),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ClrTextMuted,
                BackColor = Color.Transparent
            };

            _txtSearch.PlaceholderText = "Tìm tên hoặc username...";
            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.Font = new Font("Segoe UI", 10);
            _txtSearch.Location = new Point(36, 10);
            _txtSearch.Size = new Size(240, 25);
            _txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtSearch.BackColor = ClrInputBg;
            _txtSearch.ForeColor = ClrText;

            _txtSearch.TextChanged += async (_, _) =>
                await DebouncedSearchAsync();

            searchContainer.Controls.Add(searchIcon);
            searchContainer.Controls.Add(_txtSearch);

            // ROLE LABEL
            var lblRole = MakeFilterLabel("Vai trò");
            lblRole.Dock = DockStyle.Fill;
            lblRole.TextAlign = ContentAlignment.MiddleRight;
            lblRole.Margin = new WFPadding(2);

            // ROLE COMBO
            _cboRoleFilter.Dock = DockStyle.Fill;
            _cboRoleFilter.Margin = new WFPadding(2);
            _cboRoleFilter.DropDownStyle = WFComboBoxStyle.DropDownList;
            _cboRoleFilter.FlatStyle = FlatStyle.Flat;
            _cboRoleFilter.Font = new Font("Segoe UI", 10);
            _cboRoleFilter.BackColor = ClrInputBg;
            _cboRoleFilter.ForeColor = ClrText;

            _cboRoleFilter.SelectedIndexChanged += async (_, _) =>
                await LoadUsersAsync();

            // STATUS LABEL
            var lblStatus = MakeFilterLabel("Trạng thái");
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.TextAlign = ContentAlignment.MiddleRight;
            lblStatus.Margin = new WFPadding(2);

            // STATUS COMBO
            _cboStatusFilter.Dock = DockStyle.Fill;
            _cboStatusFilter.Margin = new WFPadding(2);
            _cboStatusFilter.DropDownStyle = WFComboBoxStyle.DropDownList;
            _cboStatusFilter.FlatStyle = FlatStyle.Flat;
            _cboStatusFilter.Font = new Font("Segoe UI", 10);
            _cboStatusFilter.BackColor = ClrInputBg;
            _cboStatusFilter.ForeColor = ClrText;

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

            pnlLeft.Controls.Add(searchContainer, 0, 0);
            pnlLeft.Controls.Add(lblRole, 1, 0);
            pnlLeft.Controls.Add(_cboRoleFilter, 2, 0);
            pnlLeft.Controls.Add(lblStatus, 3, 0);
            pnlLeft.Controls.Add(_cboStatusFilter, 4, 0);

            // ROW 2: ACTIONS (Bottom)
            var pnlButtons = new WFFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                BackColor = ClrSurface,
                Padding = new WFPadding(20, 8, 20, 0)
            };

            StyleButton(_btnAdd, "＋ Thêm", ClrPrimary,
                async (_, _) => await AddUserAsync());

            StyleButton(_btnEdit, "✏ Sửa", ClrSuccess,
                async (_, _) => await EditSelectedUserAsync());

            StyleButton(_btnDisable, "⊘ Disable", ClrDanger,
                async (_, _) => await DisableSelectedUserAsync());

            StyleButton(_btnResetPassword, "🔑 Reset", ClrWarning,
                async (_, _) => await ResetPasswordAsync());

            StyleButton(_btnPermissionMatrix, "🛡 Ma trận quyền",
                Color.FromArgb(79, 70, 229),
                (_, _) => OpenPermissionMatrix(),
                width: 140);

            StyleButton(_btnAuditHistory, "📜 Nhật ký Audit",
                Color.FromArgb(59, 130, 246),
                (_, _) => OpenAuditHistory(),
                width: 135);

            StyleButton(_btnRefresh, "↺ Refresh",
                ClrTextMuted,
                async (_, _) => await LoadUsersAsync());

            pnlButtons.Controls.Add(_btnAdd);
            pnlButtons.Controls.Add(_btnEdit);
            pnlButtons.Controls.Add(_btnDisable);
            pnlButtons.Controls.Add(_btnResetPassword);
            pnlButtons.Controls.Add(_btnPermissionMatrix);
            
            if (PermissionService.Instance.CheckPermission("VIEW_AUDIT_LOG"))
            {
                pnlButtons.Controls.Add(_btnAuditHistory);
            }
            
            pnlButtons.Controls.Add(_btnRefresh);

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
                Padding = new WFPadding(15)
            };
            card.Paint += (s, e) => PaintRoundedCard(card, e, ClrSurface, ClrBorder, 8);

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
            _grid.GridColor = ClrBorder;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            // header style
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42); // Deep slate 950 header
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = ClrTextMuted;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new WFPadding(8, 0, 0, 0);
            _grid.ColumnHeadersHeight = 44;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // row style
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            _grid.DefaultCellStyle.Padding = new WFPadding(8, 0, 0, 0);
            _grid.DefaultCellStyle.SelectionBackColor = ClrSelected;
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.RowTemplate.Height = 40;

            // ── columns ──
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
        //  DATA LOGIC
        // ══════════════════════════════════════════════════════════════════
        private async Task InitializeDataAsync()
        {
            try
            {
                var allRoles = await _service.GetRolesAsync();
                string currentRoleName = CurrentUserContext.Instance.Role;
                int currentLevel = PermissionMatrixService.GetRoleLevel(currentRoleName);

                // Filter roles strictly lower than current user's level
                _roles = allRoles.Where(r => PermissionMatrixService.GetRoleLevel(r.Name) < currentLevel).ToList();

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

                string currentRoleName = CurrentUserContext.Instance.Role;
                int currentLevel = PermissionMatrixService.GetRoleLevel(currentRoleName);

                // Filter users strictly lower than current user's level
                var filteredUsers = users.Where(u => PermissionMatrixService.GetRoleLevel(u.RoleName) < currentLevel).ToList();

                _binding.DataSource     = filteredUsers;
                _lblTotalCount.Text     = filteredUsers.Count.ToString();
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

        private void OpenPermissionMatrix()
        {
            using (var frm = new RolePermissionMatrixForm())
            {
                frm.ShowDialog(this);
            }
        }

        private void OpenAuditHistory()
        {
            using (var frm = new AuditHistoryForm())
            {
                frm.ShowDialog(this);
            }
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
                row.DefaultCellStyle.ForeColor  = Color.White;
                return;
            }

            bool isActive   = string.Equals(item.TrangThai, "Active",   StringComparison.OrdinalIgnoreCase);
            bool isDisabled = string.Equals(item.TrangThai, "Disabled", StringComparison.OrdinalIgnoreCase);

            row.DefaultCellStyle.BackColor = isActive   ? Color.FromArgb(12, 38, 28) // Subtle Dark Green
                                           : isDisabled ? Color.FromArgb(38, 12, 12) // Subtle Dark Red
                                           : (e.RowIndex % 2 == 1 ? ClrAltRow : ClrSurface);
            row.DefaultCellStyle.ForeColor = isActive   ? Color.FromArgb(52, 211, 153) // Emerald text
                                           : isDisabled ? Color.FromArgb(248, 113, 113) // Rose text
                                           : ClrText;
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
                BackColor = ClrInputBg,
                Cursor    = WFCursors.IBeam
            };
            p.Paint += (s, e) =>
            {
                var g  = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(ClrBorder);
                using var path = RoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 6);
                g.DrawPath(pen, path);
            };
            return p;
        }

        private static WFLabel MakeFilterLabel(string text) => new WFLabel
        {
            Text      = text,
            Font      = new Font("Segoe UI", 9f),
            ForeColor = ClrTextMuted,
            AutoSize  = true,
            BackColor = Color.Transparent
        };

        private static void StyleButton(WFButton btn, string text, Color bg, EventHandler handler, int width = 118)
        {
            btn.Text      = text;
            btn.Size      = new Size(width, 40);
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize  = 0;
            btn.FlatAppearance.MouseOverBackColor = LightenColor(bg, 15);
            btn.FlatAppearance.MouseDownBackColor = DarkenColor(bg, 10);
            btn.Cursor    = WFCursors.Hand;
            btn.Font      = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            btn.Click    += handler;
        }

        private static Color LightenColor(Color color, int percent)
        {
            int r = color.R + (255 - color.R) * percent / 100;
            int g = color.G + (255 - color.G) * percent / 100;
            int b = color.B + (255 - color.B) * percent / 100;
            return Color.FromArgb(
                color.A,
                r > 255 ? 255 : r,
                g > 255 ? 255 : g,
                b > 255 ? 255 : b
            );
        }

        private static Color DarkenColor(Color color, int percent)
        {
            int r = color.R - color.R * percent / 100;
            int g = color.G - color.G * percent / 100;
            int b = color.B - color.B * percent / 100;
            return Color.FromArgb(
                color.A,
                r < 0 ? 0 : r,
                g < 0 ? 0 : g,
                b < 0 ? 0 : b
            );
        }

        private void PaintRoundedCard(object? sender, PaintEventArgs e, Color bg, Color border, int radius)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var control = (WFControl)sender!;
            using (var path = RoundedRect(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius))
            {
                using (var brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(border, 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }
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
