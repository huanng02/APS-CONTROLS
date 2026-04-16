using System;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class QuanLyThe : Window
    {
        public QuanLyThe()
        {
            InitializeComponent();
            RFIDService.Instance.OnCardScanned += OnCardScanned;
            C3200Service.Instance.OnCardScanned += OnC3200CardScanned;
        }

        protected override void OnClosed(EventArgs e)
        {
            RFIDService.Instance.OnCardScanned -= OnCardScanned;
            C3200Service.Instance.OnCardScanned -= OnC3200CardScanned;
            base.OnClosed(e);
        }

        private void OnCardScanned(string uid) =>
            Dispatcher.Invoke(() => txtUID.Text = uid);

        private void OnC3200CardScanned(string uid, int door) =>
            Dispatcher.Invoke(() => txtUID.Text = uid);

        private void SaveCard(object sender, RoutedEventArgs e)
        {
            string uid = RFIDService.ChuanHoaUID(txtUID.Text);
            string bienSo = txtBienSo.Text.ToUpper();
            string? loaiThe = (cbLoaiThe.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(uid))
            { MessageBox.Show("❌ Chưa quét thẻ!"); return; }

            if (string.IsNullOrEmpty(bienSo))
            { MessageBox.Show("❌ Vui lòng nhập biển số!"); return; }

            if (string.IsNullOrEmpty(loaiThe))
            { MessageBox.Show("❌ Vui lòng chọn loại thẻ!"); return; }

            var regex = new System.Text.RegularExpressions.Regex(@"^\d{2}([A-Z]\d{5,6}|[A-Z]{1,2}\d{4,5})$");
            if (!regex.IsMatch(bienSo))
            { MessageBox.Show("❌ Biển số không đúng định dạng! (VD: 50A12345)"); return; }

            try
            {
                var db = new DatabaseService();
                if (db.CheckCardExists(uid))
                { MessageBox.Show("❌ Thẻ này đã được đăng ký!"); return; }

                db.AddRFIDCards(uid, bienSo, loaiThe);
                MessageBox.Show("✅ Đăng ký thẻ thành công");
            }
            catch (SqlException ex) when (ex.Number == 2627)
            {
                MessageBox.Show("❌ Thẻ này đã tồn tại!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Lỗi: " + ex.Message);
            }

            txtUID.Clear();
            txtBienSo.Clear();
            cbLoaiThe.SelectedIndex = 0;
            txtBienSo.Focus();
        }

        private void txtBienSo_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            if (!char.IsLetterOrDigit(e.Text, 0) || textBox.Text.Length >= 9)
            { e.Handled = true; return; }

            int pos = textBox.SelectionStart;
            textBox.Text = textBox.Text.Insert(pos, e.Text.ToUpper());
            textBox.SelectionStart = pos + 1;
            e.Handled = true;
        }

        private void txtBienSo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            int cursor = textBox.SelectionStart;
            string clean = new(textBox.Text.Where(char.IsLetterOrDigit).ToArray());
            string upper = clean.ToUpper();

            if (textBox.Text != upper)
            {
                textBox.Text = upper;
                textBox.SelectionStart = Math.Min(cursor, upper.Length);
            }
        }

        private void txtUID_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
