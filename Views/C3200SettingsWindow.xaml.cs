using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe
{
    public partial class C3200SettingsWindow : Window
    {
        private AppConfig _cfg;

        public C3200SettingsWindow()
        {
                InitializeComponent();
            _cfg = AppConfig.Load();

            IpBox.Text = _cfg.ZKTeco.IpAddress;
            PortBox.Text = _cfg.ZKTeco.TcpPort.ToString();
            PwdBox.Text = _cfg.ZKTeco.Password;
            TimeoutBox.Text = _cfg.ZKTeco.Timeout.ToString();
            BarrierBox.Text = _cfg.ZKTeco.BarrierDuration.ToString();
            CooldownBox.Text = _cfg.ZKTeco.CardCooldownMs.ToString();

            LoadReaderSelection();

            // populate button action combos selection
            var b1 = this.FindName("Button1ActionCombo") as ComboBox;
            var b2 = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1 != null && b2 != null)
            {
                var act1 = _cfg.ZKTeco.Button1Action ?? "OpenThisDoor";
                var act2 = _cfg.ZKTeco.Button2Action ?? "OpenThisDoor";
                for (int i = 0; i < b1.Items.Count; i++)
                {
                    if ((b1.Items[i] as ComboBoxItem)?.Tag?.ToString() == act1) { b1.SelectedIndex = i; break; }
                }
                for (int i = 0; i < b2.Items.Count; i++)
                {
                    if ((b2.Items[i] as ComboBoxItem)?.Tag?.ToString() == act2) { b2.SelectedIndex = i; break; }
                }
            }

            // Force mode is obsolete, handled dynamically in LaneRuntimeControl
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show("Reset tất cả cài đặt về mặc định?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            _cfg = new AppConfig();
            _cfg.Save();

            // reload UI values from defaults
            IpBox.Text = _cfg.ZKTeco.IpAddress;
            PortBox.Text = _cfg.ZKTeco.TcpPort.ToString();
            PwdBox.Text = _cfg.ZKTeco.Password;
            TimeoutBox.Text = _cfg.ZKTeco.Timeout.ToString();
            BarrierBox.Text = _cfg.ZKTeco.BarrierDuration.ToString();
            CooldownBox.Text = _cfg.ZKTeco.CardCooldownMs.ToString();

            var b1c = this.FindName("Button1ActionCombo") as ComboBox;
            var b2c = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1c != null) b1c.SelectedIndex = 1; // default OpenThisDoor
            if (b2c != null) b2c.SelectedIndex = 1;
            MessageBox.Show("Đã reset về mặc định", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private bool _isSyncingCombos = false;

        private void LoadReaderSelection()
        {
            var mappings = ReaderLaneMappingService.Instance.GetAll();
            
            // Determine door mapping from Reader 1
            var r1Map = mappings.FirstOrDefault(m => m.ReaderNo == 1);
            int door1Lane = (r1Map != null) ? r1Map.LaneIndex : 1;
            int door2Lane = (door1Lane == 1) ? 2 : 1;

            _isSyncingCombos = true;
            SetComboValue(Door1LaneCombo, door1Lane.ToString());
            SetComboValue(Door2LaneCombo, door2Lane.ToString());
            _isSyncingCombos = false;

            void BindReader(int readerNo, ComboBox dirCombo, CheckBox enableCheck)
            {
                var map = mappings.FirstOrDefault(m => m.ReaderNo == readerNo);
                if (map != null)
                {
                    // Select Direction
                    foreach (ComboBoxItem item in dirCombo.Items)
                    {
                        if (item.Tag?.ToString() == map.Direction)
                        {
                            dirCombo.SelectedItem = item;
                            break;
                        }
                    }
                    // Select Enabled
                    enableCheck.IsChecked = map.IsEnabled;
                }
                else
                {
                    dirCombo.SelectedIndex = 0;
                    enableCheck.IsChecked = true;
                }
            }

            BindReader(1, R1DirCombo, R1EnableCheck);
            BindReader(2, R2DirCombo, R2EnableCheck);
            BindReader(3, R3DirCombo, R3EnableCheck);
            BindReader(4, R4DirCombo, R4EnableCheck);
        }

        private void SetComboValue(ComboBox combo, string tag)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if ((combo.Items[i] as ComboBoxItem)?.Tag?.ToString() == tag)
                {
                    combo.SelectedIndex = i;
                    break;
                }
            }
        }

        private void Door1LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingCombos) return;
            if (Door1LaneCombo.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString();
                int door1Lane = tag == "2" ? 2 : 1;
                int door2Lane = door1Lane == 1 ? 2 : 1;

                _isSyncingCombos = true;
                SetComboValue(Door2LaneCombo, door2Lane.ToString());
                _isSyncingCombos = false;
            }
        }

        private void Door2LaneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingCombos) return;
            if (Door2LaneCombo.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString();
                int door2Lane = tag == "2" ? 2 : 1;
                int door1Lane = door2Lane == 1 ? 2 : 1;

                _isSyncingCombos = true;
                SetComboValue(Door1LaneCombo, door1Lane.ToString());
                _isSyncingCombos = false;
            }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                C3200Service.Instance.Configure(IpBox.Text,
                    int.TryParse(PortBox.Text, out var p) ? p : 4370,
                    PwdBox.Text,
                    int.TryParse(TimeoutBox.Text, out var t) ? t : 3000,
                    int.TryParse(BarrierBox.Text, out var b) ? b : 5);

                var ok = await C3200Service.Instance.ConnectAsync();
                MessageBox.Show(ok ? "Kết nối thành công" : $"Kết nối thất bại: {C3200Service.Instance.LastError}", "Test kết nối");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Test kết nối");
            }
        }

        private void TestConnectionDetailed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ip = IpBox.Text;
                int port = int.TryParse(PortBox.Text, out var p) ? p : 4370;
                string pwd = PwdBox.Text;
                int timeout = int.TryParse(TimeoutBox.Text, out var t) ? t : 3000;

                var res = Services.C3200Service.TestConnectDetailed(ip, port, pwd, timeout);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(res.Success ? "Connection: SUCCESS" : "Connection: FAILED");
                sb.AppendLine($"SDK Error: {res.SdkError}");
                sb.AppendLine("Diagnostic:");
                sb.AppendLine(res.Diagnostic ?? "(no diagnostic)");
                sb.AppendLine("Params Tried:");
                foreach (var tparam in res.TriedParams)
                {
                    var masked = tparam;
                    masked = System.Text.RegularExpressions.Regex.Replace(masked, ",password=[^,]*", ",password=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    masked = System.Text.RegularExpressions.Regex.Replace(masked, ",passwd=[^,]*", ",passwd=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    sb.AppendLine(" - " + masked);
                }
                sb.AppendLine($"DLL Arch: {res.DllArch}");
                sb.AppendLine($"Process Arch: {(Environment.Is64BitProcess ? "x64" : "x86")} ");

                try { LoggingService.Instance.LogInfo("C3200TestDetailed", "C3200SettingsWindow", sb.ToString(), userId: Environment.UserName); } catch { }

                MessageBox.Show(sb.ToString(), "C3200 Detailed Test", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while testing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var prevIp = _cfg.ZKTeco.IpAddress;
            var prevPort = _cfg.ZKTeco.TcpPort;
            var prevPwd = _cfg.ZKTeco.Password;
            var prevTimeout = _cfg.ZKTeco.Timeout;
            var prevBarrier = _cfg.ZKTeco.BarrierDuration;
            var prevCooldown = _cfg.ZKTeco.CardCooldownMs;
            var prevForceIn = _cfg.ZKTeco.ForceAllIn;
            var prevForceOut = _cfg.ZKTeco.ForceAllOut;
            var prevBtn1 = _cfg.ZKTeco.Button1Action;
            var prevBtn2 = _cfg.ZKTeco.Button2Action;

            _cfg.ZKTeco.IpAddress = IpBox.Text;
            _cfg.ZKTeco.TcpPort = int.TryParse(PortBox.Text, out var p) ? p : _cfg.ZKTeco.TcpPort;
            _cfg.ZKTeco.Password = PwdBox.Text;
            _cfg.ZKTeco.Timeout = int.TryParse(TimeoutBox.Text, out var t) ? t : _cfg.ZKTeco.Timeout;
            _cfg.ZKTeco.BarrierDuration = int.TryParse(BarrierBox.Text, out var b) ? b : _cfg.ZKTeco.BarrierDuration;
            _cfg.ZKTeco.CardCooldownMs = int.TryParse(CooldownBox.Text, out var c) ? c : _cfg.ZKTeco.CardCooldownMs;
            
            // ForceMode obsolete
            _cfg.ZKTeco.ForceAllIn = false;
            _cfg.ZKTeco.ForceAllOut = false;

            var b1 = this.FindName("Button1ActionCombo") as ComboBox;
            var b2 = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1?.SelectedItem is ComboBoxItem bi1) _cfg.ZKTeco.Button1Action = bi1.Tag?.ToString() ?? _cfg.ZKTeco.Button1Action;
            if (b2?.SelectedItem is ComboBoxItem bi2) _cfg.ZKTeco.Button2Action = bi2.Tag?.ToString() ?? _cfg.ZKTeco.Button2Action;

            // save reader mappings
            int laneForDoor1 = 1;
            if (Door1LaneCombo.SelectedItem is ComboBoxItem biDoor1)
            {
                int.TryParse(biDoor1.Tag?.ToString(), out laneForDoor1);
            }
            int laneForDoor2 = (laneForDoor1 == 1) ? 2 : 1;

            var newMappings = new List<ReaderLaneMapping>();
            
            void ExtractReaderMap(int readerNo, int mappedLane, ComboBox dirCombo, CheckBox enableCheck)
            {
                newMappings.Add(new ReaderLaneMapping
                {
                    ReaderNo = readerNo,
                    LaneIndex = mappedLane,
                    Direction = ((ComboBoxItem)dirCombo.SelectedItem)?.Tag?.ToString() ?? "IN",
                    IsEnabled = enableCheck.IsChecked == true
                });
            }

            ExtractReaderMap(1, laneForDoor1, R1DirCombo, R1EnableCheck);
            ExtractReaderMap(2, laneForDoor1, R2DirCombo, R2EnableCheck);
            ExtractReaderMap(3, laneForDoor2, R3DirCombo, R3EnableCheck);
            ExtractReaderMap(4, laneForDoor2, R4DirCombo, R4EnableCheck);

            ReaderLaneMappingService.Instance.UpdateMappings(newMappings);

            try
            {
                var changes = new System.Text.StringBuilder();
                void AddChange(string name, object oldV, object newV)
                {
                    if ((oldV?.ToString() ?? string.Empty) != (newV?.ToString() ?? string.Empty))
                    {
                        if (changes.Length > 0) changes.Append("; ");
                        changes.Append($"{name}: '{oldV}' -> '{newV}'");
                    }
                }

                AddChange("IpAddress", prevIp, _cfg.ZKTeco.IpAddress);
                AddChange("TcpPort", prevPort, _cfg.ZKTeco.TcpPort);
                AddChange("Password", string.IsNullOrEmpty(prevPwd) ? "(empty)" : "(redacted)", string.IsNullOrEmpty(_cfg.ZKTeco.Password) ? "(empty)" : "(redacted)");
                AddChange("Timeout", prevTimeout, _cfg.ZKTeco.Timeout);
                AddChange("BarrierDuration", prevBarrier, _cfg.ZKTeco.BarrierDuration);
                AddChange("CardCooldownMs", prevCooldown, _cfg.ZKTeco.CardCooldownMs);
                AddChange("ForceAllIn", prevForceIn, _cfg.ZKTeco.ForceAllIn);
                AddChange("ForceAllOut", prevForceOut, _cfg.ZKTeco.ForceAllOut);
                AddChange("Button1Action", prevBtn1, _cfg.ZKTeco.Button1Action);
                AddChange("Button2Action", prevBtn2, _cfg.ZKTeco.Button2Action);
                AddChange("ReaderMappings", "updated", "updated");

                _cfg.Save();

                C3200Service.Instance.Configure(_cfg.ZKTeco.IpAddress, _cfg.ZKTeco.TcpPort,
                    _cfg.ZKTeco.Password, _cfg.ZKTeco.Timeout, _cfg.ZKTeco.BarrierDuration);

                if (changes.Length > 0)
                {
                    try { LoggingService.Instance.LogAudit("CONFIG_CHANGED_UI", "C3200Settings", "config.json", null, new { Diffs = changes.ToString() }, source: "C3200SettingsWindow", details: $"Config updated via UI: {changes}"); } catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save config: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
