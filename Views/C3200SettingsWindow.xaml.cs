using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe
{
    public partial class C3200SettingsWindow : Window
    {
        private AppConfig _cfg;
        private bool _suppressGroupSync = false;

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
            // populate group combos (C3-200 reports rawDoor groups 1 or 2)
            var gi = this.FindName("GateInCombo") as ComboBox;
            var go = this.FindName("GateOutCombo") as ComboBox;
            if (gi != null && go != null)
            {
                gi.Items.Clear();
                go.Items.Clear();
                var item1 = new ComboBoxItem { Content = "Group A — readers 1+2 (rawDoor=1)", Tag = "1" };
                var item2 = new ComboBoxItem { Content = "Group B — readers 3+4 (rawDoor=2)", Tag = "2" };
                gi.Items.Add(item1);
                gi.Items.Add(item2);
                go.Items.Add(new ComboBoxItem { Content = "Group A — readers 1+2 (rawDoor=1)", Tag = "1" });
                go.Items.Add(new ComboBoxItem { Content = "Group B — readers 3+4 (rawDoor=2)", Tag = "2" });

                // wire up selection sync so choosing one group auto-selects the opposite in the other combo
                gi.SelectionChanged += GateInCombo_SelectionChanged;
                go.SelectionChanged += GateOutCombo_SelectionChanged;

                // pre-select according to CSV config (choose first matching group)
                var inCsv = _cfg.ZKTeco.GateInDoors ?? _cfg.ZKTeco.GateInDoor.ToString();
                if (inCsv.Contains("1")) gi.SelectedItem = item1;
                else if (inCsv.Contains("2")) gi.SelectedItem = item2;
                var outCsv = _cfg.ZKTeco.GateOutDoors ?? _cfg.ZKTeco.GateOutDoor.ToString();
                if (outCsv.Contains("1")) go.SelectedIndex = 0;
                else if (outCsv.Contains("2")) go.SelectedIndex = 1;
                // add button action options
                // Button1/2 actions: OpenThisDoor (default), OpenGroupIn, OpenGroupOut
                // show current config in tooltips
                gi.ToolTip = "Select IN group. Button actions configured in C3-200 Settings.";
                go.ToolTip = "Select OUT group. Button actions configured in C3-200 Settings.";
            }

            // populate button action combos
            var b1 = this.FindName("Button1ActionCombo") as ComboBox;
            var b2 = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1 != null && b2 != null)
            {
                // select according to config
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

            // wire up selection sync so choosing one group auto-selects the opposite in the other combo
            gi.SelectionChanged += GateInCombo_SelectionChanged;
            go.SelectionChanged += GateOutCombo_SelectionChanged;
            if (_cfg.ZKTeco.ForceAllIn)
            {
                ForceAllInRadio.IsChecked = true;
            }
            else if (_cfg.ZKTeco.ForceAllOut)
            {
                ForceAllOutRadio.IsChecked = true;
            }
            else
            {
                AutomaticRadio.IsChecked = true;
            }

            // disable combos if forcing mode is selected
            var gi2 = this.FindName("GateInCombo") as ComboBox;
            var go2 = this.FindName("GateOutCombo") as ComboBox;
            if (gi2 != null && go2 != null)
            {
                gi2.IsEnabled = !_cfg.ZKTeco.ForceAllIn && !_cfg.ZKTeco.ForceAllOut;
                go2.IsEnabled = !_cfg.ZKTeco.ForceAllIn && !_cfg.ZKTeco.ForceAllOut;
            }
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

            // reset combos
            var gi3 = this.FindName("GateInCombo") as ComboBox;
            var go3 = this.FindName("GateOutCombo") as ComboBox;
            if (gi3 != null && go3 != null)
            {
                gi3.SelectedIndex = 0;
                go3.SelectedIndex = 1;
            }

            var b1c = this.FindName("Button1ActionCombo") as ComboBox;
            var b2c = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1c != null) b1c.SelectedIndex = 1; // default OpenThisDoor
            if (b2c != null) b2c.SelectedIndex = 1;

            AutomaticRadio.IsChecked = true;

            MessageBox.Show("Đã reset về mặc định", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ForceMode_Checked(object sender, RoutedEventArgs e)
        {
            // toggle enable state of combos depending on force mode
            bool forced = (ForceAllInRadio.IsChecked == true) || (ForceAllOutRadio.IsChecked == true);
            var gi = this.FindName("GateInCombo") as ComboBox;
            var go = this.FindName("GateOutCombo") as ComboBox;
            if (gi != null && go != null)
            {
                gi.IsEnabled = !forced;
                go.IsEnabled = !forced;
            }
        }

        // now using group combos; no per-reader selection handlers
        private void GateInCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGroupSync) return;
            try
            {
                _suppressGroupSync = true;
                var gi = sender as ComboBox;
                var go = this.FindName("GateOutCombo") as ComboBox;
                if (gi != null && go != null && gi.SelectedItem is ComboBoxItem ci)
                {
                    // auto-select opposite group: if A selected for IN then select B for OUT, and vice versa
                    var sel = ci.Tag?.ToString();
                    if (sel == "1") go.SelectedIndex = 1; // select group B
                    else if (sel == "2") go.SelectedIndex = 0; // select group A
                }
            }
            finally { _suppressGroupSync = false; }
        }

        private void GateOutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGroupSync) return;
            try
            {
                _suppressGroupSync = true;
                var go = sender as ComboBox;
                var gi = this.FindName("GateInCombo") as ComboBox;
                if (gi != null && go != null && go.SelectedItem is ComboBoxItem co)
                {
                    var sel = co.Tag?.ToString();
                    if (sel == "1") gi.SelectedIndex = 1;
                    else if (sel == "2") gi.SelectedIndex = 0;
                }
            }
            finally { _suppressGroupSync = false; }
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _cfg.ZKTeco.IpAddress = IpBox.Text;
            _cfg.ZKTeco.TcpPort = int.TryParse(PortBox.Text, out var p) ? p : _cfg.ZKTeco.TcpPort;
            _cfg.ZKTeco.Password = PwdBox.Text;
            _cfg.ZKTeco.Timeout = int.TryParse(TimeoutBox.Text, out var t) ? t : _cfg.ZKTeco.Timeout;
            _cfg.ZKTeco.BarrierDuration = int.TryParse(BarrierBox.Text, out var b) ? b : _cfg.ZKTeco.BarrierDuration;
            _cfg.ZKTeco.CardCooldownMs = int.TryParse(CooldownBox.Text, out var c) ? c : _cfg.ZKTeco.CardCooldownMs;
            // save selected groups
            var selIn = this.FindName("GateInCombo") as ComboBox;
            var selOut = this.FindName("GateOutCombo") as ComboBox;
            var selInItem = selIn?.SelectedItem as ComboBoxItem;
            var selOutItem = selOut?.SelectedItem as ComboBoxItem;
            _cfg.ZKTeco.GateInDoors = selInItem?.Tag?.ToString() ?? _cfg.ZKTeco.GateInDoors;
            _cfg.ZKTeco.GateOutDoors = selOutItem?.Tag?.ToString() ?? _cfg.ZKTeco.GateOutDoors;
            if (int.TryParse(_cfg.ZKTeco.GateInDoors?.Split(',')[0], out var legacyIn)) _cfg.ZKTeco.GateInDoor = legacyIn;
            if (int.TryParse(_cfg.ZKTeco.GateOutDoors?.Split(',')[0], out var legacyOut)) _cfg.ZKTeco.GateOutDoor = legacyOut;
            _cfg.ZKTeco.ForceAllIn = ForceAllInRadio.IsChecked == true;
            _cfg.ZKTeco.ForceAllOut = ForceAllOutRadio.IsChecked == true;

            var b1 = this.FindName("Button1ActionCombo") as ComboBox;
            var b2 = this.FindName("Button2ActionCombo") as ComboBox;
            if (b1?.SelectedItem is ComboBoxItem bi1) _cfg.ZKTeco.Button1Action = bi1.Tag?.ToString() ?? _cfg.ZKTeco.Button1Action;
            if (b2?.SelectedItem is ComboBoxItem bi2) _cfg.ZKTeco.Button2Action = bi2.Tag?.ToString() ?? _cfg.ZKTeco.Button2Action;

            // validation: no overlap between selected groups
            if (selInItem != null && selOutItem != null && selInItem.Tag?.ToString() == selOutItem.Tag?.ToString())
            {
                MessageBox.Show($"Selected group for IN and OUT cannot be the same. Please choose different groups.", "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _cfg.Save();

            // reconfigure runtime
            C3200Service.Instance.Configure(_cfg.ZKTeco.IpAddress, _cfg.ZKTeco.TcpPort,
                _cfg.ZKTeco.Password, _cfg.ZKTeco.Timeout, _cfg.ZKTeco.BarrierDuration);

            MessageBox.Show("Saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
