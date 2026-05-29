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
        public ConnectDatabaseWindow()
        {
            InitializeComponent();
            _viewModel = new ConnectDatabaseViewModel();
            this.DataContext = _viewModel;
            
            // Initial load for password
            txtPassword.Password = _viewModel.Password;
            txtPasswordVisible.Text = _viewModel.Password;

            _viewModel.CloseAction = () =>
            {
                this.DialogResult = _viewModel.DialogResult;
                this.Close();
            };

            // Hủy toàn bộ tác vụ chạy ngầm khi cửa sổ bị đóng
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
            if (this.DataContext is ConnectDatabaseViewModel vm)
            {
                if (vm.Password != txtPassword.Password)
                {
                    vm.Password = txtPassword.Password;
                    txtPasswordVisible.Text = txtPassword.Password;
                }
            }
        }

        private void txtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.DataContext is ConnectDatabaseViewModel vm)
            {
                if (vm.Password != txtPasswordVisible.Text)
                {
                    vm.Password = txtPasswordVisible.Text;
                    txtPassword.Password = txtPasswordVisible.Text;
                }
            }
        }
    }
}


