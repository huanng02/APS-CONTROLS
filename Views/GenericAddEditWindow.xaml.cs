using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class GenericAddEditWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        private object _model;

        private string _selectedTrangThai;
        public string SelectedTrangThai
        {
            get => _selectedTrangThai;
            set { _selectedTrangThai = value; OnPropertyChanged("SelectedTrangThai"); }
        }

        public IEnumerable<string> TrangThaiList { get; } = new[] { "Active", "Inactive", "Disabled" };

        public GenericAddEditWindow(object model)
        {
            InitializeComponent();
            _model = model;
            // Use async method to build fields without blocking UI
            BuildFieldsAsync();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            // Allow dragging
            this.MouseDown += (s, e) => {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    this.DragMove();
            };
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private async void BuildFieldsAsync()
        {
            try
            {
                FieldsPanel.Children.Clear();
                var props = _model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.Name != "Id" && p.CanWrite && (p.PropertyType == typeof(string) || p.PropertyType == typeof(int) || p.PropertyType == typeof(decimal) || p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(int?) || p.PropertyType == typeof(bool)));


                foreach (var p in props)
                {
                    var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 8, 0, 8) };
                    sp.Children.Add(new TextBlock { 
                        Text = p.Name.ToUpper(), 
                        FontSize = 11, 
                        FontWeight = FontWeights.Bold, 
                        Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextSecondaryBrush"),
                        Margin = new Thickness(0, 0, 0, 5)
                    });
                    
                    Control input = null;
                    if (p.Name == "SiteId")
                    {
                        var cb = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("ModernComboBox") };
                        var sites = await ParkingTopologyService.Instance.GetSitesAsync();
                        cb.ItemsSource = sites;
                        cb.DisplayMemberPath = "SiteCode";
                        cb.SelectedValuePath = "Id";
                        int currentSiteId = (int)(p.GetValue(_model) ?? 0);
                        if (currentSiteId != 0) cb.SelectedValue = currentSiteId;

                        cb.SelectionChanged += (s, e) =>
                        {
                            if (cb.SelectedItem != null)
                            {
                                CopyMatchingProperties(cb.SelectedItem);
                            }
                        };
                        input = cb;
                    }
                    else if (p.Name == "ZoneId")
                    {
                        var cb = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("ModernComboBox") };
                        var zones = await ParkingTopologyService.Instance.GetZonesAsync();
                        cb.ItemsSource = zones;
                        cb.DisplayMemberPath = "ZoneCode";
                        cb.SelectedValuePath = "Id";
                        int currentZoneId = (int)(p.GetValue(_model) ?? 0);
                        if (currentZoneId != 0) cb.SelectedValue = currentZoneId;
                        
                        cb.SelectionChanged += (s, e) => {
                            if (cb.SelectedItem != null)
                            {
                                var zone = cb.SelectedItem;
                                var zoneType = zone.GetType();
                                var propsToCopy = new[] { "Capacity", "PricePerHour", "ZoneName", "ZoneCode" };
                                foreach (var propName in propsToCopy)
                                {
                                    var zoneProp = zoneType.GetProperty(propName);
                                    var modelProp = _model.GetType().GetProperty(propName);
                                    if (zoneProp != null && modelProp != null)
                                    {
                                        var val = zoneProp.GetValue(zone);
                                        modelProp.SetValue(_model, val);
                                        foreach (var field in FieldsPanel.Children.OfType<StackPanel>())
                                        {
                                            var innerInput = field.Children.OfType<Control>().FirstOrDefault();
                                            if (innerInput?.Tag is PropertyInfo pi && pi.Name == propName)
                                            {
                                                if (innerInput is TextBox tb) tb.Text = val?.ToString();
                                            }
                                        }
                                    }
                                }
                            }
                        };
                        input = cb;
                    }
                    
                    // Removed Id selection; now handled via IpAddress ComboBox
                    else if (p.Name == "IpAddress")
                    {
                        var cb = new ComboBox { 
                            Width = 200, 
                            HorizontalAlignment = HorizontalAlignment.Left, 
                            Style = (Style)Application.Current.FindResource("ModernComboBox"),
                            IsEditable = true 
                        };
                        var ips = new List<string>();
                        
                        // 1. Get the configured C3 IP from settings
                        var appCfg = AppConfig.Load();
                        if (appCfg?.ZKTeco != null && !string.IsNullOrWhiteSpace(appCfg.ZKTeco.IpAddress))
                        {
                            ips.Add(appCfg.ZKTeco.IpAddress);
                        }

                        // 2. Get the current C3 IP from connection monitor service
                        var monitorIp = ConnectionMonitorService.Instance.CurrentC3Ip;
                        if (!string.IsNullOrWhiteSpace(monitorIp) && !ips.Contains(monitorIp))
                        {
                            ips.Add(monitorIp);
                        }

                        // 3. Add existing controllers' IPs
                        try
                        {
                            var allControllers = await ParkingTopologyService.Instance.GetControllersAsync();
                            if (allControllers != null)
                            {
                                foreach (var ctrl in allControllers)
                                {
                                    if (!string.IsNullOrWhiteSpace(ctrl.IpAddress) && !ips.Contains(ctrl.IpAddress))
                                        ips.Add(ctrl.IpAddress);
                                }
                            }
                        }
                        catch { }

                        cb.ItemsSource = ips;
                        string currentIp = (string)(p.GetValue(_model) ?? string.Empty);
                        if (!string.IsNullOrEmpty(currentIp))
                        {
                            if (!ips.Contains(currentIp))
                            {
                                ips.Add(currentIp);
                                cb.ItemsSource = null;
                                cb.ItemsSource = ips;
                            }
                            cb.SelectedValue = currentIp;
                            cb.Text = currentIp;
                        }
                        else if (ips.Count > 0)
                        {
                            cb.SelectedIndex = 0;
                            p.SetValue(_model, ips[0]);
                        }
                        input = cb;
                    }
else if (p.Name == "TrangThai")
                    {
                        var cb = new ComboBox { 
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Width = 200, 
                            Style = (Style)Application.Current.FindResource("ModernComboBox") 
                        };
                        // bind ItemsSource and SelectedItem to window properties
                        cb.SetBinding(ComboBox.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(TrangThaiList)) { Source = this });
                        cb.SetBinding(ComboBox.SelectedItemProperty, new System.Windows.Data.Binding(nameof(SelectedTrangThai)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
                        input = cb;
                    }
                    else if (p.PropertyType == typeof(string)) 
                        input = new TextBox { Style = (Style)Application.Current.FindResource("ModernTextBox") };
                    else if (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?)) 
                        input = new TextBox { Width = 150, HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("ModernTextBox") };
                    else if (p.PropertyType == typeof(decimal)) 
                        input = new TextBox { Width = 150, HorizontalAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("ModernTextBox") };
                    else if (p.PropertyType == typeof(DateTime)) 
                        input = new DatePicker { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
                    else if (p.PropertyType == typeof(bool))
                        input = new CheckBox { Margin = new Thickness(0, 5, 0, 0) };

                    if (input != null)
                    {
                        input.Tag = p;
                        var val = p.GetValue(_model);
                        // Populate control with the existing value
                        switch (input)
                        {
                            case TextBox tb:
                                tb.Text = val?.ToString();
                                break;
                            case DatePicker dp:
                                if (val is DateTime dt)
                                    dp.SelectedDate = dt;
                                break;
                            case CheckBox chk:
                                if (val is bool b)
                                    chk.IsChecked = b;
                                break;
                            // For ComboBox foreign keys SiteId/ZoneId we already set SelectedValue earlier.
                            default:
                                break;
                        }

                        if (p.Name == "TrangThai" && val != null)
                        {
                            SelectedTrangThai = val.ToString();
                        }
                        sp.Children.Add(input);
                        
                        // Add Hint/Description below the control
                        var descAttr = p.GetCustomAttribute<DescriptionAttribute>();
                        if (descAttr != null && !string.IsNullOrEmpty(descAttr.Description))
                        {
                            sp.Children.Add(new TextBlock 
                            { 
                                Text = descAttr.Description, 
                                FontSize = 10, 
                                FontStyle = FontStyles.Italic,
                                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("TextMutedBrush"),
                                Margin = new Thickness(0, 3, 0, 0),
                                TextWrapping = TextWrapping.Wrap
                            });
                        }
                    }

                    FieldsPanel.Children.Add(sp);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error building fields: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyMatchingProperties(object entity)
{
    if (entity == null) return;
    var entityType = entity.GetType();
    var modelType = _model.GetType();

    // Copy each property that exists on both objects
    foreach (var prop in entityType.GetProperties())
    {
        var modelProp = modelType.GetProperty(prop.Name);
        if (modelProp != null && modelProp.CanWrite)
        {
            var val = prop.GetValue(entity);
            modelProp.SetValue(_model, val);

            // Update UI textbox if present
            foreach (var field in FieldsPanel.Children.OfType<StackPanel>())
            {
                var innerInput = field.Children.OfType<Control>().FirstOrDefault();
                if (innerInput?.Tag is PropertyInfo pi && pi.Name == prop.Name)
                {
                    if (innerInput is TextBox tb)
                        tb.Text = val?.ToString();
                }
            }
        }
    }
}
private void Save_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in FieldsPanel.Children.OfType<StackPanel>())
            {
                var input = child.Children.OfType<Control>().FirstOrDefault();
                if (input == null) continue;
                var prop = (PropertyInfo)input.Tag;
                if (prop == null) continue;

                var reqAttr = prop.GetCustomAttribute<RequiredAttribute>();
                string errorMsg = reqAttr?.ErrorMessage ?? $"Field {prop.Name} is required";
                bool isRequired = reqAttr != null || prop.Name != "Detail";

                if (input is TextBox tb)
                {
                    if (isRequired && string.IsNullOrWhiteSpace(tb.Text))
                    {
                        MessageBox.Show(errorMsg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    object value = null;
                    if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?))
                    {
                        if (string.IsNullOrWhiteSpace(tb.Text) && prop.PropertyType == typeof(int?))
                        {
                            value = null;
                        }
                        else if (int.TryParse(tb.Text, out int parsedInt))
                        {
                            value = parsedInt;
                        }
                        else
                        {
                            MessageBox.Show($"Field {prop.Name} phải là số nguyên hợp lệ.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else if (prop.PropertyType == typeof(decimal))
                    {
                        if (decimal.TryParse(tb.Text, out decimal parsedDec))
                        {
                            value = parsedDec;
                        }
                        else
                        {
                            MessageBox.Show($"Field {prop.Name} phải là số hợp lệ.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        value = tb.Text;
                    }
                    prop.SetValue(_model, value);
                }
                else if (input is ComboBox cb)
                {
                    if (isRequired && (cb.SelectedItem == null || string.IsNullOrWhiteSpace(cb.SelectedItem.ToString())))
                    {
                        MessageBox.Show(errorMsg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (prop.Name == "Id")
                    {
                        var selectedVal = cb.SelectedValue;
                        if (selectedVal != null)
                        {
                            prop.SetValue(_model, Convert.ChangeType(selectedVal, typeof(int)));
                        }
                    }
                    else if (prop.Name == "SiteId" || prop.Name == "ZoneId")
                    {
                        var selectedVal = cb.SelectedValue;
                        if (selectedVal != null)
                        {
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            prop.SetValue(_model, Convert.ChangeType(selectedVal, targetType));
                        }
                        else if (prop.PropertyType == typeof(int?))
                        {
                            prop.SetValue(_model, null);
                        }
                        else
                        {
                            prop.SetValue(_model, 0);
                        }
                    }
                    else if (prop.Name == "TrangThai")
                    {
                        // Use the bound SelectedTrangThai value
                        prop.SetValue(_model, SelectedTrangThai);
                    }
                    else
                    {
                        if (cb.SelectedItem != null)
                        {
                            if (prop.PropertyType == typeof(string))
                                prop.SetValue(_model, cb.SelectedItem.ToString());
                            else
                            {
                                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                prop.SetValue(_model, Convert.ChangeType(cb.SelectedItem, targetType));
                            }
                        }
                    }
                }
                else if (input is DatePicker dp)
                {
                    if (isRequired && !dp.SelectedDate.HasValue)
                    {
                        MessageBox.Show(errorMsg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (dp.SelectedDate.HasValue)
                        prop.SetValue(_model, dp.SelectedDate.Value);
                }
                else if (input is CheckBox chk)
                {
                    prop.SetValue(_model, chk.IsChecked ?? false);
                }
            }

            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
