using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuanLyGiuXe.ViewModels;

namespace QuanLyGiuXe.Views
{
    public partial class RealtimeLogWindow : Window
    {
        private RealtimeLogViewModel? _viewModel;

        public RealtimeLogWindow()
        {
            InitializeComponent();

            _viewModel = new RealtimeLogViewModel();
            this.DataContext = _viewModel;

            this.Loaded += RealtimeLogWindow_Loaded;
            this.Unloaded += RealtimeLogWindow_Unloaded;
        }

        private void RealtimeLogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                // Subscribe to scroll down automatically if AutoScroll is true
                _viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
            }
        }

        private void RealtimeLogWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.LogEntries.CollectionChanged -= LogEntries_CollectionChanged;
                _viewModel.Dispose(); // Clean up subscriptions and cancellation tokens
            }
        }

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_viewModel != null && _viewModel.AutoScroll && e.Action == NotifyCollectionChangedAction.Add)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (LogGrid.Items.Count > 0)
                        {
                            // Scroll to the newest item (index 0 since we Insert(0, item))
                            var firstItem = LogGrid.Items[0];
                            if (firstItem != null)
                            {
                                LogGrid.ScrollIntoView(firstItem);
                            }
                        }
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
    }
}
