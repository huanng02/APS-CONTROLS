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
            var rfid = RFIDService.Instance;
            rfid.OnCardScanned += (uid) =>
            {
                Dispatcher.Invoke(() =>
                {
                    txtUID.Text = uid;
                });
            };
        }
        

        private void SaveCard(object sender, RoutedEventArgs e)
        {
            string uid = RFIDService.ChuanHoaUID(txtUID.Text);
            string bienSo = txtBienSo.Text;
            string loaiThe = (cbLoaiThe.SelectedItem as ComboBoxItem).Content.ToString();

            using (SqlConnection conn =
                new SqlConnection("Server=localhost\\SQLEXPRESS;Database=BaiXe;Trusted_Connection=True;"))
            {
                conn.Open();

                string query =
                "INSERT INTO RFIDCards(CardUID,BienSo,LoaiThe) VALUES(@uid,@bs,@lt)";

                SqlCommand cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.Parameters.AddWithValue("@bs", bienSo);
                cmd.Parameters.AddWithValue("@lt", loaiThe);

                cmd.ExecuteNonQuery();

            }

            MessageBox.Show("Đăng ký thẻ thành công");
        }
    }
}
