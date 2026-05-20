using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            BuildFields();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            // Allow dragging
            this.MouseDown += (s, e) => {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    this.DragMove();
            };
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private void BuildFields()
        {
            FieldsPanel.Children.Clear();
            var props = _model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Id" && p.CanWrite && (p.PropertyType == typeof(string) || p.PropertyType == typeof(int) || p.PropertyType == typeof(decimal) || p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(int?) || p.PropertyType == typeof(bool)));

            // Special handling: if property name is TrangThai, render ComboBox with predefined values
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
                if (p.Name == "TrangThai")
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
                    if (input is TextBox tb) tb.Text = val?.ToString() ?? string.Empty;
                    if (input is DatePicker dp && val is DateTime dt) dp.SelectedDate = dt;
                    if (input is CheckBox chk && val is bool b) chk.IsChecked = b;
                    if (input is ComboBox cbb && val != null) SelectedTrangThai = val.ToString();
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
                    if (cb.SelectedItem != null)
                    {
                        if (prop.PropertyType == typeof(string)) prop.SetValue(_model, cb.SelectedItem.ToString());
                        else
                        {
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            prop.SetValue(_model, Convert.ChangeType(cb.SelectedItem, targetType));
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
