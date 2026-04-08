using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;
using System.Data.SqlClient;

namespace QuanLyGiuXe.ViewModels
{
    public class ButtonLogsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ButtonLogItem> Logs { get; } = new();

        private ButtonLogItem? _selectedLog;
        public ButtonLogItem? SelectedLog
        {
            get => _selectedLog;
            set { _selectedLog = value; OnPropertyChanged(nameof(SelectedLog)); }
        }

        // Return total number of matching rows for filters — used to compute total pages
        public async Task<int> GetTotalCount(DateTime from, DateTime to, string? filter, int? door)
        {
            try
            {
                var db = new DatabaseService();
                string conn = db.GetConnectionString();
                using (var connSql = new SqlConnection(conn))
                {
                    await connSql.OpenAsync();
                    string q = "SELECT COUNT(*) FROM dbo.ButtonPressLog WHERE Timestamp BETWEEN @from AND @to";
                    if (!string.IsNullOrWhiteSpace(filter)) q += " AND (CardNo LIKE @filter OR Action LIKE @filter)";
                    if (door.HasValue) q += " AND Door = @door";
                    using (var cmd = new SqlCommand(q, connSql))
                    {
                        cmd.Parameters.AddWithValue("@from", from);
                        cmd.Parameters.AddWithValue("@to", to);
                        if (!string.IsNullOrWhiteSpace(filter)) cmd.Parameters.AddWithValue("@filter", "%" + filter.Trim() + "%");
                        if (door.HasValue) cmd.Parameters.AddWithValue("@door", door.Value);
                        var res = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(res ?? 0);
                    }
                }
            }
            catch
            {
                return 0;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ButtonLogsViewModel() { }

        /// <summary>
        /// Load logs with optional filters. This method reads from DB on a background thread
        /// and adds items to the Logs collection in batches to avoid UI freezes.
        /// </summary>
        public async Task<int> LoadLogs(DateTime from, DateTime to, string? filter, int? door, int pageIndex = 0, int pageSize = 500, int batchSize = 100)
        {
            try
            {
                var db = new DatabaseService();
                string conn = db.GetConnectionString();

                // Build query with OFFSET-FETCH for paging
                string qBase = "SELECT Id, Timestamp, Door, EventType, CardNo, Action, Pin, BarrierResult, PlateImagePath, FullImagePath, Operator, SourceIp, RawData, Notes FROM dbo.ButtonPressLog WHERE Timestamp BETWEEN @from AND @to";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    qBase += " AND (CardNo LIKE @filter OR Action LIKE @filter)";
                }
                if (door.HasValue)
                {
                    qBase += " AND Door = @door";
                }
                // do not append ORDER BY here — it will be added when building the paged query

                var rowsBatch = new List<ButtonLogItem>(batchSize);
                int fetchedTotal = 0;

                await Task.Run(async () =>
                {
                    using (var connSql = new SqlConnection(conn))
                    {
                        await connSql.OpenAsync();

                        // if pageSize <= 0 treat as no paging (return all matching rows)
                        if (pageSize <= 0)
                        {
                            string qAll = "SELECT Id, Timestamp, Door, EventType, CardNo, Action, Pin, BarrierResult, PlateImagePath, FullImagePath, Operator, SourceIp, RawData, Notes FROM dbo.ButtonPressLog WHERE Timestamp BETWEEN @from AND @to";
                            if (!string.IsNullOrWhiteSpace(filter)) qAll += " AND (CardNo LIKE @filter OR Action LIKE @filter)";
                            if (door.HasValue) qAll += " AND Door = @door";
                            qAll += " ORDER BY Timestamp DESC";

                            using (var cmd = new SqlCommand(qAll, connSql))
                            {
                                cmd.Parameters.AddWithValue("@from", from);
                                cmd.Parameters.AddWithValue("@to", to);
                                if (!string.IsNullOrWhiteSpace(filter)) cmd.Parameters.AddWithValue("@filter", "%" + filter.Trim() + "%");
                                if (door.HasValue) cmd.Parameters.AddWithValue("@door", door.Value);

                                using (var rdr = await cmd.ExecuteReaderAsync())
                                {
                                    // when loading all, clear existing on first page
                                    if (pageIndex == 0) Application.Current?.Dispatcher?.Invoke(() => Logs.Clear());

                                    while (await rdr.ReadAsync())
                                    {
                                        var item = new ButtonLogItem
                                        {
                                            Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                                            Timestamp = rdr.GetDateTime(rdr.GetOrdinal("Timestamp")),
                                            Door = rdr.IsDBNull(rdr.GetOrdinal("Door")) ? (byte?)null : Convert.ToByte(rdr.GetValue(rdr.GetOrdinal("Door"))),
                                            EventType = rdr.IsDBNull(rdr.GetOrdinal("EventType")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("EventType")),
                                            CardNo = rdr.IsDBNull(rdr.GetOrdinal("CardNo")) ? null : rdr.GetString(rdr.GetOrdinal("CardNo")),
                                            Action = rdr.IsDBNull(rdr.GetOrdinal("Action")) ? null : rdr.GetString(rdr.GetOrdinal("Action")),
                                            Pin = rdr.IsDBNull(rdr.GetOrdinal("Pin")) ? (int?)null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Pin"))),
                                            RawData = rdr.IsDBNull(rdr.GetOrdinal("RawData")) ? null : rdr.GetString(rdr.GetOrdinal("RawData")),
                                            PlateImagePath = rdr.IsDBNull(rdr.GetOrdinal("PlateImagePath")) ? null : rdr.GetString(rdr.GetOrdinal("PlateImagePath")),
                                            FullImagePath = rdr.IsDBNull(rdr.GetOrdinal("FullImagePath")) ? null : rdr.GetString(rdr.GetOrdinal("FullImagePath")),
                                            Operator = rdr.IsDBNull(rdr.GetOrdinal("Operator")) ? null : rdr.GetString(rdr.GetOrdinal("Operator")),
                                            SourceIp = rdr.IsDBNull(rdr.GetOrdinal("SourceIp")) ? null : rdr.GetString(rdr.GetOrdinal("SourceIp")),
                                            Notes = rdr.IsDBNull(rdr.GetOrdinal("Notes")) ? null : rdr.GetString(rdr.GetOrdinal("Notes")),
                                            BarrierResult = rdr.IsDBNull(rdr.GetOrdinal("BarrierResult")) ? (byte?)null : Convert.ToByte(rdr.GetValue(rdr.GetOrdinal("BarrierResult")))
                                        };

                                        rowsBatch.Add(item);
                                        fetchedTotal++;

                                        if (rowsBatch.Count >= batchSize)
                                        {
                                            var toAdd = rowsBatch.ToArray();
                                            rowsBatch.Clear();
                                            Application.Current?.Dispatcher?.Invoke(() =>
                                            {
                                                foreach (var it in toAdd) Logs.Add(it);
                                            });
                                        }
                                    }

                                    // flush remaining
                                    if (rowsBatch.Count > 0)
                                    {
                                        var toAdd = rowsBatch.ToArray();
                                        rowsBatch.Clear();
                                        Application.Current?.Dispatcher?.Invoke(() =>
                                        {
                                            foreach (var it in toAdd) Logs.Add(it);
                                        });
                                    }
                                }
                            }
                        }
                        else
                        {
                        int offset = pageIndex * pageSize;
                        int startRow = offset + 1;
                        int endRow = offset + pageSize;

                        // Use ROW_NUMBER() paging for broader SQL Server compatibility
                        string paged = $@"SELECT * FROM (
                                            SELECT Id, Timestamp, Door, EventType, CardNo, Action, Pin, BarrierResult, PlateImagePath, FullImagePath, Operator, SourceIp, RawData, Notes,
                                                   ROW_NUMBER() OVER (ORDER BY Timestamp DESC) AS rn
                                            FROM dbo.ButtonPressLog
                                            WHERE Timestamp BETWEEN @from AND @to";
                        if (!string.IsNullOrWhiteSpace(filter)) paged += " AND (CardNo LIKE @filter OR Action LIKE @filter)";
                        if (door.HasValue) paged += " AND Door = @door";
                        paged += ") t WHERE rn BETWEEN @start AND @end ORDER BY rn";

                        using (var cmd = new SqlCommand(paged, connSql))
                        {
                            cmd.Parameters.AddWithValue("@from", from);
                            cmd.Parameters.AddWithValue("@to", to);
                            if (!string.IsNullOrWhiteSpace(filter)) cmd.Parameters.AddWithValue("@filter", "%" + filter.Trim() + "%");
                            if (door.HasValue) cmd.Parameters.AddWithValue("@door", door.Value);
                            cmd.Parameters.AddWithValue("@start", startRow);
                            cmd.Parameters.AddWithValue("@end", endRow);

                            using (var rdr = await cmd.ExecuteReaderAsync())
                            {
                                // clear existing on UI before adding new page
                                if (pageIndex == 0) Application.Current?.Dispatcher?.Invoke(() => Logs.Clear());

                                while (await rdr.ReadAsync())
                                {
                                    var item = new ButtonLogItem
                                    {
                                        Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                                        Timestamp = rdr.GetDateTime(rdr.GetOrdinal("Timestamp")),
                                        Door = rdr.IsDBNull(rdr.GetOrdinal("Door")) ? (byte?)null : Convert.ToByte(rdr.GetValue(rdr.GetOrdinal("Door"))),
                                        EventType = rdr.IsDBNull(rdr.GetOrdinal("EventType")) ? (int?)null : rdr.GetInt32(rdr.GetOrdinal("EventType")),
                                        CardNo = rdr.IsDBNull(rdr.GetOrdinal("CardNo")) ? null : rdr.GetString(rdr.GetOrdinal("CardNo")),
                                        Action = rdr.IsDBNull(rdr.GetOrdinal("Action")) ? null : rdr.GetString(rdr.GetOrdinal("Action")),
                                        Pin = rdr.IsDBNull(rdr.GetOrdinal("Pin")) ? (int?)null : Convert.ToInt32(rdr.GetValue(rdr.GetOrdinal("Pin"))),
                                        RawData = rdr.IsDBNull(rdr.GetOrdinal("RawData")) ? null : rdr.GetString(rdr.GetOrdinal("RawData")),
                                        PlateImagePath = rdr.IsDBNull(rdr.GetOrdinal("PlateImagePath")) ? null : rdr.GetString(rdr.GetOrdinal("PlateImagePath")),
                                        FullImagePath = rdr.IsDBNull(rdr.GetOrdinal("FullImagePath")) ? null : rdr.GetString(rdr.GetOrdinal("FullImagePath")),
                                        Operator = rdr.IsDBNull(rdr.GetOrdinal("Operator")) ? null : rdr.GetString(rdr.GetOrdinal("Operator")),
                                        SourceIp = rdr.IsDBNull(rdr.GetOrdinal("SourceIp")) ? null : rdr.GetString(rdr.GetOrdinal("SourceIp")),
                                        Notes = rdr.IsDBNull(rdr.GetOrdinal("Notes")) ? null : rdr.GetString(rdr.GetOrdinal("Notes")),
                                        BarrierResult = rdr.IsDBNull(rdr.GetOrdinal("BarrierResult")) ? (byte?)null : Convert.ToByte(rdr.GetValue(rdr.GetOrdinal("BarrierResult")))
                                    };

                                    rowsBatch.Add(item);
                                    fetchedTotal++;

                                    if (rowsBatch.Count >= batchSize)
                                    {
                                        var toAdd = rowsBatch.ToArray();
                                        rowsBatch.Clear();
                                        Application.Current?.Dispatcher?.Invoke(() =>
                                        {
                                            foreach (var it in toAdd) Logs.Add(it);
                                        });
                                    }
                                }

                                // flush remaining
                                if (rowsBatch.Count > 0)
                                {
                                    var toAdd = rowsBatch.ToArray();
                                    rowsBatch.Clear();
                                    Application.Current?.Dispatcher?.Invoke(() =>
                                    {
                                        foreach (var it in toAdd) Logs.Add(it);
                                    });
                                }
                            }
                        }
                        }
                    }
                });
                // ensure SelectedLog set
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    if (Logs.Count > 0 && SelectedLog == null) SelectedLog = Logs.First();
                });

                return fetchedTotal;
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher?.Invoke(() => MessageBox.Show("Không thể tải lịch sử: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error));
                return 0;
            }
        }
    }
}
