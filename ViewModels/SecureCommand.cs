using System;
using System.Windows.Input;

namespace QuanLyGiuXe.ViewModels
{
    public class SecureCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        private readonly string _requiredPermission;

        public SecureCommand(string requiredPermission, Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _requiredPermission = requiredPermission ?? throw new ArgumentNullException(nameof(requiredPermission));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            // Check authorization layer first
            if (!Services.PermissionService.Instance.CheckPermission(_requiredPermission))
            {
                return false;
            }

            // Check specific delegate condition
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            // Perform active authorization guard validation
            Services.AuthorizationGuard.Protect(_requiredPermission, _execute.Method.Name);

            _execute(parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class SecureCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Predicate<T?>? _canExecute;
        private readonly string _requiredPermission;

        public SecureCommand(string requiredPermission, Action<T?> execute, Predicate<T?>? canExecute = null)
        {
            _requiredPermission = requiredPermission ?? throw new ArgumentNullException(nameof(requiredPermission));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            // Check authorization layer first
            if (!Services.PermissionService.Instance.CheckPermission(_requiredPermission))
            {
                return false;
            }

            // Check specific delegate condition
            return _canExecute == null || _canExecute((T?)parameter);
        }

        public void Execute(object? parameter)
        {
            // Perform active authorization guard validation
            Services.AuthorizationGuard.Protect(_requiredPermission, _execute.Method.Name);

            _execute((T?)parameter);
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
