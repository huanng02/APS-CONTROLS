using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class ConnectDatabaseWindow : Window
    {
        private ConnectDatabaseViewModel _viewModel;
        private bool _isUpdatingPassword = false;
        public ConnectDatabaseWindow()
        {
            InitializeComponent();
            _viewModel = new ConnectDatabaseViewModel();
            this.DataContext = _viewModel;

            txtPassword.Password = _viewModel.Password ?? "";
            txtPasswordVisible.Text = _viewModel.Password ?? "";

            // ĐĂNG KÝ SỰ KIỆN: Khi ViewModel thay đổi dữ liệu
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.Password))
                {
                    // Nếu chính UI đang thay đổi mật khẩu thì bỏ qua, không cập nhật ngược lại nữa
                    if (_isUpdatingPassword) return;

                    // Bật cờ chặn: Báo hiệu hệ thống đang cập nhật từ ViewModel xuống UI
                    _isUpdatingPassword = true;

                    if (txtPassword.Password != _viewModel.Password)
                    {
                        txtPassword.Password = _viewModel.Password ?? "";
                    }
                    if (txtPasswordVisible.Text != _viewModel.Password)
                    {
                        txtPasswordVisible.Text = _viewModel.Password ?? "";
                    }

                    // Tắt cờ chặn sau khi cập nhật xong
                    _isUpdatingPassword = false;
                }
            };

            _viewModel.CloseAction = () =>
            {
                this.DialogResult = _viewModel.DialogResult;
                this.Close();
            };

            Closed += (s, e) => _viewModel?.CancelPendingTasks();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void InputField_GotFocus(object sender, RoutedEventArgs e)
        {
            var border = FindParentBorder(sender as DependencyObject);
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2563eb"));
                border.BorderThickness = new Thickness(2);
            }
        }

        private void InputField_LostFocus(object sender, RoutedEventArgs e)
        {
            var border = FindParentBorder(sender as DependencyObject);
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#cbd5e1"));
                border.BorderThickness = new Thickness(1.5);
            }
        }

        private Border FindParentBorder(DependencyObject child)
        {
            if (child == null) return null;
            
            DependencyObject parentDep = VisualTreeHelper.GetParent(child);
            while (parentDep != null && !(parentDep is Border))
            {
                parentDep = VisualTreeHelper.GetParent(parentDep);
            }
            return parentDep as Border;
        }

        private void txtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingPassword) return; // Nếu đang cập nhật từ VM thì bỏ qua

            if (this.DataContext is ConnectDatabaseViewModel vm)
            {
                if (vm.Password != txtPassword.Password)
                {
                    _isUpdatingPassword = true; // Bật cờ chặn
                    vm.Password = txtPassword.Password;
                    txtPasswordVisible.Text = txtPassword.Password;
                    _isUpdatingPassword = false; // Tắt cờ
                }
            }
        }

        private void txtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPassword) return; // Nếu đang cập nhật từ VM thì bỏ qua

            if (this.DataContext is ConnectDatabaseViewModel vm)
            {
                if (vm.Password != txtPasswordVisible.Text)
                {
                    _isUpdatingPassword = true; // Bật cờ chặn
                    vm.Password = txtPasswordVisible.Text;
                    txtPassword.Password = txtPasswordVisible.Text;
                    _isUpdatingPassword = false; // Tắt cờ
                }
            }
        }
    }
}


