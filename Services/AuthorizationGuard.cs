using System;
using System.Security;
using QuanLyGiuXe.Models;

namespace QuanLyGiuXe.Services
{
    public static class AuthorizationGuard
    {
        public static void Protect(string permissionCode, string actionName = "Action")
        {
            if (!PermissionService.Instance.CheckPermission(permissionCode))
            {
                string msg = $"Unauthorized access to '{actionName}'. User '{CurrentUserContext.Instance.Username}' lacks permission '{permissionCode}'.";
                
                try
                {
                    LoggingService.Instance.LogSecurity("UNAUTHORIZED_ACCESS", "Guard", $"{{\"Action\":\"{actionName}\",\"Permission\":\"{permissionCode}\",\"User\":\"{CurrentUserContext.Instance.Username}\"}}");
                }
                catch { }
                
                throw new SecurityException(msg);
            }
        }

        public static void ProtectLane(int laneId, string actionName = "Lane Action")
        {
            int userId = CurrentUserContext.Instance.Id;
            if (!PermissionService.Instance.HasLaneAccess(userId, laneId))
            {
                string msg = $"Unauthorized access to Lane ID {laneId} for '{actionName}'. User '{CurrentUserContext.Instance.Username}' is not assigned to this lane.";
                
                try
                {
                    LoggingService.Instance.LogSecurity("UNAUTHORIZED_LANE_ACCESS", "Guard", $"{{\"Action\":\"{actionName}\",\"LaneId\":{laneId},\"User\":\"{CurrentUserContext.Instance.Username}\"}}");
                }
                catch { }
                
                throw new SecurityException(msg);
            }
        }

        public static void ProtectSite(int siteId, string actionName = "Site Action")
        {
            int userId = CurrentUserContext.Instance.Id;
            if (!PermissionService.Instance.HasSiteAccess(userId, siteId))
            {
                string msg = $"Unauthorized access to Site ID {siteId} for '{actionName}'. User '{CurrentUserContext.Instance.Username}' is not assigned to this site.";
                
                try
                {
                    LoggingService.Instance.LogSecurity("UNAUTHORIZED_SITE_ACCESS", "Guard", $"{{\"Action\":\"{actionName}\",\"SiteId\":{siteId},\"User\":\"{CurrentUserContext.Instance.Username}\"}}");
                }
                catch { }
                
                throw new SecurityException(msg);
            }
        }
    }
}
