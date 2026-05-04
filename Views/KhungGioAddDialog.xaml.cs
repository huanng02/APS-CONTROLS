using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyGiuXe.Views
{
    public partial class KhungGioAddDialog : Window
    {
        public string TenKhungGio { get; private set; }
        public TimeSpan GioBatDau { get; private set; }
        public TimeSpan GioKetThuc { get; private set; }
        
        // Danh sách các khung đã tồn tại để hiển thị
        public List<(string Name, TimeSpan Start, TimeSpan End)> ExistingSlots { get; set; } = new List<(string, TimeSpan, TimeSpan)>();

        // Callback function to check for overlaps in the ViewModel
        public Func<TimeSpan, TimeSpan, bool> CheckOverlapFunc { get; set; }

        private bool _isInitializing = true;

        public KhungGioAddDialog()
        {
            InitializeComponent();

            // Populate Hours (00-23)
            for (int i = 0; i < 24; i++)
            {
                string h = i.ToString("D2");
                cbStartHour.Items.Add(h);
                cbEndHour.Items.Add(h);
            }

            // Populate Minutes and Seconds (00-59)
            for (int i = 0; i < 60; i++)
            {
                string m = i.ToString("D2");
                cbStartMin.Items.Add(m);
                cbEndMin.Items.Add(m);
                cbStartSec.Items.Add(m);
                cbEndSec.Items.Add(m);
            }

            // Default values: 06:00:00 to 22:00:00
            cbStartHour.SelectedIndex = 6;
            cbStartMin.SelectedIndex = 0;
            cbStartSec.SelectedIndex = 0;
            cbEndHour.SelectedIndex = 22;
            cbEndMin.SelectedIndex = 0;
            cbEndSec.SelectedIndex = 0;

            _isInitializing = false;
            
            this.Loaded += KhungGioAddDialog_Loaded;
        }

        private void KhungGioAddDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (ExistingSlots.Any())
            {
                var slotsText = string.Join("\n", ExistingSlots.Select(x => $"• {x.Name}: {x.Start:hh\\:mm\\:ss} - {x.End:hh\\:mm\\:ss}"));
                txtExisting.Text = "Các khung giờ đã tồn tại:\n" + slotsText;
            }
            ValidateTimeSelection();
        }

        private void TimeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateTimeSelection();
        }

        private void ValidateTimeSelection()
        {
            if (_isInitializing || btnSave == null) return;

            txtError.Text = "";
            btnSave.IsEnabled = true;

            if (cbStartHour.SelectedItem == null || cbStartMin.SelectedItem == null || cbStartSec.SelectedItem == null ||
                cbEndHour.SelectedItem == null || cbEndMin.SelectedItem == null || cbEndSec.SelectedItem == null)
            {
                btnSave.IsEnabled = false;
                return;
            }

            int sHour = int.Parse(cbStartHour.SelectedItem.ToString());
            int sMin = int.Parse(cbStartMin.SelectedItem.ToString());
            int sSec = int.Parse(cbStartSec.SelectedItem.ToString());
            TimeSpan start = new TimeSpan(sHour, sMin, sSec);

            int eHour = int.Parse(cbEndHour.SelectedItem.ToString());
            int eMin = int.Parse(cbEndMin.SelectedItem.ToString());
            int eSec = int.Parse(cbEndSec.SelectedItem.ToString());
            TimeSpan end = new TimeSpan(eHour, eMin, eSec);

            if (start == end)
            {
                txtError.Text = "Thời gian bắt đầu không được bằng kết thúc.";
                btnSave.IsEnabled = false;
                return;
            }

            if (CheckOverlapFunc != null && CheckOverlapFunc(start, end))
            {
                txtError.Text = "Khung giờ này bị trùng lặp (Overlaps existing slot). Đã tự động khoá lưu.";
                btnSave.IsEnabled = false;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            txtError.Text = "";
            
            if (string.IsNullOrWhiteSpace(txtTenKhung.Text))
            {
                txtError.Text = "Vui lòng nhập tên khung giờ.";
                return;
            }

            int sHour = int.Parse(cbStartHour.SelectedItem.ToString());
            int sMin = int.Parse(cbStartMin.SelectedItem.ToString());
            int sSec = int.Parse(cbStartSec.SelectedItem.ToString());
            TimeSpan start = new TimeSpan(sHour, sMin, sSec);

            int eHour = int.Parse(cbEndHour.SelectedItem.ToString());
            int eMin = int.Parse(cbEndMin.SelectedItem.ToString());
            int eSec = int.Parse(cbEndSec.SelectedItem.ToString());
            TimeSpan end = new TimeSpan(eHour, eMin, eSec);

            TenKhungGio = txtTenKhung.Text;
            GioBatDau = start;
            GioKetThuc = end;

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
