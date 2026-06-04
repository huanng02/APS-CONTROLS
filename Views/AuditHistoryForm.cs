using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Security;
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
    public sealed class AuditHistoryForm : Form
    {
        // ── Services & State ────────────────────────────────────────────────
        private readonly AuditLogService _service = AuditLogService.Instance;
        private readonly BindingSource _binding = new();
        private int _currentPage = 1;
        private const int PageSize = 25;
        private int _totalPages = 1;
        private int _totalItems = 0;

        // ── UI Controls ──────────────────────────────────────────────────────
        private readonly DataGridView _grid = new();
        private readonly DateTimePicker _dtFrom = new();
        private readonly DateTimePicker _dtTo = new();
        private readonly WFTextBox _txtSearch = new();
        private readonly WFComboBox _cboActionType = new();
        private readonly WFComboBox _cboEntityType = new();
        private readonly WFButton _btnSearch = new();
        private readonly WFButton _btnClear = new();

        // Pagination Controls
        private readonly WFButton _btnPrev = new();
        private readonly WFButton _btnNext = new();
        private readonly WFLabel _lblPageStatus = new();

        // Detail View Controls (Right Panel)
        private readonly WFTextBox _txtDetailId = new();
        private readonly WFTextBox _txtDetailTime = new();
        private readonly WFTextBox _txtDetailUser = new();
        private readonly WFTextBox _txtDetailAction = new();
        private readonly WFTextBox _txtDetailEntity = new();
        private readonly WFTextBox _txtDetailDesc = new();
        private readonly WFTextBox _txtDetailOldValue = new();
        private readonly WFTextBox _txtDetailNewValue = new();

        // ── Color Palette ────────────────────────────────────────────────────
        private static readonly Color ClrBg = Color.FromArgb(9, 11, 20); // Deep space background
        private static readonly Color ClrSurface = Color.FromArgb(17, 24, 39); // Slate 900 card surface
        private static readonly Color ClrInputBg = Color.FromArgb(30, 41, 59); // Slate 800 inputs
        private static readonly Color ClrPrimary = Color.FromArgb(99, 102, 241); // Indigo 500
        private static readonly Color ClrText = Color.FromArgb(243, 244, 246); // Cool gray/white
        private static readonly Color ClrTextMuted = Color.FromArgb(156, 163, 175); // Muted slate gray
        private static readonly Color ClrBorder = Color.FromArgb(31, 41, 55); // Slate 800 borders
        private static readonly Color ClrHeaderBg = Color.FromArgb(15, 23, 42); // Slate 950 deep header

        public AuditHistoryForm()
        {
            // Security Enforcement: Block unauthorized constructor execution
            AuthorizationGuard.Protect("VIEW_AUDIT_LOG", "Access Audit History Screen");
            
            DoubleBuffered = true;
            InitializeComponent();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InitializeDropdowns();
            await LoadAuditLogsAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UI DESIGN LAYOUT
        // ══════════════════════════════════════════════════════════════════════
        private void InitializeComponent()
        {
            Text = "Nhật ký kiểm toán phân quyền (RBAC Audit Trail)";
            Size = new Size(1366, 780);
            MinimumSize = new Size(1024, 620);
            StartPosition = WFFormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            BackColor = ClrBg;

            // Build components
            var header = BuildTopHeader();
            var filterBar = BuildFilterBar();

            // Main Split Container (Left Grid / Right Detail card)
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Vertical,
                SplitterWidth = 8,
                BackColor = ClrBg,
                FixedPanel = FixedPanel.Panel2
            };

            // Left Side: Grid & Pagination Card
            var leftCard = BuildLeftCard();
            split.Panel1.Controls.Add(leftCard);

            // Right Side: Detail Card
            var rightCard = BuildRightCard();
            split.Panel2.Controls.Add(rightCard);

            // CRITICAL: WinForms dock order — add Fill FIRST, then Top panels
            // (last added Top panel goes highest, so header last)
            Controls.Add(split);
            Controls.Add(filterBar);
            Controls.Add(header);

            // Defer SplitterDistance to Shown event — control is fully sized by then
            Shown += (_, _) =>
            {
                try
                {
                    int targetDistance = split.Width - 380;
                    if (targetDistance < 100) targetDistance = split.Width * 2 / 3;
                    split.SplitterDistance = targetDistance;
                    split.Panel1MinSize = 400;
                    split.Panel2MinSize = 280;
                }
                catch { /* silently ignore if still too small */ }
            };
        }

        private WFControl BuildTopHeader()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                BackColor = ClrHeaderBg,
                Padding = new WFPadding(20, 0, 20, 0)
            };

            pnl.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, pnl.Width, pnl.Height);
                using (var brush = new LinearGradientBrush(rect, Color.FromArgb(79, 70, 229), ClrBg, 0F))
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
                Text = "📜 NHẬT KÝ KIỂM TOÁN HỆ THỐNG",
                Font = new Font("Segoe UI Semibold", 14.5f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(20, 12),
                BackColor = Color.Transparent
            };

            var lblSubtitle = new WFLabel
            {
                Text = "Theo dõi, kiểm tra, đối soát lịch sử phân quyền và chỉnh sửa bảo mật",
                Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(200, 220, 255),
                AutoSize = true,
                Location = new Point(22, 40),
                BackColor = Color.Transparent
            };

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblSubtitle);
            return pnl;
        }

        private WFControl BuildFilterBar()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = ClrSurface,
                Padding = new WFPadding(15, 10, 15, 10)
            };

            pnl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                using (var pen = new Pen(ClrBorder, 1))
                {
                    g.DrawLine(pen, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
                }
            };

            // Use a TableLayoutPanel for auto-reflowing filter controls
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 2,
                BackColor = ClrSurface,
                Padding = new WFPadding(0),
                Margin = new WFPadding(0)
            };

            // Column proportions: DateFrom, DateTo, Action, Entity, Search, BtnSearch, BtnClear
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105f));

            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Row 0: Labels
            var lblFrom = new WFLabel { Text = "Từ ngày:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ClrTextMuted, Dock = DockStyle.Fill };
            var lblTo = new WFLabel { Text = "Đến ngày:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ClrTextMuted, Dock = DockStyle.Fill };
            var lblAction = new WFLabel { Text = "Hành động:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ClrTextMuted, Dock = DockStyle.Fill };
            var lblEntity = new WFLabel { Text = "Loại thực thể:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ClrTextMuted, Dock = DockStyle.Fill };
            var lblSearch = new WFLabel { Text = "Tìm từ khóa:", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = ClrTextMuted, Dock = DockStyle.Fill };

            tbl.Controls.Add(lblFrom, 0, 0);
            tbl.Controls.Add(lblTo, 1, 0);
            tbl.Controls.Add(lblAction, 2, 0);
            tbl.Controls.Add(lblEntity, 3, 0);
            tbl.Controls.Add(lblSearch, 4, 0);

            // Row 1: Input controls
            _dtFrom.Format = DateTimePickerFormat.Custom;
            _dtFrom.CustomFormat = "dd/MM/yyyy HH:mm";
            _dtFrom.Value = DateTime.Today.AddDays(-7);
            _dtFrom.Dock = DockStyle.Fill;
            _dtFrom.Margin = new WFPadding(2);

            _dtTo.Format = DateTimePickerFormat.Custom;
            _dtTo.CustomFormat = "dd/MM/yyyy HH:mm";
            _dtTo.Value = DateTime.Now;
            _dtTo.Dock = DockStyle.Fill;
            _dtTo.Margin = new WFPadding(2);

            _cboActionType.DropDownStyle = WFComboBoxStyle.DropDownList;
            _cboActionType.Dock = DockStyle.Fill;
            _cboActionType.Margin = new WFPadding(2);
            _cboActionType.BackColor = ClrInputBg;
            _cboActionType.ForeColor = ClrText;
            _cboActionType.FlatStyle = FlatStyle.Flat;

            _cboEntityType.DropDownStyle = WFComboBoxStyle.DropDownList;
            _cboEntityType.Dock = DockStyle.Fill;
            _cboEntityType.Margin = new WFPadding(2);
            _cboEntityType.BackColor = ClrInputBg;
            _cboEntityType.ForeColor = ClrText;
            _cboEntityType.FlatStyle = FlatStyle.Flat;

            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.Margin = new WFPadding(2);
            _txtSearch.BackColor = ClrInputBg;
            _txtSearch.ForeColor = ClrText;
            _txtSearch.BorderStyle = BorderStyle.FixedSingle;

            StyleButton(_btnSearch, "🔍 Tìm kiếm", ClrPrimary, async (_, _) => {
                _currentPage = 1;
                await LoadAuditLogsAsync();
            }, new Point(0, 0), new Size(110, 30));
            _btnSearch.Dock = DockStyle.Fill;
            _btnSearch.Margin = new WFPadding(2);

            StyleButton(_btnClear, "↺ Xóa lọc", Color.FromArgb(71, 85, 105), async (_, _) => {
                _dtFrom.Value = DateTime.Today.AddDays(-7);
                _dtTo.Value = DateTime.Now;
                _cboActionType.SelectedIndex = 0;
                _cboEntityType.SelectedIndex = 0;
                _txtSearch.Text = string.Empty;
                _currentPage = 1;
                await LoadAuditLogsAsync();
            }, new Point(0, 0), new Size(100, 30));
            _btnClear.Dock = DockStyle.Fill;
            _btnClear.Margin = new WFPadding(2);

            tbl.Controls.Add(_dtFrom, 0, 1);
            tbl.Controls.Add(_dtTo, 1, 1);
            tbl.Controls.Add(_cboActionType, 2, 1);
            tbl.Controls.Add(_cboEntityType, 3, 1);
            tbl.Controls.Add(_txtSearch, 4, 1);
            tbl.Controls.Add(_btnSearch, 5, 1);
            tbl.Controls.Add(_btnClear, 6, 1);

            pnl.Controls.Add(tbl);
            return pnl;
        }

        private WFControl BuildLeftCard()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Fill,
                Padding = new WFPadding(15),
                BackColor = ClrBg
            };

            var mainCard = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrSurface,
                Padding = new WFPadding(15)
            };

            mainCard.Paint += (s, e) => PaintRoundedCard(mainCard, e, ClrSurface, ClrBorder, 8);

            // Grid setup
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.ReadOnly = true;
            _grid.BackgroundColor = ClrSurface;
            _grid.BorderStyle = BorderStyle.None;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(24, 33, 52); // Slate 800
            _grid.RowTemplate.Height = 36;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42); // Slate 950
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = ClrTextMuted;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _grid.DefaultCellStyle.BackColor = ClrSurface;
            _grid.DefaultCellStyle.ForeColor = ClrText;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
            _grid.DefaultCellStyle.SelectionForeColor = Color.White;
            _grid.GridColor = ClrBorder;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.EnableHeadersVisualStyles = false;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", DataPropertyName = "CreatedAt", HeaderText = "Thời gian", Width = 145, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Username", DataPropertyName = "Username", HeaderText = "Người thao tác", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ActionType", DataPropertyName = "ActionType", HeaderText = "Hành động", Width = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntityType", DataPropertyName = "EntityType", HeaderText = "Thực thể", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntityId", DataPropertyName = "EntityId", HeaderText = "Target ID", Width = 110 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "Mô tả chi tiết", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellFormatting += Grid_CellFormatting;

            // Pagination strip
            var pagStrip = new WFPanel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                BackColor = ClrSurface,
                Padding = new WFPadding(5)
            };

            StyleButton(_btnPrev, "🡄 Trang trước", Color.FromArgb(71, 85, 105), async (_, _) => {
                if (_currentPage > 1) {
                    _currentPage--;
                    await LoadAuditLogsAsync();
                }
            }, new Point(5, 8), new Size(110, 28));

            StyleButton(_btnNext, "Trang sau 🡆", Color.FromArgb(71, 85, 105), async (_, _) => {
                if (_currentPage < _totalPages) {
                    _currentPage++;
                    await LoadAuditLogsAsync();
                }
            }, new Point(125, 8), new Size(110, 28));

            _lblPageStatus.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            _lblPageStatus.ForeColor = ClrText;
            _lblPageStatus.AutoSize = true;
            _lblPageStatus.Location = new Point(250, 13);
            _lblPageStatus.Text = "Trang 1 / 1 (Tổng số: 0 dòng)";

            pagStrip.Controls.Add(_btnPrev);
            pagStrip.Controls.Add(_btnNext);
            pagStrip.Controls.Add(_lblPageStatus);

            mainCard.Controls.Add(_grid);
            mainCard.Controls.Add(pagStrip);
            pnl.Controls.Add(mainCard);

            return pnl;
        }

        private WFControl BuildRightCard()
        {
            var pnl = new WFPanel
            {
                Dock = DockStyle.Fill,
                Padding = new WFPadding(15),
                BackColor = ClrBg
            };

            var detailCard = new WFPanel
            {
                Dock = DockStyle.Fill,
                BackColor = ClrSurface,
                Padding = new WFPadding(15)
            };

            detailCard.Paint += (s, e) => PaintRoundedCard(detailCard, e, ClrSurface, ClrBorder, 8);

            var lblTitle = new WFLabel
            {
                Text = "🔍 CHI TIẾT SỰ KIỆN",
                Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
                ForeColor = ClrPrimary,
                AutoSize = true,
                Location = new Point(15, 12)
            };

            // Input fields panel (Scrollable)
            var body = new WFPanel
            {
                Location = new Point(15, 45),
                Size = new Size(420, 600),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = ClrSurface
            };

            int curY = 10;
            int hLabel = 18;
            int hText = 23;
            int spacing = 12;

            AddDetailField(body, "Mã Audit:", _txtDetailId, ref curY, hLabel, hText, spacing);
            AddDetailField(body, "Thời gian:", _txtDetailTime, ref curY, hLabel, hText, spacing);
            AddDetailField(body, "Người thao tác:", _txtDetailUser, ref curY, hLabel, hText, spacing);
            AddDetailField(body, "Hành động:", _txtDetailAction, ref curY, hLabel, hText, spacing);
            AddDetailField(body, "Loại thực thể:", _txtDetailEntity, ref curY, hLabel, hText, spacing);
            AddDetailField(body, "Mô tả chi tiết:", _txtDetailDesc, ref curY, hLabel, 45, spacing, isMultiline: true);
            AddDetailField(body, "Giá trị cũ (Old Value JSON):", _txtDetailOldValue, ref curY, hLabel, 150, spacing, isMultiline: true, isCode: true);
            AddDetailField(body, "Giá trị mới (New Value JSON):", _txtDetailNewValue, ref curY, hLabel, 150, spacing, isMultiline: true, isCode: true);

            detailCard.Controls.Add(lblTitle);
            detailCard.Controls.Add(body);
            pnl.Controls.Add(detailCard);

            return pnl;
        }

        private void AddDetailField(WFControl container, string title, WFTextBox txt, ref int y, int hLabel, int hText, int spacing, bool isMultiline = false, bool isCode = false)
        {
            var lbl = new WFLabel
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = ClrTextMuted,
                AutoSize = true,
                Location = new Point(5, y),
                BackColor = Color.Transparent
            };
            y += hLabel + 2;

            txt.Location = new Point(5, y);
            txt.Width = container.Width - 35;
            txt.Height = hText;
            txt.ReadOnly = true;
            txt.BackColor = isCode ? Color.FromArgb(15, 23, 42) : ClrInputBg;
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.ForeColor = isCode ? Color.FromArgb(52, 211, 153) : ClrText; // Terminal Emerald for JSON

            if (isMultiline)
            {
                txt.Multiline = true;
                txt.Height = hText;
                txt.ScrollBars = ScrollBars.Vertical;
            }
            if (isCode)
            {
                txt.Font = new Font("Consolas", 9.5f);
            }
            else
            {
                txt.Font = new Font("Segoe UI", 9.5f);
            }

            // Bind anchor
            txt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            container.Controls.Add(lbl);
            container.Controls.Add(txt);
            y += hText + spacing;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS & STYLING
        // ══════════════════════════════════════════════════════════════════════
        private void StyleButton(WFButton btn, string text, Color bg, EventHandler click, Point pos, Size sz)
        {
            btn.Text = text;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = LightenColor(bg, 15);
            btn.FlatAppearance.MouseDownBackColor = DarkenColor(bg, 10);
            btn.Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold);
            btn.Location = pos;
            btn.Size = sz;
            btn.Cursor = WFCursors.Hand;
            btn.Click += click;
        }

        private Color LightenColor(Color color, int percent)
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

        private Color DarkenColor(Color color, int percent)
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
            using (var path = GetRoundedRectPath(new Rectangle(0, 0, control.Width - 1, control.Height - 1), radius))
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

        private GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void InitializeDropdowns()
        {
            _cboActionType.Items.Clear();
            _cboActionType.Items.Add("Tất cả");
            _cboActionType.Items.Add("GRANT_PERMISSION");
            _cboActionType.Items.Add("REVOKE_PERMISSION");
            _cboActionType.SelectedIndex = 0;

            _cboEntityType.Items.Clear();
            _cboEntityType.Items.Add("Tất cả");
            _cboEntityType.Items.Add("RolePermission");
            _cboEntityType.Items.Add("User");
            _cboEntityType.Items.Add("Role");
            _cboEntityType.SelectedIndex = 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DATA OPERATIONS & EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════════
        private async Task LoadAuditLogsAsync()
        {
            try
            {
                UseWaitCursor = true;
                _btnSearch.Enabled = false;

                string? actionFilter = _cboActionType.SelectedItem?.ToString();
                if (actionFilter == "Tất cả") actionFilter = "All";

                string? entityFilter = _cboEntityType.SelectedItem?.ToString();
                if (entityFilter == "Tất cả") entityFilter = "All";

                DateTime from = _dtFrom.Value;
                DateTime to = _dtTo.Value;
                string search = _txtSearch.Text.Trim();

                var (items, totalCount) = await _service.GetAuditLogsPagedAsync(
                    _currentPage, PageSize, null, actionFilter, entityFilter, from, to, search
                );

                _totalItems = totalCount;
                _totalPages = (int)Math.Ceiling((double)_totalItems / PageSize);
                if (_totalPages < 1) _totalPages = 1;

                _binding.DataSource = items;
                _grid.DataSource = _binding;

                // Sync navigation button states
                _btnPrev.Enabled = _currentPage > 1;
                _btnNext.Enabled = _currentPage < _totalPages;
                _lblPageStatus.Text = $"Trang {_currentPage} / {_totalPages} (Tổng số: {_totalItems} dòng)";
            }
            catch (SecurityException secEx)
            {
                WFMessageBox.Show(this, "Không có quyền xem: " + secEx.Message, "Lỗi phân quyền", WFMessageBoxButtons.OK, WFMessageBoxIcon.Error);
                Close();
            }
            catch (Exception ex)
            {
                LoggingService.Instance.LogError("AUDIT_GRID_LOAD_FAILED", "AuditHistoryForm", "Failed to fetch paginated logs", ex);
                WFMessageBox.Show(this, "Lỗi tải lịch sử audit: " + ex.Message, "Lỗi kết nối", WFMessageBoxButtons.OK, WFMessageBoxIcon.Error);
            }
            finally
            {
                _btnSearch.Enabled = true;
                UseWaitCursor = false;
            }
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_grid.CurrentRow == null)
            {
                ClearDetails();
                return;
            }

            var log = _grid.CurrentRow.DataBoundItem as AuditLog;
            if (log == null)
            {
                ClearDetails();
                return;
            }

            _txtDetailId.Text = log.AuditLogId.ToString();
            _txtDetailTime.Text = log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            _txtDetailUser.Text = log.Username;
            _txtDetailAction.Text = log.ActionType;
            _txtDetailEntity.Text = log.EntityType;
            _txtDetailDesc.Text = log.Description;

            _txtDetailOldValue.Text = PrettyPrintJson(log.OldValue);
            _txtDetailNewValue.Text = PrettyPrintJson(log.NewValue);
        }

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            // Highlight Action Type column in modern color scheme
            if (_grid.Columns[e.ColumnIndex].Name == "ActionType" && e.Value != null)
            {
                string val = e.Value.ToString();
                if (val == "GRANT_PERMISSION")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(52, 211, 153); // Emerald 400
                    e.CellStyle.SelectionForeColor = Color.FromArgb(52, 211, 153);
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
                else if (val == "REVOKE_PERMISSION")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(248, 113, 113); // Rose/Red 400
                    e.CellStyle.SelectionForeColor = Color.FromArgb(248, 113, 113);
                    e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
                }
            }
        }

        private void ClearDetails()
        {
            _txtDetailId.Text = string.Empty;
            _txtDetailTime.Text = string.Empty;
            _txtDetailUser.Text = string.Empty;
            _txtDetailAction.Text = string.Empty;
            _txtDetailEntity.Text = string.Empty;
            _txtDetailDesc.Text = string.Empty;
            _txtDetailOldValue.Text = string.Empty;
            _txtDetailNewValue.Text = string.Empty;
        }

        private string PrettyPrintJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                }
            }
            catch
            {
                return json;
            }
        }
    }
}
