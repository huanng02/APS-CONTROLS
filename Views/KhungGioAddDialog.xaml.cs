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

        public List<(string Name, TimeSpan Start, TimeSpan End)> ExistingSlots
        {
            get;
            set;
        } = new List<(string, TimeSpan, TimeSpan)>();

        public Func<TimeSpan, TimeSpan, bool> CheckOverlapFunc
        {
            get;
            set;
        }

        private bool _isInitializing = true;

        public KhungGioAddDialog()
        {
            InitializeComponent();

            LoadTimeData();

            _isInitializing = false;

            Loaded += KhungGioAddDialog_Loaded;
        }

        private void LoadTimeData()
        {
            for (int i = 0; i < 24; i++)
            {
                string h = i.ToString("D2");

                cbStartHour.Items.Add(h);
                cbEndHour.Items.Add(h);
            }

            for (int i = 0; i < 60; i++)
            {
                string m = i.ToString("D2");

                cbStartMin.Items.Add(m);
                cbEndMin.Items.Add(m);

                cbStartSec.Items.Add(m);
                cbEndSec.Items.Add(m);
            }

            cbStartHour.SelectedIndex = 6;
            cbStartMin.SelectedIndex = 0;
            cbStartSec.SelectedIndex = 0;

            cbEndHour.SelectedIndex = 22;
            cbEndMin.SelectedIndex = 0;
            cbEndSec.SelectedIndex = 0;
        }

        private void KhungGioAddDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (ExistingSlots.Any())
            {
                txtExisting.Text =
                    "Các khung giờ hiện có:\n\n" +
                    string.Join(
                        "\n",
                        ExistingSlots.Select(x =>
                            $"• {x.Name}: {x.Start:hh\\:mm\\:ss} - {x.End:hh\\:mm\\:ss}")
                    );
            }
            else
            {
                txtExisting.Text = "Chưa có khung giờ nào.";
            }
        }

        private void TimeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateTimeSelection();
        }

        private void ValidateTimeSelection()
        {
            if (_isInitializing || btnSave == null)
                return;

            txtError.Text = "";
            errorBorder.Visibility = Visibility.Collapsed;

            btnSave.IsEnabled = true;

            if (cbStartHour.SelectedItem == null ||
                cbStartMin.SelectedItem == null ||
                cbStartSec.SelectedItem == null ||
                cbEndHour.SelectedItem == null ||
                cbEndMin.SelectedItem == null ||
                cbEndSec.SelectedItem == null)
            {
                btnSave.IsEnabled = false;
                return;
            }

            int sHour = int.Parse(cbStartHour.SelectedItem.ToString());
            int sMin = int.Parse(cbStartMin.SelectedItem.ToString());
            int sSec = int.Parse(cbStartSec.SelectedItem.ToString());

            int eHour = int.Parse(cbEndHour.SelectedItem.ToString());
            int eMin = int.Parse(cbEndMin.SelectedItem.ToString());
            int eSec = int.Parse(cbEndSec.SelectedItem.ToString());

            TimeSpan start = new TimeSpan(sHour, sMin, sSec);
            TimeSpan end = new TimeSpan(eHour, eMin, eSec);

            if (start == end)
            {
                txtError.Text =
                    "Thời gian bắt đầu không được bằng thời gian kết thúc.";

                errorBorder.Visibility = Visibility.Visible;

                btnSave.IsEnabled = false;

                return;
            }

            if (CheckOverlapFunc != null &&
                CheckOverlapFunc(start, end))
            {
                txtError.Text =
                    "Khung giờ này đang bị trùng với khung giờ khác.";

                errorBorder.Visibility = Visibility.Visible;

                btnSave.IsEnabled = false;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            txtError.Text = "";
            errorBorder.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(txtTenKhung.Text))
            {
                txtError.Text = "Vui lòng nhập tên khung giờ.";

                errorBorder.Visibility = Visibility.Visible;

                txtTenKhung.Focus();

                return;
            }

            int sHour = int.Parse(cbStartHour.SelectedItem.ToString());
            int sMin = int.Parse(cbStartMin.SelectedItem.ToString());
            int sSec = int.Parse(cbStartSec.SelectedItem.ToString());

            int eHour = int.Parse(cbEndHour.SelectedItem.ToString());
            int eMin = int.Parse(cbEndMin.SelectedItem.ToString());
            int eSec = int.Parse(cbEndSec.SelectedItem.ToString());

            TimeSpan start = new TimeSpan(sHour, sMin, sSec);
            TimeSpan end = new TimeSpan(eHour, eMin, eSec);

            if (start == end)
            {
                txtError.Text =
                    "Thời gian bắt đầu không được bằng thời gian kết thúc.";

                errorBorder.Visibility = Visibility.Visible;

                return;
            }

            if (CheckOverlapFunc != null &&
                CheckOverlapFunc(start, end))
            {
                txtError.Text =
                    "Khung giờ đang bị trùng.";

                errorBorder.Visibility = Visibility.Visible;

                return;
            }

            TenKhungGio = txtTenKhung.Text.Trim();

            GioBatDau = start;

            GioKetThuc = end;

            DialogResult = true;

            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;

            Close();
        }
    }
}