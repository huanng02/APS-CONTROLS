using System;
using System.Windows;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Views
{
    public partial class CardInfoDialog : Window
    {
        public CardInfoDialog()
        {
            InitializeComponent();
        }

        public void SetCard(RFIDCard card)
        {
            if (card == null) return;
            txtUID.Text = card.UID;
            txtBienSo.Text = card.BienSo;
            txtLoaiVe.Text = card.LoaiVeId > 0 ? card.LoaiVeId.ToString() : "-";
            txtTrangThai.Text = string.IsNullOrWhiteSpace(card.TrangThai) ? "-" : card.TrangThai;
            txtNotes.Text = $"Ngày đăng ký: {card.NgayTao:yyyy-MM-dd}  {(card.NgayHetHan.HasValue ? " | Hết hạn: " + card.NgayHetHan.Value.ToString("yyyy-MM-dd") : string.Empty)}";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
