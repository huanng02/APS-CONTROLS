using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace QuanLyGiuXe.Services.Connection
{
    public class ConnectionStateService : INotifyPropertyChanged
    {
        private static readonly Lazy<ConnectionStateService> _instance = 
            new Lazy<ConnectionStateService>(() => new ConnectionStateService());
        
        public static ConnectionStateService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, ConnectionState> _states = new();

        public event PropertyChangedEventHandler PropertyChanged;

        private ConnectionStateService() { }

        public void UpdateState(string resourceId, ConnectionState newState)
        {
            if (!_states.TryGetValue(resourceId, out var oldState) || oldState != newState)
            {
                _states[resourceId] = newState;
                OnPropertyChanged(resourceId);
                
                // Cũng thông báo cho một property chung nếu cần
                OnPropertyChanged(nameof(AllStates));
            }
        }

        public ConnectionState GetState(string resourceId)
        {
            return _states.TryGetValue(resourceId, out var state) ? state : ConnectionState.Disconnected;
        }

        public IReadOnlyDictionary<string, ConnectionState> AllStates => _states;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Helper cho UI binding dễ dàng hơn
        public bool IsDatabaseOnline => GetState("Database") == ConnectionState.Connected;
        public bool IsC3Online => GetState("C3200") == ConnectionState.Connected;
    }
}
