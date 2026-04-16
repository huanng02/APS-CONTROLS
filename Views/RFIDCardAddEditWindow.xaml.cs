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
            // If model is null, caller (Update flow) should set DataContext to a preloaded ViewModel.
        }

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
            // lists are bound; ViewModel handles step behavior via its properties; here we trigger step recalculation
            if (vm.LoaiVeId.HasValue)
            {
                var item = _loaiVeService.GetAll().FirstOrDefault(x => x.Id == vm.LoaiVeId.Value);
                bool isMonthly = item != null && ((item.TenLoai ?? string.Empty).ToLower().Contains("tháng") || item.GiaTien > 0);
                vm.CurrentStep = isMonthly ? 4 : 5;
            }
        }

        // NgayDangKy -> NgayHetHan handled by ViewModel (binding)

        private void ShowInlineError(string message)
        {
            // show message box for now
            MessageBox.Show(message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        private void OnNext(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
            if (vm == null) { ShowInlineError("Internal error: ViewModel missing"); return; }
            if (!vm.ValidateCurrentStep(out string err)) { ShowInlineError(err); return; }
            if (vm.CurrentStep < vm.MaxStep) vm.CurrentStep++;
        }

        private void OnBack(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
            if (vm == null) { ShowInlineError("Internal error: ViewModel missing"); return; }
            if (vm.CurrentStep > 1) vm.CurrentStep--;
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
            if (vm == null) { ShowInlineError("Internal error: ViewModel missing"); return; }
            if (!vm.ValidateCurrentStep(out string err)) { ShowInlineError(err); return; }
            // All validation passed; close dialog with success. Caller will persist.
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
            var vm = this.DataContext as ViewModels.RFIDCardWizardViewModel;
            if (vm == null) { ShowInlineError("Internal error: ViewModel missing"); return; }
            if (!vm.ValidateCurrentStep(out string err)) { ShowInlineError(err); return; }

            // Map ViewModel back to model for caller
            // Caller expects DialogResult == true and will read ViewModel to persist
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
