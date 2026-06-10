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

            // 1. Initial UI updates from cache/parameters
            txtUID.Text = card.UID;
            txtCardName.Text = string.IsNullOrWhiteSpace(card.CardName) ? "-" : card.CardName;
            txtBienSo.Text = string.IsNullOrWhiteSpace(card.BienSo) ? "-" : card.BienSo;
            txtLoaiVe.Text = card.LoaiVeId > 0 ? card.LoaiVeId.ToString() : "-";

            // Set Status styling
            bool isActive = string.Equals(card.TrangThai, "Active", StringComparison.OrdinalIgnoreCase);
            if (isActive)
            {
                txtTrangThai.Text = "HOẠT ĐỘNG";
                txtTrangThai.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 128, 61));
                brdStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231));
            }
            else
            {
                txtTrangThai.Text = string.IsNullOrWhiteSpace(card.TrangThai) ? "KHÓA" : card.TrangThai.ToUpper();
                txtTrangThai.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
                brdStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 226, 226));
            }

            // Date details
            txtDateInfo.Text = $"Ngày đăng ký: {card.NgayTao:dd/MM/yyyy}\n" + 
                              (card.NgayHetHan.HasValue ? $"Ngày hết hạn: {card.NgayHetHan.Value:dd/MM/yyyy}" : "Hạn dùng: Vô thời hạn");

            brdEmployeeInfo.Visibility = Visibility.Collapsed;
            brdNoEmployee.Visibility = Visibility.Visible;

            // 2. Resolve LoaiVe and LoaiXe names from database asynchronously
            string loaiVeName = card.LoaiVeId > 0 ? card.LoaiVeId.ToString() : "-";
            string loaiXeName = card.LoaiXeId > 0 ? card.LoaiXeId.ToString() : "-";

            await Task.Run(() => {
                try
                {
                    var db = new DatabaseService();
                    var lvList = db.GetLoaiVe();
                    var lv = lvList.FirstOrDefault(x => x.Id == card.LoaiVeId);
                    if (lv != null) loaiVeName = lv.TenLoai ?? loaiVeName;
                }
                catch { }
                try
                {
                    var db = new DatabaseService();
                    var lxList = db.GetLoaiXe();
                    var lx = lxList.FirstOrDefault(x => x.Id == card.LoaiXeId);
                    if (lx != null) loaiXeName = lx.TenLoai ?? loaiXeName;
                }
                catch { }
            });

            txtLoaiVe.Text = $"{loaiVeName} ({loaiXeName})";

            // 3. Query and load Owner employee details asynchronously
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
