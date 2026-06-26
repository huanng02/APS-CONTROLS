using System;
using System.Collections.Generic;

namespace QuanLyGiuXe.Models
{
    public interface ICurrentUserContext
    {
        int Id { get; }
        string Username { get; }
        string Role { get; }
        string Ten { get; }
        bool IsAuthenticated { get; }
        DateTime LoginTime { get; }
        HashSet<string> Permissions { get; }
        HashSet<int> AssignedLaneIds { get; }
        HashSet<int> AssignedSiteIds { get; }
        void SetCurrentUser(int id, string username, string role, string ten, IEnumerable<string> permissions, IEnumerable<int> laneIds, IEnumerable<int> siteIds);
        void UpdatePermissions(IEnumerable<string> permissions);
        void Clear();
    }

    public class CurrentUserContext : ICurrentUserContext
    {
        private static readonly CurrentUserContext _globalInstance = new();

        public static CurrentUserContext Instance => _globalInstance;

        private readonly object _lock = new();
        private int _id;
        private string _username = string.Empty;
        private string _role = string.Empty;
        private string _ten = string.Empty;
        private DateTime _loginTime;
        private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _assignedLaneIds = new();
        private readonly HashSet<int> _assignedSiteIds = new();

        public int Id { get { lock(_lock) return _id; } }
        public string Username { get { lock(_lock) return _username; } }
        public string Role { get { lock(_lock) return _role; } }
        public string Ten { get { lock(_lock) return _ten; } }
        public bool IsAuthenticated => !string.IsNullOrEmpty(Username);
        public DateTime LoginTime { get { lock(_lock) return _loginTime; } }

        public HashSet<string> Permissions { get { lock(_lock) return new HashSet<string>(_permissions, StringComparer.OrdinalIgnoreCase); } }
        public HashSet<int> AssignedLaneIds { get { lock(_lock) return new HashSet<int>(_assignedLaneIds); } }
        public HashSet<int> AssignedSiteIds { get { lock(_lock) return new HashSet<int>(_assignedSiteIds); } }

        public void SetCurrentUser(int id, string username, string role, string ten, IEnumerable<string> permissions, IEnumerable<int> laneIds, IEnumerable<int> siteIds)
        {
            lock (_lock)
            {
                _id = id;
                _username = username ?? string.Empty;
                _role = role ?? string.Empty;
                _ten = ten ?? string.Empty;
                _loginTime = DateTime.Now;

                _permissions.Clear();
                if (permissions != null)
                {
                    foreach (var p in permissions) _permissions.Add(p);
                }

                _assignedLaneIds.Clear();
                if (laneIds != null)
                {
                    foreach (var l in laneIds) _assignedLaneIds.Add(l);
                }

                _assignedSiteIds.Clear();
                if (siteIds != null)
                {
                    foreach (var s in siteIds) _assignedSiteIds.Add(s);
                }

                // Keep legacy static CurrentUser synced to avoid breaking any legacy code!
                CurrentUser.Id = id;
                CurrentUser.Username = username;
                CurrentUser.Role = role;
                CurrentUser.Ten = ten;
                CurrentUser.LoginTime = _loginTime;
            }
        }

        public void UpdatePermissions(IEnumerable<string> permissions)
        {
            lock (_lock)
            {
                _permissions.Clear();
                if (permissions != null)
                {
                    foreach (var p in permissions) _permissions.Add(p);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _id = 0;
                _username = string.Empty;
                _role = string.Empty;
                _ten = string.Empty;
                _loginTime = default;
                _permissions.Clear();
                _assignedLaneIds.Clear();
                _assignedSiteIds.Clear();

                CurrentUser.Clear();
            }
        }
    }
}
