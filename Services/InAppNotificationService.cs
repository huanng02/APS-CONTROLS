using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public sealed class InAppNotificationService
    {
        // ──────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────
        private static readonly Lazy<InAppNotificationService> _lazy =
            new Lazy<InAppNotificationService>(() => new InAppNotificationService());

        public static InAppNotificationService Instance => _lazy.Value;

        // ──────────────────────────────────────────────
        // State & Thread-safe queues
        // ──────────────────────────────────────────────
        public ObservableCollection<InAppNotificationItem> ActiveNotifications { get; } = new();
        
        // Cooldown for anti-spam (Co-relation: key/message -> last shown time)
        private readonly ConcurrentDictionary<string, DateTime> _lastShownTimestamps = new(StringComparer.OrdinalIgnoreCase);
        private const int AntiSpamCooldownMs = 3000; // 3 seconds cooldown for identical messages
        private const int MaxVisibleNotifications = 5;

        private InAppNotificationService() { }

        // ──────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────

        public void ShowNotification(string message, NotificationType type, int durationMs = 4000)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // Anti-Spam Check: Ignore high-frequency duplicate notifications
            var now = DateTime.Now;
            if (_lastShownTimestamps.TryGetValue(message, out var lastTime))
            {
                if ((now - lastTime).TotalMilliseconds < AntiSpamCooldownMs)
                {
                    // Check if it is currently displayed to avoid double posting
                    return;
                }
            }
            _lastShownTimestamps[message] = now;

            // Dispatch to UI Thread safely
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
            {
                CreateAndShow(message, type, durationMs);
            }
            else
            {
                dispatcher.Invoke(new Action(() => CreateAndShow(message, type, durationMs)));
            }
        }

        public void DismissNotification(Guid id)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
            {
                RemoveNotificationById(id);
            }
            else
            {
                dispatcher.Invoke(new Action(() => RemoveNotificationById(id)));
            }
        }

        public void ClearActiveNotifications()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
            {
                ActiveNotifications.Clear();
                _lastShownTimestamps.Clear();
            }
            else
            {
                dispatcher.Invoke(new Action(() =>
                {
                    ActiveNotifications.Clear();
                    _lastShownTimestamps.Clear();
                }));
            }
        }

        // ──────────────────────────────────────────────
        // Private Helpers
        // ──────────────────────────────────────────────

        private void CreateAndShow(string message, NotificationType type, int durationMs)
        {
            // Limit visible notification cards on screen
            if (ActiveNotifications.Count >= MaxVisibleNotifications)
            {
                // Dismiss the oldest one
                ActiveNotifications.RemoveAt(0);
            }

            var item = new InAppNotificationItem
            {
                Message = message,
                Type = type,
                DurationMs = durationMs
            };

            // Set up command to dismiss manually
            item.DismissCommand = new RelayCommand(() => DismissNotification(item.Id));

            ActiveNotifications.Add(item);

            // Setup auto-dismiss task
            if (durationMs > 0)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(durationMs);
                    DismissNotification(item.Id);
                });
            }
        }

        private void RemoveNotificationById(Guid id)
        {
            for (int i = 0; i < ActiveNotifications.Count; i++)
            {
                if (ActiveNotifications[i].Id == id)
                {
                    ActiveNotifications.RemoveAt(i);
                    break;
                }
            }
        }
    }
}
