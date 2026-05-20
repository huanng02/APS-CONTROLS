using System;
using System.Windows;

namespace QuanLyGiuXe.Views
{
    public partial class RFIDGiaHanDialog : Window
    {
        public int SelectedMonths { get; private set; }

        public RFIDGiaHanDialog(string cardUid, string bienSo)
        {
            InitializeComponent();
            this.DataContext = new { CardInfo = $"Mã thẻ: {cardUid}", PlateInfo = $"Biển số: {bienSo}" };
            txtMonths.Focus();
            txtMonths.SelectAll();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtMonths.Text, out int months) && months > 0)
            {
                SelectedMonths = months;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                txtError.Text = "Vui lòng nhập số tháng hợp lệ (> 0).";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
