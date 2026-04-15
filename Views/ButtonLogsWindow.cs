using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using QuanLyGiuXe.Models;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using ClosedXML.Excel;
// QuestPDF will be referenced with fully-qualified names to avoid symbol conflicts with WPF
using System.Text;
using System.Reflection;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public class ButtonLogsWindow : Window
    {
        private DatePicker _dpFrom;
        private DatePicker _dpTo;
        private TextBox _txtFilter;
        private ComboBox _cbDoor;
        private DataGrid _dgLogs;
        private System.Windows.Controls.Image _imgFull;
        private System.Windows.Controls.Image _imgPlate;
        // detail fields
        private TextBox _tbId;
        private TextBox _tbTimestamp;
        private TextBox _tbDoor;
        private TextBox _tbEvent;
        // removed _tbInOut (column not present in DB)
        private TextBox _tbCard;
        private TextBox _tbPin;
        private TextBox _tbAction;
        private TextBox _tbBarrier;
        private TextBox _tbOperator;
        private TextBox _tbSourceIp;
        private TextBox _tbPlatePath;
        private TextBox _tbFullPath;
        private Button _btnOpenPlate;
        private Button _btnOpenFull;
        private Button _btnRefresh;
        private TextBox _txtRaw;
        private TextBox _txtNotes;
        private bool _firstLoad = true;
        private bool _suppressAutoRefresh = false;

        private ObservableCollection<ButtonPressLog> _logs = new();
        private int _lastMaxId = 0;
        private bool _pendingRefresh = false;
        private System.Timers.Timer? _refreshTimer;
        private int _currentPageSize = 1000;
        private readonly int _pageIncrement = 1000;
        private readonly int _batchSize = 100;
        private Button _btnPrev;
        private Button _btnNext;
        private TextBlock _lblPageInfo;
        private int _currentPageIndex = 0;
        private int _totalCount = 0;
        private int _totalPages = 1;
        private ComboBox _cbPageSize;

        private readonly string _defaultImagePath;

        public ButtonLogsWindow()
        {
            Title = "Lịch sử nhấn nút";
            Width = 1000;
            Height = 640;

            _defaultImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "default_button.png");

            var root = new Grid { Margin = new Thickness(8) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // filters
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,8) };
            sp.Children.Add(new TextBlock { Text = "Từ:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,4,0) });
            _dpFrom = new DatePicker { Width = 140, SelectedDate = DateTime.Today };
            sp.Children.Add(_dpFrom);
            sp.Children.Add(new TextBlock { Text = "Đến:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,4,0) });
            _dpTo = new DatePicker { Width = 140, SelectedDate = DateTime.Today };
            sp.Children.Add(_dpTo);
            sp.Children.Add(new TextBlock { Text = "Tìm:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,4,0) });
            _txtFilter = new TextBox { Width = 200 };
            sp.Children.Add(_txtFilter);
            sp.Children.Add(new TextBlock { Text = "Cổng:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,4,0) });
            _cbDoor = new ComboBox { Width = 80 };
            _cbDoor.Items.Add("All");
            _cbDoor.Items.Add("1");
            _cbDoor.Items.Add("2");
            _cbDoor.SelectedIndex = 0;
            sp.Children.Add(_cbDoor);
            var btnFind = new Button { Content = "Tìm", Margin = new Thickness(12,0,0,0), Padding = new Thickness(8,4,8,4) };
            btnFind.Click += async (s,e) =>
            {
                _currentPageIndex = 0;
                if (this.DataContext is ViewModels.ButtonLogsViewModel vm)
                {
                    _totalCount = await vm.GetTotalCount(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null);
                    _totalPages = (_currentPageSize <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling((double)_totalCount / _currentPageSize));
                    await vm.LoadLogs(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null, pageIndex: 0, pageSize: _currentPageSize, batchSize: _batchSize);
                    _btnNext.IsEnabled = _currentPageIndex < (_totalPages - 1);
                    UpdatePagingControls();
                }
            };

            sp.Children.Add(btnFind);

            // Export Excel button
            var btnExportExcel = new Button { Content = "Export Excel", Margin = new Thickness(8,0,0,0), Padding = new Thickness(8,4,8,4) };
            btnExportExcel.Click += async (s, e) =>
            {
                // collect current filter values first so suggested filename can reflect them
                DateTime from = _dpFrom.SelectedDate?.Date ?? DateTime.Today;
                DateTime to = _dpTo.SelectedDate?.Date.AddDays(1).AddSeconds(-1) ?? DateTime.Today.AddDays(1).AddSeconds(-1);
                string? filter = string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim();
                int? door = null;
                if (_cbDoor.SelectedIndex == 1) door = 1;
                if (_cbDoor.SelectedIndex == 2) door = 2;

                // build a safe filename based on filters
                string MakeSafe(string s)
                {
                    if (string.IsNullOrEmpty(s)) return string.Empty;
                    var invalid = Path.GetInvalidFileNameChars();
                    foreach (var c in invalid) s = s.Replace(c, '_');
                    if (s.Length > 50) s = s.Substring(0, 50);
                    return s;
                }

                string suggested = $"ButtonLogs_{from:yyyyMMdd}_{to:yyyyMMdd}";
                if (door.HasValue) suggested += $"_Door{door.Value}";
                if (!string.IsNullOrEmpty(filter)) suggested += "_" + MakeSafe(filter);

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel Workbook|*.xlsx",
                    FileName = suggested + ".xlsx"
                };
                if (dialog.ShowDialog() != true) return;

                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var db = new DatabaseService();
                        string conn = db.GetConnectionString();
                        using (var connSql = new SqlConnection(conn))
                        {
                            connSql.Open();
                            string q = @"SELECT Id, Timestamp, Door, EventType, CardNo, Action, Pin, BarrierResult, PlateImagePath, FullImagePath, Operator, SourceIp, RawData, Notes
                                         FROM dbo.ButtonPressLog
                                         WHERE Timestamp BETWEEN @from AND @to";
                            if (!string.IsNullOrWhiteSpace(filter)) q += " AND (CardNo LIKE @filter OR Action LIKE @filter)";
                            if (door.HasValue) q += " AND Door = @door";
                            q += " ORDER BY Timestamp DESC";

                            using (var cmd = new SqlCommand(q, connSql))
                            {
                                cmd.Parameters.AddWithValue("@from", from);
                                cmd.Parameters.AddWithValue("@to", to);
                                if (!string.IsNullOrWhiteSpace(filter)) cmd.Parameters.AddWithValue("@filter", "%" + filter + "%");
                                if (door.HasValue) cmd.Parameters.AddWithValue("@door", door.Value);

                                using (var rdr = cmd.ExecuteReader())
                                using (var wb = new XLWorkbook())
                                {
                                    var ws = wb.Worksheets.Add("Logs");
                                    // Header
                                    ws.Cell(1, 1).Value = "Id";
                                    ws.Cell(1, 2).Value = "Thời gian";
                                    ws.Cell(1, 3).Value = "Cổng";
                                    ws.Cell(1, 4).Value = "Event";
                                    ws.Cell(1, 5).Value = "Card";
                                    ws.Cell(1, 6).Value = "Action";
                                    ws.Cell(1, 7).Value = "Barrier";
                                    ws.Cell(1, 8).Value = "Pin";
                                    ws.Cell(1, 9).Value = "Operator";
                                    ws.Cell(1,10).Value = "Source IP";
                                    ws.Cell(1,11).Value = "PlateImagePath";
                                    ws.Cell(1,12).Value = "FullImagePath";
                                    ws.Cell(1,13).Value = "RawData";
                                    ws.Cell(1,14).Value = "Notes";

                                    int row = 2;
                                    while (rdr.Read())
                                    {
                                        // Use SetValue to avoid type conversion issues with ClosedXML
                                        if (rdr.IsDBNull(rdr.GetOrdinal("Id"))) ws.Cell(row, 1).Value = string.Empty;
                                        else ws.Cell(row, 1).SetValue(rdr.GetInt32(rdr.GetOrdinal("Id")));

                                        if (rdr.IsDBNull(rdr.GetOrdinal("Timestamp"))) ws.Cell(row, 2).Value = string.Empty;
                                        else ws.Cell(row, 2).SetValue(rdr.GetDateTime(rdr.GetOrdinal("Timestamp")));

                                        if (rdr.IsDBNull(rdr.GetOrdinal("Door"))) ws.Cell(row, 3).Value = string.Empty;
                                        else ws.Cell(row, 3).SetValue(Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Door"))));

                                        if (rdr.IsDBNull(rdr.GetOrdinal("EventType"))) ws.Cell(row, 4).Value = string.Empty;
                                        else ws.Cell(row, 4).SetValue(rdr.GetInt32(rdr.GetOrdinal("EventType")));

                                        ws.Cell(row, 5).SetValue(rdr.IsDBNull(rdr.GetOrdinal("CardNo")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("CardNo")));
                                        ws.Cell(row, 6).SetValue(rdr.IsDBNull(rdr.GetOrdinal("Action")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Action")));

                                        if (rdr.IsDBNull(rdr.GetOrdinal("BarrierResult"))) ws.Cell(row, 7).Value = string.Empty;
                                        else ws.Cell(row, 7).SetValue(Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("BarrierResult"))));

                                        if (rdr.IsDBNull(rdr.GetOrdinal("Pin"))) ws.Cell(row, 8).Value = string.Empty;
                                        else ws.Cell(row, 8).SetValue(Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Pin"))));

                                        ws.Cell(row, 9).SetValue(rdr.IsDBNull(rdr.GetOrdinal("Operator")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Operator")));
                                        ws.Cell(row,10).SetValue(rdr.IsDBNull(rdr.GetOrdinal("SourceIp")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("SourceIp")));
                                        ws.Cell(row,11).SetValue(rdr.IsDBNull(rdr.GetOrdinal("PlateImagePath")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("PlateImagePath")));
                                        ws.Cell(row,12).SetValue(rdr.IsDBNull(rdr.GetOrdinal("FullImagePath")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("FullImagePath")));
                                        ws.Cell(row,13).SetValue(rdr.IsDBNull(rdr.GetOrdinal("RawData")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("RawData")));
                                        ws.Cell(row,14).SetValue(rdr.IsDBNull(rdr.GetOrdinal("Notes")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Notes")));

                                        // no image embedding for bulk export

                                        row++;
                                    }

                                    ws.Columns().AdjustToContents();
                                    // Save workbook to a temp file first, then copy to target to provide a clearer error if the
                                    // destination is locked by another process (e.g. Excel has it open).
                                    var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xlsx");
                                    wb.SaveAs(tempPath);
                                    try
                                    {
                                        File.Copy(tempPath, dialog.FileName, true);
                                    }
                                    catch (IOException ioex)
                                    {
                                        // wrap and rethrow so outer catch shows a friendly message
                                        throw new IOException($"Cannot write to '{dialog.FileName}'. It may be open in another program. Close it and try again.\n{ioex.Message}", ioex);
                                    }
                                    finally
                                    {
                                        try { File.Delete(tempPath); } catch { }
                                    }
                                }
                            }
                        }
                    });

                    MessageBox.Show("Export thành công!", "Excel Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export thất bại: " + ex.Message, "Excel Export", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    try { Mouse.OverrideCursor = null; } catch { }
                }
            };
            sp.Children.Add(btnExportExcel);

            // PDF export removed; Excel export supports embedded images now
            // page size selector moved below the table
            // page size selector moved below the table
            _btnPrev = new Button { Content = "Trang trước", Margin = new Thickness(8,0,0,0), Padding = new Thickness(8,4,8,4), IsEnabled = false };
            _btnPrev.Click += async (s, e) =>
            {
                if (_currentPageIndex <= 0) return;
                _currentPageIndex--;
                if (this.DataContext is ViewModels.ButtonLogsViewModel vm)
                {
                    var fetched = await vm.LoadLogs(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null, pageIndex: _currentPageIndex, pageSize: _currentPageSize, batchSize: _batchSize);
                    // update next enabled based on fetched vs page size
                    _btnNext.IsEnabled = fetched >= _currentPageSize;
                    UpdatePagingControls();
                }
            };

            _btnNext = new Button { Content = "Trang sau", Margin = new Thickness(8,0,0,0), Padding = new Thickness(8,4,8,4) };
            _btnNext.Click += async (s, e) =>
            {
                _currentPageIndex++; // Increment the current page index
                if (this.DataContext is ViewModels.ButtonLogsViewModel vm)
                {
                    var fetched = await vm.LoadLogs(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null, pageIndex: _currentPageIndex, pageSize: _currentPageSize, batchSize: _batchSize);
                    // update cached total and pages (call once)
                    _totalCount = await vm.GetTotalCount(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null);
                    _totalPages = (_currentPageSize <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling((double)_totalCount / _currentPageSize));
                    _btnNext.IsEnabled = _currentPageIndex < (_totalPages - 1);
                    UpdatePagingControls();
                }
            };

            _lblPageInfo = new TextBlock { Text = "Trang 1", VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(8,0,0,0) };
            _btnRefresh = new Button { Content = "Làm mới", Margin = new Thickness(8,0,0,0), Padding = new Thickness(8,4,8,4), ToolTip = "Làm mới danh sách" };
            _btnRefresh.Click += async (s,e) =>
            {
                _currentPageSize = _pageIncrement; // reset
                _currentPageIndex = 0;
                if (this.DataContext is ViewModels.ButtonLogsViewModel vm)
                {
                    var fetched = await vm.LoadLogs(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null, pageIndex: 0, pageSize: _currentPageSize, batchSize: _batchSize);
                    _btnNext.IsEnabled = fetched >= _currentPageSize;
                    UpdatePagingControls();
                }
            };
            // detail open button removed — double-click on a row opens details

            root.Children.Add(sp);
            Grid.SetRow(sp, 0);

            // main grid — left: list, right: detail (fixed narrow column)
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // increase detail pane by ~50%
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(570) });
            // left column: data grid (star) + paging (auto)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _dgLogs = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                SelectionMode = DataGridSelectionMode.Single,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Margin = new Thickness(0),
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeaderWidth = 0,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };
            // enable virtualization/recycling for performance with many rows
            _dgLogs.EnableRowVirtualization = true;
            _dgLogs.EnableColumnVirtualization = true;
            VirtualizingPanel.SetIsVirtualizing(_dgLogs, true);
            VirtualizingPanel.SetVirtualizationMode(_dgLogs, VirtualizationMode.Recycling);
            // bind items & selection to viewmodel
            _dgLogs.SetBinding(DataGrid.ItemsSourceProperty, new System.Windows.Data.Binding("Logs"));
            _dgLogs.SetBinding(DataGrid.SelectedItemProperty, new System.Windows.Data.Binding("SelectedLog") { Mode = System.Windows.Data.BindingMode.TwoWay });
            // highlight rows for Door == 2 so they are easy to spot
            try
            {
                var rowStyle = new Style(typeof(DataGridRow));
                var trigger = new DataTrigger
                {
                    Binding = new System.Windows.Data.Binding("Door"),
                    // Door is stored as byte? ensure comparison uses same boxed type
                    Value = (byte)2
                };
                trigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.ExtraBold));
                trigger.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.LightGoldenrodYellow));
                trigger.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.Black));
                // slightly larger font
                trigger.Setters.Add(new Setter(Control.FontSizeProperty, 13.0));
                rowStyle.Triggers.Add(trigger);
                _dgLogs.RowStyle = rowStyle;
            }
            catch { }

            _dgLogs.SelectionChanged += (s,e) => OnSelectionChanged();
            _dgLogs.MouseDoubleClick += (s,e) => OpenSelectedDetail();

            // images will be created later in the detail pane

            _dgLogs.Columns.Add(new DataGridTextColumn { Header = "Thời gian", Binding = new System.Windows.Data.Binding("Timestamp"), Width = 180 });
            _dgLogs.Columns.Add(new DataGridTextColumn { Header = "Door", Binding = new System.Windows.Data.Binding("Door"), Width = 60 });
            _dgLogs.Columns.Add(new DataGridTextColumn { Header = "Event", Binding = new System.Windows.Data.Binding("EventType"), Width = 80 });
            _dgLogs.Columns.Add(new DataGridTextColumn { Header = "Card", Binding = new System.Windows.Data.Binding("CardNo"), Width = 140 });
            _dgLogs.Columns.Add(new DataGridTextColumn { Header = "Hành động", Binding = new System.Windows.Data.Binding("Action"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            _dgLogs.Columns.Add(new DataGridTextColumn { Header = "Barrier", Binding = new System.Windows.Data.Binding("BarrierResult"), Width = 70 });
            // Plate image column removed — images shown only in detail pane

            // Adding DataGrid to the grid
            grid.Children.Add(_dgLogs);
            Grid.SetColumn(_dgLogs, 0);
            Grid.SetRow(_dgLogs, 0);

            // paging controls container (below the DataGrid)
            var pagingPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,8,0,8), HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            // page size selector
            pagingPanel.Children.Add(new TextBlock { Text = "Kích thước trang:", VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(0,0,4,0) });
            _cbPageSize = new ComboBox { Width = 100 };
            _cbPageSize.Items.Add("50");
            _cbPageSize.Items.Add("100");
            _cbPageSize.Items.Add("250");
            _cbPageSize.Items.Add("500");
            _cbPageSize.Items.Add("Vô hạn");
            _cbPageSize.SelectedIndex = 1; // default 100
            _cbPageSize.SelectionChanged += async (s, e) =>
            {
                var sel = _cbPageSize.SelectedItem as string;
                if (sel == "Vô hạn") _currentPageSize = 0;
                else if (int.TryParse(sel, out var v)) _currentPageSize = v;

                // reload from first page using new page size
                _currentPageIndex = 0;
                if (this.DataContext is ViewModels.ButtonLogsViewModel vm)
                {
                    _totalCount = await vm.GetTotalCount(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null);
                    _totalPages = (_currentPageSize <= 0) ? 1 : Math.Max(1, (int)Math.Ceiling((double)_totalCount / _currentPageSize));
                    await vm.LoadLogs(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null, pageIndex: 0, pageSize: _currentPageSize, batchSize: _batchSize);
                    _btnNext.IsEnabled = _currentPageIndex < (_totalPages - 1);
                    UpdatePagingControls();
                }
            };
            pagingPanel.Children.Add(_cbPageSize);

            pagingPanel.Children.Add(_btnPrev);
            pagingPanel.Children.Add(_btnNext);
            pagingPanel.Children.Add(_lblPageInfo);
            pagingPanel.Children.Add(_btnRefresh);

            // place pagingPanel below the grid (left column)
            Grid.SetColumn(pagingPanel, 0);
            Grid.SetRow(pagingPanel, 1);
            // add pagingPanel to grid (DataGrid already added above)
            grid.Children.Add(pagingPanel);

            var border = new Border { Width = 560, Background = System.Windows.Media.Brushes.WhiteSmoke, CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Margin = new Thickness(12,0,0,0) };
            var sv = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(0) };
            // scale images up ~50%
            _imgFull = new Image { Width = 510, Height = 300, Stretch = System.Windows.Media.Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
            _imgPlate = new Image { Width = 510, Height = 180, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(0,8,0,0), HorizontalAlignment = HorizontalAlignment.Center };
            // attach click handlers once (methods defined below)
            _imgPlate.MouseLeftButtonUp += ImgPlate_Click;
            _imgFull.MouseLeftButtonUp += ImgFull_Click;
            stack.Children.Add(_imgFull);
            stack.Children.Add(_imgPlate);
            // details grid
            var details = new Grid { Margin = new Thickness(0,8,0,0) };
            // rows: Id, Timestamp, Door, Event, Card, Barrier, Action, Pin, Operator, SourceIp
            for (int i = 0; i < 10; i++) details.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            details.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int r = 0;
            details.Children.Add(new TextBlock { Text = "Id:", FontWeight = FontWeights.Bold });
            _tbId = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbId, r); Grid.SetColumn(_tbId, 1); details.Children.Add(_tbId);
            Grid.SetRow(details.Children[0], r); Grid.SetColumn(details.Children[0], 0);
            // bind detail fields to SelectedLog on the viewmodel (display-only -> OneWay)
            _tbId.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.Id") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Thời gian:", FontWeight = FontWeights.Bold });
            _tbTimestamp = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbTimestamp, r); Grid.SetColumn(_tbTimestamp, 1); details.Children.Add(_tbTimestamp);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbTimestamp.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.Timestamp") { StringFormat = "g", Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Cổng:", FontWeight = FontWeights.Bold });
            _tbDoor = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbDoor, r); Grid.SetColumn(_tbDoor, 1); details.Children.Add(_tbDoor);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbDoor.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.DoorText") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Event:", FontWeight = FontWeights.Bold });
            _tbEvent = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbEvent, r); Grid.SetColumn(_tbEvent, 1); details.Children.Add(_tbEvent);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbEvent.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.EventTypeText") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Card:", FontWeight = FontWeights.Bold });
            _tbCard = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbCard, r); Grid.SetColumn(_tbCard, 1); details.Children.Add(_tbCard);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbCard.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.CardNo") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Barrier:", FontWeight = FontWeights.Bold });
            _tbBarrier = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbBarrier, r); Grid.SetColumn(_tbBarrier, 1); details.Children.Add(_tbBarrier);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbBarrier.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.BarrierText") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Action:", FontWeight = FontWeights.Bold });
            _tbAction = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbAction, r); Grid.SetColumn(_tbAction, 1); details.Children.Add(_tbAction);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbAction.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.Action") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Pin:", FontWeight = FontWeights.Bold });
            _tbPin = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbPin, r); Grid.SetColumn(_tbPin, 1); details.Children.Add(_tbPin);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            _tbPin.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.Pin") { Mode = System.Windows.Data.BindingMode.OneWay });
            r++;

            details.Children.Add(new TextBlock { Text = "Operator:", FontWeight = FontWeights.Bold });
            _tbOperator = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbOperator, r); Grid.SetColumn(_tbOperator, 1); details.Children.Add(_tbOperator);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            var convOp = new Services.NullOrEmptyToPlaceholderConverter();
            _tbOperator.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.Operator") { Mode = System.Windows.Data.BindingMode.OneWay, Converter = convOp, ConverterParameter = "Coming soon" });
            r++;

            details.Children.Add(new TextBlock { Text = "Source IP:", FontWeight = FontWeights.Bold });
            _tbSourceIp = new TextBox { IsReadOnly = true };
            Grid.SetRow(_tbSourceIp, r); Grid.SetColumn(_tbSourceIp, 1); details.Children.Add(_tbSourceIp);
            Grid.SetRow(details.Children[details.Children.Count-2], r); Grid.SetColumn(details.Children[details.Children.Count-2], 0);
            var conv = new Services.NullOrEmptyToPlaceholderConverter();
            _tbSourceIp.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.SourceIp") { Mode = System.Windows.Data.BindingMode.OneWay, Converter = conv, ConverterParameter = "Coming soon" });
            r++;


            // image path (plate) - clicking the plate image opens the file, add copy button
            var pathGrid = new Grid { Margin = new Thickness(0,8,0,0) };
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            _tbPlatePath = new TextBox { IsReadOnly = true };
            pathGrid.Children.Add(_tbPlatePath); Grid.SetColumn(_tbPlatePath, 0);
            _tbPlatePath.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.PlateImagePath") { Mode = System.Windows.Data.BindingMode.OneWay });
            var btnCopyPlate = new Button { Content = "Copy", Width = 64, Height = 28, Margin = new Thickness(8,0,0,0) };
            btnCopyPlate.Click += (s, e) => { try { Clipboard.SetText(_tbPlatePath.Text ?? string.Empty); } catch { } };
            pathGrid.Children.Add(btnCopyPlate); Grid.SetColumn(btnCopyPlate, 1);

            // full image path - clicking the full image opens the file, add copy button
            var pathGridFull = new Grid { Margin = new Thickness(0,4,0,0) };
            pathGridFull.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathGridFull.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            _tbFullPath = new TextBox { IsReadOnly = true };
            pathGridFull.Children.Add(_tbFullPath); Grid.SetColumn(_tbFullPath, 0);
            _tbFullPath.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.FullImagePath") { Mode = System.Windows.Data.BindingMode.OneWay });
            var btnCopyFull = new Button { Content = "Copy", Width = 64, Height = 28, Margin = new Thickness(8,0,0,0) };
            btnCopyFull.Click += (s, e) => { try { Clipboard.SetText(_tbFullPath.Text ?? string.Empty); } catch { } };
            pathGridFull.Children.Add(btnCopyFull); Grid.SetColumn(btnCopyFull, 1);

            stack.Children.Add(details);
            // replace open buttons with click handlers on images
            stack.Children.Add(pathGrid);
            stack.Children.Add(pathGridFull);

            stack.Children.Add(new TextBlock { Text = "Raw data:", FontWeight = FontWeights.Bold, Margin = new Thickness(0,8,0,0) });
            // compact raw data: 1-2 lines + copy button
            var rawRow = new Grid { Margin = new Thickness(0,4,0,0) };
            rawRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rawRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            _txtRaw = new TextBox
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Height = 48, // ~1-2 lines
                MaxHeight = 96,
                IsReadOnly = true,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            rawRow.Children.Add(_txtRaw);
            Grid.SetColumn(_txtRaw, 0);
            _txtRaw.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.RawData") { Mode = System.Windows.Data.BindingMode.OneWay });

            var btnCopyRaw = new Button { Content = "Copy", Width = 64, Height = 28, Margin = new Thickness(8,0,0,0) };
            btnCopyRaw.Click += (s, e) =>
            {
                try { Clipboard.SetText(_txtRaw.Text ?? string.Empty); }
                catch { }
            };
            rawRow.Children.Add(btnCopyRaw);
            Grid.SetColumn(btnCopyRaw, 1);

            stack.Children.Add(rawRow);
            stack.Children.Add(new TextBlock { Text = "Notes:", FontWeight = FontWeights.Bold, Margin = new Thickness(0,8,0,0) });
            _txtNotes = new TextBox { IsReadOnly = true };
            _txtNotes.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding("SelectedLog.Notes") { Mode = System.Windows.Data.BindingMode.OneWay });
            stack.Children.Add(_txtNotes);
            // Export selected detail (Excel)
            var btnExportDetail = new Button { Content = "Export detail (Excel)", Margin = new Thickness(0,8,0,0), Padding = new Thickness(8,4,8,4), HorizontalAlignment = HorizontalAlignment.Left };
            btnExportDetail.Click += async (s, e) =>
            {
                // determine selected log
                Models.ButtonPressLog? log = null;
                if (this.DataContext is ViewModels.ButtonLogsViewModel vm && vm.SelectedLog != null)
                    log = vm.SelectedLog;
                else if (_dgLogs.SelectedItem is Models.ButtonPressLog sel)
                    log = sel;

                if (log == null)
                {
                    MessageBox.Show("Không có bản ghi được chọn.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string suggested = $"ButtonLog_{log.Id}_{log.Timestamp:yyyyMMdd_HHmmss}.xlsx";
                var dialog = new SaveFileDialog { Filter = "Excel Workbook|*.xlsx", FileName = suggested };
                if (dialog.ShowDialog() != true) return;

                try
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        using var wb = new XLWorkbook();
                        var ws = wb.Worksheets.Add("Detail");
                        // headers
                        ws.Cell(1, 1).Value = "Id";
                        ws.Cell(1, 2).Value = "Thời gian";
                        ws.Cell(1, 3).Value = "Cổng";
                        ws.Cell(1, 4).Value = "Event";
                        ws.Cell(1, 5).Value = "Card";
                        ws.Cell(1, 6).Value = "Action";
                        ws.Cell(1, 7).Value = "Barrier";
                        ws.Cell(1, 8).Value = "Pin";
                        ws.Cell(1, 9).Value = "Operator";
                        ws.Cell(1,10).Value = "Source IP";
                        ws.Cell(1,11).Value = "PlateImagePath";
                        ws.Cell(1,12).Value = "FullImagePath";
                        ws.Cell(1,13).Value = "RawData";
                        ws.Cell(1,14).Value = "Notes";

                        // values
                        ws.Cell(2, 1).SetValue(log.Id);
                        ws.Cell(2, 2).SetValue(log.Timestamp);
                        ws.Cell(2, 3).SetValue(log.DoorText);
                        ws.Cell(2, 4).SetValue(log.EventTypeText);
                        ws.Cell(2, 5).SetValue(log.CardNo ?? string.Empty);
                        ws.Cell(2, 6).SetValue(log.Action ?? string.Empty);
                        ws.Cell(2, 7).SetValue(log.BarrierText ?? string.Empty);
                        ws.Cell(2, 8).SetValue(log.Pin ?? (int?)null);
                        ws.Cell(2, 9).SetValue(log.Operator ?? string.Empty);
                        ws.Cell(2,10).SetValue(log.SourceIp ?? string.Empty);
                        ws.Cell(2,11).SetValue(log.PlateImagePath ?? string.Empty);
                        ws.Cell(2,12).SetValue(log.FullImagePath ?? string.Empty);
                        ws.Cell(2,13).SetValue(log.RawData ?? string.Empty);
                        ws.Cell(2,14).SetValue(log.Notes ?? string.Empty);

                        ws.Columns().AdjustToContents();

                        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".xlsx");
                        wb.SaveAs(temp);
                        string finalExcelPath = dialog.FileName;
                        try
                        {
                            File.Copy(temp, finalExcelPath, true);
                        }
                        catch (IOException)
                        {
                            // destination may be locked (opened by Excel). Try a unique filename in same folder.
                            try
                            {
                                string dir = Path.GetDirectoryName(dialog.FileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                                string baseName = Path.GetFileNameWithoutExtension(dialog.FileName);
                                string ext = Path.GetExtension(dialog.FileName);
                                int idx = 1;
                                string alt;
                                do
                                {
                                    alt = Path.Combine(dir, $"{baseName}_{idx}{ext}");
                                    idx++;
                                } while (File.Exists(alt));

                                File.Copy(temp, alt, false);
                                finalExcelPath = alt;
                            }
                            catch (Exception exAlt)
                            {
                                // rethrow original IOException with helpful message
                                throw new IOException($"Cannot write to '{dialog.FileName}' and failed to create an alternative file. {exAlt.Message}", exAlt);
                            }
                        }
                        finally { try { File.Delete(temp); } catch { } }
                    });

                    MessageBox.Show("Export detail thành công!", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export thất bại: " + ex.Message, "Export", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { try { Mouse.OverrideCursor = null; } catch { } }
            };
            stack.Children.Add(btnExportDetail);
            sv.Content = stack;
            border.Child = sv;

            grid.Children.Add(border);
            Grid.SetColumn(border, 1);
            Grid.SetRow(border, 0);

            root.Children.Add(grid);
            Grid.SetRow(grid, 1);

            Content = root;

            // switch to MVVM: use ButtonLogsViewModel as DataContext
            var vm = new ViewModels.ButtonLogsViewModel();
            this.DataContext = vm;

            Loaded += async (s, e) => {
                if (_dpFrom.SelectedDate == null) _dpFrom.SelectedDate = DateTime.Today;
                if (_dpTo.SelectedDate == null) _dpTo.SelectedDate = DateTime.Today;
                // use batching and async DB read for smooth UI
                _currentPageSize = _pageIncrement;
                var fetched = await vm.LoadLogs(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null, pageIndex: 0, pageSize: _currentPageSize, batchSize: _batchSize);
                var total = await vm.GetTotalCount(_dpFrom.SelectedDate.Value.Date, _dpTo.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1), string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim(), _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null);
                int totalPages = (_currentPageSize <= 0) ? 1 : (int)Math.Ceiling((double)total / _currentPageSize);
                _btnNext.IsEnabled = _currentPageIndex < (totalPages - 1);
                UpdatePagingControls();
            };


            // subscribe to live RT events so the log updates immediately
            C3200Service.Instance.OnEvent += OnC3200EventLive;
            this.Closing += (s, e) => C3200Service.Instance.OnEvent -= OnC3200EventLive;
            // debounce refresh: if multiple RT events occur in short time, coalesce
            _refreshTimer = new System.Timers.Timer(500) { AutoReset = false };
            _refreshTimer.Elapsed += (s, e) => { _pendingRefresh = true; Application.Current?.Dispatcher?.Invoke(() => { if (!_suppressAutoRefresh) LoadLogs(); _pendingRefresh = false; }); };
            // do not auto-refresh while user is interacting with the grid
            _dgLogs.GotFocus += (s, e) => _suppressAutoRefresh = true;
            _dgLogs.LostFocus += (s, e) => _suppressAutoRefresh = false;
            // when user clicks selection, keep it (do not auto-select)
            _dgLogs.SelectionChanged += (s, e) => { /* no-op to allow selection to persist */ };
        }

        private void OnC3200EventLive(Services.C3200Event evt)
        {
            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (!_suppressAutoRefresh)
                        // trigger debounced refresh
                        _refreshTimer?.Stop();
                        _refreshTimer?.Start();
                });
            }
            catch { }
        }

        private void UpdatePagingControls()
        {
            try
            {
                // disable Prev if on first page
                if (_btnPrev != null) _btnPrev.IsEnabled = _currentPageIndex > 0;
                // Next enabled only if there are more pages
                if (_btnNext != null) _btnNext.IsEnabled = (_totalPages <= 0) ? false : _currentPageIndex < (_totalPages - 1);
                if (_lblPageInfo != null) _lblPageInfo.Text = _totalPages > 1 ? $"Trang {_currentPageIndex + 1} / {_totalPages}" : $"Trang {_currentPageIndex + 1}";
            }
            catch { }
        }

        private void OpenSelectedDetail()
        {
            if (_dgLogs.SelectedItem == null) return;
            if (_dgLogs.SelectedItem is ButtonPressLog log)
            {
                ShowDetailFromLog(log);
            }
        }

        private void OnSelectionChanged()
        {
            if (_dgLogs.SelectedItem == null) return;
            if (_dgLogs.SelectedItem is ButtonPressLog log)
                ShowDetailFromLog(log);
        }

        private void ShowDetailFromLog(ButtonPressLog log)
        {
            if (log == null) return;

            // populate metadata fields (guard against null controls)
            if (_tbId != null) _tbId.Text = log.Id.ToString();
            if (_tbTimestamp != null) _tbTimestamp.Text = log.Timestamp.ToString("g");
            // display friendly labels on UI; keep DB values unchanged
            if (_tbDoor != null) _tbDoor.Text = log.DoorText;
            if (_tbEvent != null) _tbEvent.Text = log.EventTypeText;
            if (_tbCard != null) _tbCard.Text = log.CardNo ?? string.Empty;
            if (_tbAction != null) _tbAction.Text = log.Action ?? string.Empty;
            if (_tbBarrier != null) _tbBarrier.Text = log.BarrierText;
            if (_tbPin != null) _tbPin.Text = log.Pin?.ToString() ?? string.Empty;
            if (_tbOperator != null) _tbOperator.Text = log.Operator ?? string.Empty;
            if (_tbSourceIp != null) _tbSourceIp.Text = log.SourceIp ?? string.Empty;

            // image paths
            _tbPlatePath.Text = log.PlateImagePath ?? string.Empty;
            _tbFullPath.Text = log.FullImagePath ?? string.Empty;

            // images, raw, notes
            SetImageSource(_imgFull, _tbFullPath.Text);
            SetImageSource(_imgPlate, _tbPlatePath.Text);
            // image paths already bound; clicking handlers are attached once in constructor
            _txtRaw.Text = log.RawData ?? string.Empty;
            _txtNotes.Text = log.Notes ?? string.Empty;

            // enable image click by binding path textboxes to SelectedLog
        }

        private void SetImageSource(Image img, string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    // use DecodePixelWidth to avoid loading full-size images into memory
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(path);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 1024; // reasonable max width for display
                    bi.EndInit();
                    bi.Freeze();
                    img.Source = bi;
                    return;
                }

                if (File.Exists(_defaultImagePath))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(_defaultImagePath);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    img.Source = bi;
                    return;
                }

                img.Source = null;
            }
            catch
            {
                img.Source = null;
            }
        }

        private async void LoadLogs()
        {
            try
            {
                if (_btnRefresh != null) _btnRefresh.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;

                if (this.DataContext is ViewModels.ButtonLogsViewModel vm)
                {
                    DateTime from = _dpFrom.SelectedDate?.Date ?? DateTime.Today;
                    DateTime to = _dpTo.SelectedDate?.Date.AddDays(1).AddSeconds(-1) ?? DateTime.Today.AddDays(1).AddSeconds(-1);
                    string? filter = string.IsNullOrWhiteSpace(_txtFilter.Text) ? null : _txtFilter.Text.Trim();
                    int? door = _cbDoor.SelectedIndex == 1 ? 1 : _cbDoor.SelectedIndex == 2 ? 2 : (int?)null;

                    var fetched = await vm.LoadLogs(from, to, filter, door, pageIndex: _currentPageIndex, pageSize: _currentPageSize, batchSize: _batchSize);
                    // update paging controls based on fetched count
                    _btnNext.IsEnabled = fetched >= _currentPageSize && _currentPageSize > 0;
                    UpdatePagingControls();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tải lịch sử: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try { if (_btnRefresh != null) _btnRefresh.IsEnabled = true; } catch { }
                try { Mouse.OverrideCursor = null; } catch { }
            }
        }
        // click handlers for images (moved out of constructor)
        private void ImgPlate_Click(object? sender, MouseButtonEventArgs e)
        {
            try
            {
                var p = _tbPlatePath?.Text;
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            }
            catch { }
        }

        private void ImgFull_Click(object? sender, MouseButtonEventArgs e)
        {
            try
            {
                var p = _tbFullPath?.Text;
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
