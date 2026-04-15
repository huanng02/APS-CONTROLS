using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

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
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private void BuildFields()
        {
            FieldsPanel.Children.Clear();
            var props = _model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Id" && p.CanWrite && (p.PropertyType == typeof(string) || p.PropertyType == typeof(int) || p.PropertyType == typeof(decimal) || p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(int?)));

            // Special handling: if property name is TrangThai, render ComboBox with predefined values
            foreach (var p in props)
            {
                var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 6, 0, 6) };
                sp.Children.Add(new TextBlock { Text = p.Name });
                Control input = null;
                if (p.Name == "TrangThai")
                {
                    var cb = new ComboBox { Width = 200 };
                    // bind ItemsSource and SelectedItem to window properties
                    cb.SetBinding(ComboBox.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(TrangThaiList)) { Source = this });
                    cb.SetBinding(ComboBox.SelectedItemProperty, new System.Windows.Data.Binding(nameof(SelectedTrangThai)) { Source = this, Mode = System.Windows.Data.BindingMode.TwoWay });
                    input = cb;
                }
                else if (p.PropertyType == typeof(string)) input = new TextBox { Width = 320 };
                else if (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?)) input = new TextBox { Width = 120 };
                else if (p.PropertyType == typeof(decimal)) input = new TextBox { Width = 120 };
                else if (p.PropertyType == typeof(DateTime)) input = new DatePicker { Width = 200 };

                if (input != null)
                {
                    input.Tag = p;
                    var val = p.GetValue(_model);
                    if (input is TextBox tb) tb.Text = val?.ToString() ?? string.Empty;
                    if (input is DatePicker dp && val is DateTime dt) dp.SelectedDate = dt;
                    if (input is ComboBox cbb && val != null) SelectedTrangThai = val.ToString();
                    sp.Children.Add(input);
                }

                FieldsPanel.Children.Add(sp);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // simple validation: no empty strings
            foreach (var child in FieldsPanel.Children.OfType<StackPanel>())
            {
                var input = child.Children.OfType<Control>().LastOrDefault();
                if (input == null) continue;
                var prop = (PropertyInfo)input.Tag;
                if (input is TextBox tb)
                {
                    if (string.IsNullOrWhiteSpace(tb.Text))
                    {
                        MessageBox.Show($"Field {prop.Name} is required", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    object value = null;
                    if (prop.PropertyType == typeof(int) || prop.PropertyType == typeof(int?)) value = int.Parse(tb.Text);
                    else if (prop.PropertyType == typeof(decimal)) value = decimal.Parse(tb.Text);
                    else value = tb.Text;
                    prop.SetValue(_model, value);
                }
                else if (input is ComboBox cb)
                {
                    // SelectedItem may be string or object; require selection
                    if (cb.SelectedItem == null || string.IsNullOrWhiteSpace(cb.SelectedItem.ToString()))
                    {
                        MessageBox.Show($"Field {prop.Name} is required", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // set as string
                    if (prop.PropertyType == typeof(string)) prop.SetValue(_model, cb.SelectedItem.ToString());
                    else
                    {
                        // try convert
                        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        prop.SetValue(_model, Convert.ChangeType(cb.SelectedItem, targetType));
                    }
                }
                else if (input is DatePicker dp)
                {
                    if (!dp.SelectedDate.HasValue)
                    {
                        MessageBox.Show($"Field {prop.Name} is required", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    prop.SetValue(_model, dp.SelectedDate.Value);
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
