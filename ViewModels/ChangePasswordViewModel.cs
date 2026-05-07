using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.ViewModels
{
    internal class ChangePasswordViewModel : INotifyPropertyChanged
    {
        private readonly UserManagementService _service = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(name));
        }

        // =========================
        // Properties
        // =========================

        private string _oldPassword = "";
        public string OldPassword
        {
            get => _oldPassword;
            set
            {
                _oldPassword = value;
                OnPropertyChanged(nameof(OldPassword));
            }
        }

        private string _newPassword = "";
        public string NewPassword
        {
            get => _newPassword;
            set
            {
                _newPassword = value;
                OnPropertyChanged(nameof(NewPassword));
            }
        }

        private string _confirmPassword = "";
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                _confirmPassword = value;
                OnPropertyChanged(nameof(ConfirmPassword));
            }
        }

        // =========================
        // Commands
        // =========================

        public ICommand SaveCommand { get; }

        // =========================
        // Constructor
        // =========================

        public ChangePasswordViewModel()
        {
            SaveCommand = new RelayCommand(async w =>
            {
                await SaveAsync(w as Window);
            });
        }

        // =========================
        // Save
        // =========================

        private async Task SaveAsync(Window? window)
        {
            var result = await _service.ChangePasswordAsync(
                CurrentUser.Id,
                OldPassword,
                NewPassword,
                ConfirmPassword);

            if (!result.Success)
            {
                MessageBox.Show(
                    result.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            MessageBox.Show(
                result.Message,
                "Thành công",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            window?.Close();
        }
    }
}
