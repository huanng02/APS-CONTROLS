using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.ViewModels;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public class AppLogsWindow : Window
    {
        private DataGrid _dg;
        private ObservableCollection<AppLog> _items = new();

        public AppLogsWindow()
        {
            Title = "Application Logs";
            Width = 900;
            Height = 600;

            var root = new Grid { Margin = new Thickness(8) };
            this.Content = root;

            _dg = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                ItemsSource = _items
            };

            _dg.Columns.Add(new DataGridTextColumn { Header = "Time (UTC)", Binding = new System.Windows.Data.Binding("TimestampUtc") { StringFormat = "o" }, Width = 180 });
            _dg.Columns.Add(new DataGridTextColumn { Header = "Level", Binding = new System.Windows.Data.Binding("Level"), Width = 80 });
            _dg.Columns.Add(new DataGridTextColumn { Header = "Event", Binding = new System.Windows.Data.Binding("EventType"), Width = 200 });
            _dg.Columns.Add(new DataGridTextColumn { Header = "Source", Binding = new System.Windows.Data.Binding("Source"), Width = 140 });
            _dg.Columns.Add(new DataGridTextColumn { Header = "User", Binding = new System.Windows.Data.Binding("UserId"), Width = 100 });
            _dg.Columns.Add(new DataGridTextColumn { Header = "Plate", Binding = new System.Windows.Data.Binding("Plate"), Width = 120 });
            _dg.Columns.Add(new DataGridTextColumn { Header = "Details", Binding = new System.Windows.Data.Binding("Details"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

            root.Children.Add(_dg);

            Loaded += AppLogsWindow_Loaded;
        }

        private async void AppLogsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLogs();
        }

        private async System.Threading.Tasks.Task LoadLogs()
        {
            try
            {
                var db = new DatabaseService();
                string conn = db.GetConnectionString();
                using (var connSql = new SqlConnection(conn))
                {
                    await connSql.OpenAsync();
                    string q = "SELECT TOP (1000) Id, TimestampUtc, [Level], EventType, Source, UserId, Plate, Details FROM dbo.AppLogs ORDER BY TimestampUtc DESC";
                    using (var cmd = new SqlCommand(q, connSql))
                    using (var rdr = await cmd.ExecuteReaderAsync())
                    {
                        while (await rdr.ReadAsync())
                        {
                            _items.Add(new AppLog
                            {
                                Id = rdr.GetInt32(rdr.GetOrdinal("Id")),
                                TimestampUtc = rdr.GetDateTime(rdr.GetOrdinal("TimestampUtc")),
                                Level = rdr.IsDBNull(rdr.GetOrdinal("Level")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Level")),
                                EventType = rdr.IsDBNull(rdr.GetOrdinal("EventType")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("EventType")),
                                Source = rdr.IsDBNull(rdr.GetOrdinal("Source")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Source")),
                                UserId = rdr.IsDBNull(rdr.GetOrdinal("UserId")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("UserId")),
                                Plate = rdr.IsDBNull(rdr.GetOrdinal("Plate")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Plate")),
                                Details = rdr.IsDBNull(rdr.GetOrdinal("Details")) ? string.Empty : rdr.GetString(rdr.GetOrdinal("Details"))
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thể tải logs: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
