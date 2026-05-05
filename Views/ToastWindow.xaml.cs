using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.Views
{
    /// <summary>
    /// Toast notification popup – hiển thị góc dưới phải màn hình.
    /// Tự ẩn sau <paramref name="item"/>.DurationMs.
    /// </summary>
    public partial class ToastWindow : Window
    {
        private readonly ToastItem _item;
        private readonly Action _onClosed;
        private readonly DispatcherTimer _timer;

        public ToastWindow(ToastItem item, Action onClosed)
        {
            InitializeComponent();

            _item     = item;
            _onClosed = onClosed;

            ConfigureAppearance();
            PositionBottomRight();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(item.DurationMs)
            };
            _timer.Tick += (_, _) => BeginClose();

            Loaded += OnLoaded;
        }

        // ── Appearance ────────────────────────────────────────────────────────────

        private void ConfigureAppearance()
        {
            ToastMessage.Text = _item.Message;

            switch (_item.Type)
            {
                case ToastType.Success:
                    ToastBackground.Color = (Color)ColorConverter.ConvertFromString("#27AE60");
                    ToastIcon.Text = "✅";
                    break;

                case ToastType.Error:
                    ToastBackground.Color = (Color)ColorConverter.ConvertFromString("#C0392B");
                    ToastIcon.Text = "❌";
                    break;

                case ToastType.Warning:
                    ToastBackground.Color = (Color)ColorConverter.ConvertFromString("#E67E22");
                    ToastIcon.Text = "⚠️";
                    break;
            }
        }

        // ── Position ──────────────────────────────────────────────────────────────

        private void PositionBottomRight()
        {
            var screen = SystemParameters.WorkArea;
            Left = screen.Right - Width - 18;
            Top  = screen.Bottom - Height - 18;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Slide-in animation
            var slideIn = (Storyboard)FindResource("SlideIn");
            slideIn.Begin(this);

            // Countdown bar: shrink from 1 → 0 over DurationMs
            var countdownAnim = new DoubleAnimation(1, 0,
                new Duration(TimeSpan.FromMilliseconds(_item.DurationMs)));
            // Không có EasingFunction → shrink đều (linear)
            CountdownScale.BeginAnimation(ScaleTransform.ScaleXProperty, countdownAnim);

            _timer.Start();
        }

        private void BeginClose()
        {
            _timer.Stop();
            var fadeOut = (Storyboard)FindResource("FadeOut");
            fadeOut.Begin(this);
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void FadeOut_Completed(object sender, EventArgs e)
        {
            _onClosed?.Invoke();
            Close();
        }

        private void Border_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // User click để đóng sớm
            BeginClose();
        }
    }
}
