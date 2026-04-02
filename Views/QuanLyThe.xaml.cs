using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using QuanLyGiuXe.Services;
namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Interaction logic for QuanLyThe.xaml
    /// </summary>
    public partial class QuanLyThe : Window
    {
        SerialPort port;
        public QuanLyThe()
        {
            InitializeComponent();
            RFIDService.Instance.OnCardScanned += RFID_OnCardScanned;
        }
            private void RFID_OnCardScanned(string uid)
        {
            Dispatcher.Invoke(() =>
            {
                txtUID.Text = uid;
            });
        }
        protected override void OnClosed(EventArgs e)
        {
            RFIDService.Instance.OnCardScanned -= RFID_OnCardScanned;
            base.OnClosed(e);
        }



        private void SaveCard(object sender, RoutedEventArgs e)
        {
            string uid = RFIDService.ChuanHoaUID(txtUID.Text);
            string bienSo = txtBienSo.Text;
            string loaiThe = (cbLoaiThe.SelectedItem as ComboBoxItem)?.Content?.ToString();
            // VALIDATE 

            if (string.IsNullOrEmpty(uid))
            {
                MessageBox.Show("❌ Chưa quét thẻ!");
                return;
            }

            if (string.IsNullOrEmpty(bienSo))
            {
                MessageBox.Show("❌ Vui lòng nhập biển số!");
                return;
            }

            if (string.IsNullOrEmpty(loaiThe))
            {
                MessageBox.Show("❌ Vui lòng chọn loại thẻ!");
                return;
            }

            // CHECK FORMAT BIỂN SỐ
            // Ví dụ hợp lệ: 50A12345 hoặc 50AC12345
            var regex = new System.Text.RegularExpressions.Regex(@"^\d{2}([A-Z]\d{5,6}|[A-Z]{1,2}\d{4,5})$");

            if (!regex.IsMatch(bienSo.ToUpper()))
            {
                MessageBox.Show("❌ Biển số không đúng định dạng! (VD: 50A12345)");
                return;
            }

            using (SqlConnection conn =
                new SqlConnection("Server=.;Database=Baixe;Trusted_Connection=True;"))
            {
                conn.Open();

                // CHECK TRÙNG UID
                string checkQuery = "SELECT COUNT(*) FROM RFIDCards WHERE CardUID = @uid";
                SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@uid", uid);

                int count = (int)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    MessageBox.Show("❌ Thẻ này đã được đăng ký!");
                    return;
                }

                // INSERT
                string insertQuery =
                "INSERT INTO RFIDCards(CardUID,BienSo,LoaiThe) VALUES(@uid,@bs,@lt)";

                SqlCommand cmd = new SqlCommand(insertQuery, conn);

                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.Parameters.AddWithValue("@bs", bienSo);
                cmd.Parameters.AddWithValue("@lt", loaiThe);

                try
                {
                    cmd.ExecuteNonQuery();
                    MessageBox.Show("Đăng ký thẻ thành công");
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 2627) // lỗi duplicate
                    {
                        MessageBox.Show("Thẻ này đã tồn tại!");
                    }
                    else
                    {
                        MessageBox.Show("Lỗi: " + ex.Message);
                    }
                }
            }

            txtUID.Clear();
            txtBienSo.Clear();
            cbLoaiThe.SelectedIndex = 0; // reset combobox

            txtBienSo.Focus(); // trỏ chuột về nhập biển số
        }
        private void txtBienSo_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;

            // nếu không phải chữ/số → chặn
            if (!char.IsLetterOrDigit(e.Text, 0))
            {
                e.Handled = true;
                return;
            }
            //giới hạn độ dài của biển số là 9
            if (textBox.Text.Length >= 9)
            {
                e.Handled = true;
                return;
            }

            // tự xử lý viết hoa
            string newText = e.Text.ToUpper();

            int selectionStart = textBox.SelectionStart;

            textBox.Text = textBox.Text.Insert(selectionStart, newText);
            textBox.SelectionStart = selectionStart + newText.Length;

            // chặn input mặc định
            e.Handled = true;
        }
        private void txtBienSo_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;

            int cursor = textBox.SelectionStart;

            string newText = new string(
                textBox.Text
                .Where(char.IsLetterOrDigit) // loại ký tự rác
                .ToArray()
            ).ToUpper();

            if (textBox.Text != newText)
            {
                textBox.Text = newText;
                textBox.SelectionStart = cursor;
            }
        }
    }
}
