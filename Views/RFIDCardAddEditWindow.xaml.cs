using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using QuanLyGiuXe.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    public partial class RFIDCardAddEditWindow : Window
    {
        private readonly LoaiVeService _loaiVeService = new LoaiVeService();
        private readonly LoaiXeService _loaiXeService = new LoaiXeService();

        // window created without manipulating UI controls directly; DataContext must be set to RFIDCardWizardViewModel
        private ViewModels.RFIDCardWizardViewModel _attachedVm;
        public RFIDCardAddEditWindow(RFIDCards model)
        {
            InitializeComponent();
            this.DataContextChanged += RFIDCardAddEditWindow_DataContextChanged;
            this.Loaded += RFIDCardAddEditWindow_Loaded;

            // If a model is provided, prepare a wizard ViewModel for Add flow so the window can work standalone.
            if (model != null)
            {
                var vm = new ViewModels.RFIDCardWizardViewModel();
                vm.InitForAdd();
                // copy initial fields from model
                vm.CardUID = model.CardUID;
                vm.BienSo = model.BienSo;
                vm.TrangThai = model.TrangThai;
                this.DataContext = vm;
            }
        }
        private void TxtCardUID_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // block any manual text input
            e.Handled = true;
        }

        private void TxtCardUID_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // block paste via Ctrl+V or Shift+Insert
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
                {
                    e.Handled = true;
                    return;
                }
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && e.Key == Key.Insert)
                {
                    e.Handled = true;
                    return;
                }
                // allow navigation keys (Tab, arrows) so focus can move
                if (e.Key == Key.Tab || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
                    return;

                // otherwise block key input
                e.Handled = true;
            }
            catch { e.Handled = true; }
        }

        private void TxtCardUID_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // disable context menu to prevent paste
            e.Handled = true;
        }
            // If model is null, caller (Update flow) should set DataContext to a preloaded ViewModel.

        private void RFIDCardAddEditWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // detach previous
            if (_attachedVm != null)
            {
                _attachedVm.RequestClose -= AttachedVm_RequestClose;
                _attachedVm = null;
            }

            if (this.DataContext is ViewModels.RFIDCardWizardViewModel newVm)
            {
                _attachedVm = newVm;
                _attachedVm.RequestClose += AttachedVm_RequestClose;
                // set focus if appropriate
                SetInitialFocus();
            }
        }

        private void RFIDCardAddEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetInitialFocus();
        }

        private void SetInitialFocus()
        {
            try
            {
                var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
                if (vm == null) return;

                // Focus CardUID only in Add mode so scanner (keyboard emulation) types into textbox
                if (vm.IsAddMode && this.FindName("TxtCardUID") is TextBox tb)
                {
                    // focus after render
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        tb.Focus();
                        Keyboard.Focus(tb);
                        tb.SelectAll();
                    }));
                }
            }
            catch { }
        }

        private void AttachedVm_RequestClose(bool? result)
        {
            // marshal to UI thread
            Dispatcher.Invoke(() =>
            {
                this.DialogResult = result;
                this.Close();
            });
        }

        // Removed dynamic UI creation. XAML contains step grids and bindings; code-behind only handles commands via ViewModel.

        private void CbLoaiVe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Forward selection change to ViewModel if available
            var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
            if (vm == null) return;
            // Keep method intentionally minimal for single-page layout. ViewModel should handle any business logic when LoaiVeId changes via binding.
        }

        // NgayDangKy -> NgayHetHan handled by ViewModel (binding)

        private void ShowInlineError(string message)
        {
            // show message box for now
            MessageBox.Show(message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void TxtCardUID_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox tb)
                {
                    // If in Add mode focus the textbox to allow fast scanner input
                    var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
                    if (vm != null && vm.IsAddMode)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            tb.Focus();
                            Keyboard.Focus(tb);
                            tb.SelectAll();
                        }));
                    }
                }
            }
            catch { }
        }
        private void OnNext(object sender, RoutedEventArgs e)
        {
            // No-op in single-page layout. Navigation handled by showing all sections.
        }

        private void OnBack(object sender, RoutedEventArgs e)
        {
            // No-op in single-page layout.
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            // Prefer ViewModel SaveCommand. If caller wired button to this handler, close dialog positively.
            this.DialogResult = true;
            this.Close();
        }

        private void UpdateVisibilityByLoaiVe()
        {
            // No-op: visibility handled by XAML bindings to CurrentStep and ViewModel
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save uses ViewModel data; no direct UI access
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
