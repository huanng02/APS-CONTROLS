SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Insert Permission if not exists
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'VIEW_SHIFT_REPORT')
BEGIN
    INSERT INTO dbo.Permissions (Code, Name, Description)
    VALUES ('VIEW_SHIFT_REPORT', N'Báo cáo ca làm việc', N'Cho phép xem và báo cáo ca làm việc của bảo vệ');
END

-- 2. Link Role permissions for SuperAdmin, Admin, Guard
DECLARE @ShiftPermId INT = (SELECT Id FROM dbo.Permissions WHERE Code = 'VIEW_SHIFT_REPORT');

IF @ShiftPermId IS NOT NULL
BEGIN
    -- SuperAdmin
    DECLARE @SuperAdminId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'SuperAdmin');
    IF @SuperAdminId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @SuperAdminId AND PermissionId = @ShiftPermId)
    BEGIN
        INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@SuperAdminId, @ShiftPermId);
    END

    -- Admin
    DECLARE @AdminId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Admin');
    IF @AdminId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @AdminId AND PermissionId = @ShiftPermId)
    BEGIN
        INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@AdminId, @ShiftPermId);
    END

    -- Guard
    DECLARE @GuardId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Guard');
    IF @GuardId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @GuardId AND PermissionId = @ShiftPermId)
    BEGIN
        INSERT INTO dbo.RolePermissions (RoleId, PermissionId) VALUES (@GuardId, @ShiftPermId);
    END
END

COMMIT TRANSACTION;
