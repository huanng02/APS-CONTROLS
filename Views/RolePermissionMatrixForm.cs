using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using QuanLyGiuXe.ViewModels;
using WFTextBox = System.Windows.Forms.TextBox;
using WFButton = System.Windows.Forms.Button;
using WFLabel = System.Windows.Forms.Label;
using WFPanel = System.Windows.Forms.Panel;
using WFFlowLayoutPanel = System.Windows.Forms.FlowLayoutPanel;
using WFMessageBox = System.Windows.Forms.MessageBox;
using WFControl = System.Windows.Forms.Control;
using WFCursors = System.Windows.Forms.Cursors;

namespace QuanLyGiuXe.Views
{
    public sealed class RolePermissionMatrixForm : Form
    {
        // ── ViewModel (MVP/MVVM separation of concerns) ───────────────────
        private readonly PermissionMatrixViewModel _viewModel = new();
        private int _hoverRowIndex = -1;

        // ── Controls ─────────────────────────────────────────────────────
        private readonly DataGridView _grid = new();
        private readonly WFTextBox _txtSearch = new();
        private readonly WFButton _btnSave = new();
        private readonly WFButton _btnClose = new();
        private readonly WFLabel _lblStatus = new();

        // ── Palette Constants (Aligned with UserManagementForm) ───────────
        private static readonly Color ClrBg        = Color.FromArgb(248, 249, 252);
        private static readonly Color ClrSurface   = Color.White;
        private static readonly Color ClrPrimary   = Color.FromArgb(30, 79, 163);   // APSBlueColor
        private static readonly Color ClrSuccess   = Color.FromArgb(25, 135, 84);   // SuccessColor
        private static readonly Color ClrDanger    = Color.FromArgb(220, 53, 69);
        private static readonly Color ClrWarning   = Color.FromArgb(217, 119, 6);   // Amber-600 warning
        private static readonly Color ClrMuted     = Color.FromArgb(107, 114, 128); // TextMutedColor
        private static readonly Color ClrHeaderBg  = Color.FromArgb(30, 79, 163);
        private static readonly Color ClrAltRow    = Color.FromArgb(248, 249, 252);
        private static readonly Color ClrHoverRow  = Color.FromArgb(241, 245, 251);
        private static readonly Color ClrSelected  = Color.FromArgb(227, 236, 247);
        private static readonly Color ClrGridLines = Color.FromArgb(243, 244, 246);

        public RolePermissionMatrixForm()
        {
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadDataAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  UI BUILD
        // ══════════════════════════════════════════════════════════════════
        private void InitializeComponent()
        {
            Text = "Ma trận phân quyền hệ thống (RBAC)";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1366, 768);
            MinimumSize = new Size(1024, 600);
            Font = new Font("Segoe UI", 10);
            BackColor = ClrBg;

            // Enable double buffering for the form and child controls
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            var header = BuildTopHeader();
            var toolbar = BuildToolbar();
            var gridCard = BuildGridCard();

            Controls.Add(gridCard);    // Fill
            Controls.Add(toolbar);     // Top
            Controls.Add(header);      // Top
        }

        // ── Top Header Strip ──────────────────────────────────────────────
        private WFControl BuildTopHeader()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = ClrHeaderBg,
                Padding = new Padding(25, 0, 25, 0)
            };

            var lblTitle = new WFLabel
            {
                Text = "🛡️ Ma Trận Phân Quyền Hệ Thống (RBAC)",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(25, 16)
            };

            var lblSubtitle = new WFLabel
            {
                Text = "Nhấp trực tiếp vào ô tương ứng để cấp phát hoặc thu hồi quyền hạn đối với từng vai trò",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(208, 224, 240),
                AutoSize = true,
                Location = new Point(25, 46)
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblSubtitle);

            return pnl;
        }

        // ── Toolbar (Search & Buttons) ────────────────────────────────────
        private WFControl BuildToolbar()
        {
            var toolbar = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = ClrSurface,
                Padding = new Padding(20, 10, 20, 10)
            };

            toolbar.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(229, 231, 235));
                e.Graphics.DrawLine(pen, 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
            };

            // --- Left: Search & Status ---
            var searchContainer = MakeRoundedInputContainer(280);
            searchContainer.Location = new Point(20, 10);

            var searchIcon = new WFLabel
            {
                Text = "🔍",
                Font = new Font("Segoe UI", 10),
                Size = new Size(26, 36),
                Location = new Point(6, 4),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _txtSearch.PlaceholderText = "Tìm kiếm quyền...";
            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.Font = new Font("Segoe UI", 10);
            _txtSearch.Location = new Point(36, 10);
            _txtSearch.Size = new Size(230, 25);
            _txtSearch.TextChanged += (s, e) => FilterPermissions();

            searchContainer.Controls.Add(searchIcon);
            searchContainer.Controls.Add(_txtSearch);

            _lblStatus.Text = "Đang tải dữ liệu...";
            _lblStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Italic);
            _lblStatus.ForeColor = ClrMuted;
            _lblStatus.Location = new Point(320, 10);
            _lblStatus.Size = new Size(400, 36);
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;

            // --- Right: Action Buttons ---
            var pnlButtons = new WFFlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 450,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = ClrSurface,
                Padding = new Padding(0, 0, 0, 0)
            };

            StyleButton(_btnClose, "Đóng", ClrMuted, (s, e) => Close());
            StyleButton(_btnSave, "Lưu thay đổi", ClrSuccess, async (s, e) => await SaveChangesAsync());
            
            // Initialize save button to disabled read-only default style
            _btnSave.Enabled = false; 
            _btnSave.BackColor = Color.FromArgb(209, 213, 219);
            _btnSave.ForeColor = Color.FromArgb(156, 163, 175);
            _btnSave.Cursor = WFCursors.Default;

            pnlButtons.Controls.Add(_btnClose);
            pnlButtons.Controls.Add(_btnSave);

            toolbar.Controls.Add(searchContainer);
            toolbar.Controls.Add(_lblStatus);
            toolbar.Controls.Add(pnlButtons);

            return toolbar;
        }

        // ── Grid Card ─────────────────────────────────────────────────────
        private WFControl BuildGridCard()
        {
            var outer = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrBg,
                Padding = new Padding(20, 16, 20, 20)
            };

            var card = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrSurface,
                Padding = new Padding(0)
            };
            card.Paint += PaintCardShadow;

            // Double buffering configuration for the grid
            typeof(DataGridView).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_grid, true, null);

            _grid.Dock = DockStyle.Fill;
            _grid.BackgroundColor = ClrSurface;
            _grid.BorderStyle = BorderStyle.None;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.MultiSelect = false;
            _grid.ReadOnly = false; // Grid is editable
            _grid.EnableHeadersVisualStyles = false;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = ClrGridLines;

            // Header Style
            _grid.ColumnHeadersDefaultCellStyle.BackColor = ClrHeaderBg;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _grid.ColumnHeadersHeight = 44;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Row Style
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
            _grid.DefaultCellStyle.SelectionBackColor = ClrSelected;
            _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 27, 75);
            _grid.RowTemplate.Height = 38;

            // Register Grid Events
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellMouseEnter += Grid_CellMouseEnter;
            _grid.CellMouseLeave += Grid_CellMouseLeave;
            _grid.CellContentClick += Grid_CellContentClick;
            _grid.CellValueChanged += Grid_CellValueChanged;

            card.Controls.Add(_grid);
            outer.Controls.Add(card);
            return outer;
        }

        // ══════════════════════════════════════════════════════════════════
        //  DATA LOGIC
        // ══════════════════════════════════════════════════════════════════
        private async Task LoadDataAsync()
        {
            try
            {
                UseWaitCursor = true;
                _lblStatus.Text = "Đang tải Roles & Permissions...";

                await _viewModel.LoadDataAsync();

                SetupGridColumns();
                PopulateGridRows();

                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                WFMessageBox.Show($"Không thể tải ma trận phân quyền: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "Lỗi tải dữ liệu.";
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void SetupGridColumns()
        {
            _grid.Columns.Clear();

            // Fixed columns on the left (Set as ReadOnly explicitly)
            var colModule = new DataGridViewTextBoxColumn
            {
                Name = "colModule",
                HeaderText = "Phân hệ / Module",
                Width = 200,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Frozen = true
            };
            var colCode = new DataGridViewTextBoxColumn
            {
                Name = "colCode",
                HeaderText = "Mã Quyền (Code)",
                Width = 180,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Frozen = true
            };
            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Tên Quyền hạn",
                Width = 200,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                Frozen = true
            };
            var colDesc = new DataGridViewTextBoxColumn
            {
                Name = "colDescription",
                HeaderText = "Mô tả chức năng",
                Width = 300,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            _grid.Columns.AddRange(colModule, colCode, colName, colDesc);

            string currentRole = CurrentUserContext.Instance.Role;
            int currentLevel = PermissionMatrixService.GetRoleLevel(currentRole);

            // Dynamically generate role columns only for roles the user is permitted to edit
            foreach (var role in _viewModel.Roles)
            {
                int targetLevel = PermissionMatrixService.GetRoleLevel(role.Name);
                if (targetLevel >= currentLevel)
                {
                    continue; // Skip roles with equal/higher level or own role
                }

                var colRole = new DataGridViewCheckBoxColumn
                {
                    Name = $"colRole_{role.Id}",
                    HeaderText = role.Name,
                    Width = 110,
                    Tag = role,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Resizable = DataGridViewTriState.False,
                    ReadOnly = false, // Dynamic Role Columns are editable!
                    DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
                };
                _grid.Columns.Add(colRole);
            }
        }

        private void PopulateGridRows()
        {
            _grid.Rows.Clear();

            // Filter permission rows by search text
            var searchText = _txtSearch.Text.Trim();
            var filteredPerms = _viewModel.Permissions;
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredPerms = _viewModel.Permissions.Where(p =>
                    p.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Module.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            string currentRole = CurrentUserContext.Instance.Role;
            int currentLevel = PermissionMatrixService.GetRoleLevel(currentRole);

            // Populate rows
            foreach (var perm in filteredPerms)
            {
                int idx = _grid.Rows.Add();
                var row = _grid.Rows[idx];

                row.Cells["colModule"].Value = perm.Module;
                row.Cells["colCode"].Value = perm.Code;
                row.Cells["colName"].Value = perm.Name;
                row.Cells["colDescription"].Value = perm.Description;
                row.Tag = perm;

                // Setup checkbox cell values only for the visible role columns
                foreach (var role in _viewModel.Roles)
                {
                    int targetLevel = PermissionMatrixService.GetRoleLevel(role.Name);
                    if (targetLevel >= currentLevel)
                    {
                        continue;
                    }

                    string colKey = $"colRole_{role.Id}";
                    bool isAssigned = _viewModel.GetCurrentValue(role.Id, perm.Id);
                    var cell = row.Cells[colKey];
                    cell.Value = isAssigned;
                }
            }
        }

        private void FilterPermissions()
        {
            PopulateGridRows();
            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            int changesCount = _viewModel.GetChangeCount();
            if (changesCount > 0)
            {
                _lblStatus.Text = $"Có {changesCount} thay đổi chưa lưu.";
                _lblStatus.ForeColor = ClrWarning;
                _lblStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

                _btnSave.Enabled = true;
                _btnSave.BackColor = ClrSuccess;
                _btnSave.ForeColor = Color.White;
                _btnSave.Cursor = WFCursors.Hand;
            }
            else
            {
                _lblStatus.Text = $"Hiển thị {_grid.Rows.Count} quyền hạn hệ thống.";
                _lblStatus.ForeColor = ClrMuted;
                _lblStatus.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

                _btnSave.Enabled = false;
                _btnSave.BackColor = Color.FromArgb(209, 213, 219);
                _btnSave.ForeColor = Color.FromArgb(156, 163, 175);
                _btnSave.Cursor = WFCursors.Default;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  GRID EVENTS - INTERACTION & MODIFICATIONS
        // ══════════════════════════════════════════════════════════════════
        private void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 4) return;

            // Commit checkbox click immediately to trigger CellValueChanged
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 4) return;

            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is not PermissionMatrixItem perm) return;
            if (_grid.Columns[e.ColumnIndex].Tag is not RoleOption role) return;

            var cellVal = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
            bool newValue = cellVal is bool b && b;

            // Update state in ViewModel (Separation of View & Logic)
            _viewModel.TogglePermission(role.Id, role.Name, perm.Id, perm.Code, newValue);

            // Re-evaluate save button and count indicator
            UpdateStatusLabel();

            // Repaint modified cell immediately
            _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
        }

        private async Task SaveChangesAsync()
        {
            try
            {
                var changes = _viewModel.GetPendingChanges();
                if (!changes.Any())
                {
                    WFMessageBox.Show("Không có thay đổi nào cần lưu.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Prompt user for confirmation
                var confirmResult = WFMessageBox.Show(
                    $"Bạn có chắc chắn muốn lưu {changes.Count} thay đổi phân quyền này không?",
                    "Xác nhận lưu thay đổi",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirmResult != DialogResult.Yes) return;

                UseWaitCursor = true;
                _lblStatus.Text = "Đang lưu thay đổi vào cơ sở dữ liệu...";

                // Execute save via ViewModel under transaction
                await _viewModel.SaveChangesAsync();

                // Reload logged-in user permissions from DB (since role permissions have changed)
                await PermissionService.Instance.RefreshCurrentUserPermissionsAsync();

                // Re-evaluate WPF MainWindow menu/button visibility based on updated permissions
                if (System.Windows.Application.Current?.MainWindow is QuanLyGiuXe.MainWindow mainWin)
                {
                    mainWin.Dispatcher.Invoke(() => mainWin.ApplyPermissions());
                }

                WFMessageBox.Show("Đã lưu tất cả thay đổi phân quyền thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Reload Matrix from the newly synchronized state
                SetupGridColumns();
                PopulateGridRows();
                UpdateStatusLabel();
            }
            catch (Exception ex)
            {
                WFMessageBox.Show($"Lỗi khi lưu thay đổi phân quyền: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _lblStatus.Text = "Lưu thay đổi thất bại.";
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  GRID PAINTING & STYLING
        // ══════════════════════════════════════════════════════════════════
        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is not PermissionMatrixItem item) return;

            // 1. Row Selection/Hover Styling
            if (e.RowIndex == _hoverRowIndex)
            {
                e.CellStyle.BackColor = ClrHoverRow;
                return;
            }

            // 2. Visual grouping using module category colors
            Color moduleBg = GetModuleColor(item.Module);
            
            // Format column 0 (Module) to stand out like a category block
            if (e.ColumnIndex == 0)
            {
                e.CellStyle.BackColor = moduleBg;
                e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                e.CellStyle.ForeColor = Color.FromArgb(30, 41, 59); // Slate-800
                
                // Hide duplicate module labels to create a merged view
                if (e.RowIndex > 0)
                {
                    var prevRow = _grid.Rows[e.RowIndex - 1];
                    if (prevRow.Tag is PermissionMatrixItem prevItem && prevItem.Module == item.Module)
                    {
                        e.Value = string.Empty;
                        e.FormattingApplied = true;
                    }
                }
            }
            else if (e.ColumnIndex < 4)
            {
                // Soft background for left info columns
                e.CellStyle.BackColor = Color.FromArgb(253, 254, 255);
                e.CellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            }
            else
            {
                // Soft background for checkbox columns
                e.CellStyle.BackColor = Color.White;
                
                // Check if role is present to format cell modified states
                if (_grid.Columns[e.ColumnIndex].Tag is RoleOption role)
                {
                    var cell = row.Cells[e.ColumnIndex];
                    if (cell.ReadOnly)
                    {
                        // Grayed out styling for disabled security cells
                        e.CellStyle.BackColor = Color.FromArgb(241, 245, 249); // Slate-100
                        e.CellStyle.ForeColor = Color.FromArgb(148, 163, 184); // Slate-400
                        e.CellStyle.SelectionBackColor = Color.FromArgb(226, 232, 240); // Selected Gray
                    }
                    else if (_viewModel.HasChange(role.Id, item.Id))
                    {
                        // Yellow/Amber warning styling for modified cells
                        e.CellStyle.BackColor = Color.FromArgb(254, 240, 138); // Yellow-100/amber-200 tint
                        e.CellStyle.SelectionBackColor = Color.FromArgb(253, 224, 71); // Selected Amber
                        e.CellStyle.ForeColor = Color.FromArgb(133, 77, 14); // Brown-800 text
                    }
                    else
                    {
                        // Highlight original database assigned/checked permissions lightly
                        var cellVal = e.Value;
                        if (cellVal is bool b && b)
                        {
                            e.CellStyle.BackColor = Color.FromArgb(240, 253, 244); // Soft emerald green
                        }
                    }
                }
            }
        }

        private static Color GetModuleColor(string module)
        {
            switch (module)
            {
                case "Quản lý nhân viên & Phân quyền": return Color.FromArgb(236, 253, 245); // Emerald-50
                case "Quản lý Thẻ & Bảng giá":         return Color.FromArgb(239, 246, 255); // Blue-50
                case "Cấu hình & Phần cứng":         return Color.FromArgb(254, 243, 199); // Amber-50
                case "Báo cáo & Nhật ký":            return Color.FromArgb(245, 243, 255); // Violet-50
                case "Vận hành Làn xe":             return Color.FromArgb(255, 241, 242); // Rose-50
                case "Giám sát & Dashboard":          return Color.FromArgb(240, 253, 250); // Teal-50
                case "Tài chính & Soát vé":           return Color.FromArgb(253, 242, 248); // Pink-50
                case "Vận hành QA Panel":            return Color.FromArgb(254, 242, 242); // Red-50
                default:                             return Color.FromArgb(248, 250, 252);
            }
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
                Size      = new Size(width, 40),
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

        private static void StyleButton(WFButton btn, string text, Color bg, EventHandler handler)
        {
            btn.Text      = text;
            btn.Size      = new Size(130, 38);
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
