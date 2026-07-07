using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class CardInfoDialog : Window
    {
        public CardInfoDialog()
        {
            InitializeComponent();
        }

        public async void SetCard(RFIDCard card)
        {
            if (card == null) return;

            brdEmployeeInfo.Visibility = Visibility.Collapsed;
            brdNoEmployee.Visibility = Visibility.Visible;

            // Query and load Owner employee details asynchronously
            if (card.EmployeeId.HasValue)
            {
                try
                {
                    var service = new EnterpriseCrudService();
                    var emp = await Task.Run(() => service.GetEmployeeById(card.EmployeeId.Value));
                    if (emp != null)
                    {
                        brdEmployeeInfo.Visibility = Visibility.Visible;
                        brdNoEmployee.Visibility = Visibility.Collapsed;

                        txtEmpName.Text = emp.FullName;
                        txtEmpCode.Text = $"Mã NV: {emp.EmployeeCode}";
                        txtEmpCompany.Text = string.IsNullOrWhiteSpace(emp.CompanyName) ? "-" : emp.CompanyName;
                        txtEmpDept.Text = string.IsNullOrWhiteSpace(emp.DepartmentName) ? "-" : emp.DepartmentName;
                        txtEmpPos.Text = string.IsNullOrWhiteSpace(emp.PositionName) ? "-" : emp.PositionName;
                        txtEmpPhone.Text = string.IsNullOrWhiteSpace(emp.Phone) ? "-" : emp.Phone;

                        if (!string.IsNullOrEmpty(emp.Avatar))
                        {
                            imgAvatar.Source = ConvertBase64ToImage(emp.Avatar);
                        }
                        else
                        {
                            imgAvatar.Source = null;
                        }
                    }
                }
                catch { }
            }
        }

        private static ImageSource? ConvertBase64ToImage(string? base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] binaryData = Convert.FromBase64String(base64);
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = new MemoryStream(binaryData);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch
            {
                return null;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
