using System;
using System.Windows;
using System.Windows.Controls;

namespace QuanLyGiuXe.Views
{
    public partial class ReasonInputDialog : Window
    {
        public string EnteredReason { get; private set; } = string.Empty;

        public ReasonInputDialog()
        {
            InitializeComponent();
            txtReason.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string reason = txtReason.Text?.Trim() ?? string.Empty;
            if (reason.Length >= 3)
            {
                EnteredReason = reason;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                txtError.Text = "Vui lòng nhập lý do cụ thể và chi tiết (tối thiểu 3 ký tự).";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void QuickReason_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                txtReason.Text = btn.Content?.ToString() ?? string.Empty;
                txtReason.Focus();
                txtReason.CaretIndex = txtReason.Text.Length;
            }
        }
    }
}
