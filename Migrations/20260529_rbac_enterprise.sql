SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 0. Safely migrate Roles.TrangThai and NhanVien.TrangThai from INT/BIT to NVARCHAR(20) if needed
IF OBJECT_ID(N'dbo.Roles') IS NOT NULL
BEGIN
    -- Unconditionally drop check constraints on TrangThai column (CK_Roles_TrangThai)
    DECLARE @CheckConstraintRoles NVARCHAR(200);
    SELECT @CheckConstraintRoles = name 
    FROM sys.check_constraints 
    WHERE parent_object_id = OBJECT_ID(N'dbo.Roles') 
      AND definition LIKE '%TrangThai%';

    IF @CheckConstraintRoles IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.Roles DROP CONSTRAINT [' + @CheckConstraintRoles + ']');
    END

    -- Update old string '1'/'0' values to 'Active'/'Inactive' if they exist
    EXEC('UPDATE dbo.Roles SET TrangThai = ''Active'' WHERE TrangThai = ''1'' OR TrangThai IS NULL');
    EXEC('UPDATE dbo.Roles SET TrangThai = ''Inactive'' WHERE TrangThai = ''0''');

    IF EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Roles')
          AND LOWER(c.name) = 'trangthai'
          AND t.name NOT IN ('nvarchar', 'varchar', 'nchar', 'char')
    )
    BEGIN
        DECLARE @ConstraintNameRoles NVARCHAR(200);
        SELECT @ConstraintNameRoles = d.name 
        FROM sys.default_constraints d
        INNER JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
        WHERE d.parent_object_id = OBJECT_ID(N'dbo.Roles') 
          AND LOWER(c.name) = 'trangthai';
          
        IF @ConstraintNameRoles IS NOT NULL
        BEGIN
            EXEC('ALTER TABLE dbo.Roles DROP CONSTRAINT [' + @ConstraintNameRoles + ']');
        END

        ALTER TABLE dbo.Roles ALTER COLUMN TrangThai NVARCHAR(20) NULL;
        EXEC('UPDATE dbo.Roles SET TrangThai = ''Active'' WHERE TrangThai = ''1'' OR TrangThai IS NULL');
        EXEC('UPDATE dbo.Roles SET TrangThai = ''Inactive'' WHERE TrangThai = ''0''');
        ALTER TABLE dbo.Roles ALTER COLUMN TrangThai NVARCHAR(20) NOT NULL;
        ALTER TABLE dbo.Roles ADD CONSTRAINT DF_Roles_TrangThai DEFAULT 'Active' FOR TrangThai;
    END
END

IF OBJECT_ID(N'dbo.NhanVien') IS NOT NULL
BEGIN
    -- Unconditionally drop check constraints on NhanVien.TrangThai if they exist
    DECLARE @CheckConstraintNhanVien NVARCHAR(200);
    SELECT @CheckConstraintNhanVien = name 
    FROM sys.check_constraints 
    WHERE parent_object_id = OBJECT_ID(N'dbo.NhanVien') 
      AND definition LIKE '%TrangThai%';

    IF @CheckConstraintNhanVien IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.NhanVien DROP CONSTRAINT [' + @CheckConstraintNhanVien + ']');
    END

    -- Update old string '1'/'0' values to 'Active'/'Inactive' if they exist
    EXEC('UPDATE dbo.NhanVien SET TrangThai = ''Active'' WHERE TrangThai = ''1'' OR TrangThai IS NULL');
    EXEC('UPDATE dbo.NhanVien SET TrangThai = ''Inactive'' WHERE TrangThai = ''0''');

    IF EXISTS (
        SELECT 1 FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.NhanVien')
          AND LOWER(c.name) = 'trangthai'
          AND t.name NOT IN ('nvarchar', 'varchar', 'nchar', 'char')
    )
    BEGIN
        DECLARE @ConstraintNameNhanVien NVARCHAR(200);
        SELECT @ConstraintNameNhanVien = d.name 
        FROM sys.default_constraints d
        INNER JOIN sys.columns c ON d.parent_column_id = c.column_id AND d.parent_object_id = c.object_id
        WHERE d.parent_object_id = OBJECT_ID(N'dbo.NhanVien') 
          AND LOWER(c.name) = 'trangthai';
          
        IF @ConstraintNameNhanVien IS NOT NULL
        BEGIN
            EXEC('ALTER TABLE dbo.NhanVien DROP CONSTRAINT [' + @ConstraintNameNhanVien + ']');
        END

        ALTER TABLE dbo.NhanVien ALTER COLUMN TrangThai NVARCHAR(20) NULL;
        EXEC('UPDATE dbo.NhanVien SET TrangThai = ''Active'' WHERE TrangThai = ''1'' OR TrangThai IS NULL');
        EXEC('UPDATE dbo.NhanVien SET TrangThai = ''Inactive'' WHERE TrangThai = ''0''');
        ALTER TABLE dbo.NhanVien ALTER COLUMN TrangThai NVARCHAR(20) NOT NULL;
        ALTER TABLE dbo.NhanVien ADD CONSTRAINT DF_NhanVien_TrangThai DEFAULT 'Active' FOR TrangThai;
    END
END

-- 1. Create Permissions table
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(100) NOT NULL UNIQUE,
        Name NVARCHAR(200) NOT NULL,
        Description NVARCHAR(500) NULL,
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
    );
END

-- 2. Create RolePermissions table
IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions (
        RoleId INT NOT NULL,
        PermissionId INT NOT NULL,
        PRIMARY KEY (RoleId, PermissionId),
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(Id) ON DELETE CASCADE
    );
END

-- 3. Create UserRoles table
IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles (
        UserId INT NOT NULL,
        RoleId INT NOT NULL,
        PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_UserRoles_NhanVien FOREIGN KEY (UserId) REFERENCES dbo.NhanVien(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE
    );
END

-- 4. Create UserLanePermissions table
IF OBJECT_ID(N'dbo.UserLanePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserLanePermissions (
        UserId INT NOT NULL,
        LaneId INT NOT NULL,
        PRIMARY KEY (UserId, LaneId),
        CONSTRAINT FK_UserLanePermissions_NhanVien FOREIGN KEY (UserId) REFERENCES dbo.NhanVien(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserLanePermissions_Lanes FOREIGN KEY (LaneId) REFERENCES dbo.Lanes(Id) ON DELETE CASCADE
    );
END

-- 5. Create UserSitePermissions table
IF OBJECT_ID(N'dbo.UserSitePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSitePermissions (
        UserId INT NOT NULL,
        SiteId INT NOT NULL,
        PRIMARY KEY (UserId, SiteId),
        CONSTRAINT FK_UserSitePermissions_NhanVien FOREIGN KEY (UserId) REFERENCES dbo.NhanVien(Id) ON DELETE CASCADE,
        CONSTRAINT FK_UserSitePermissions_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id) ON DELETE CASCADE
    );
END

-- Seed standard roles if they do not exist
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'SuperAdmin')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('SuperAdmin', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Admin')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Admin', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Manager')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Manager', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Operator')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Operator', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Guard')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Guard', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Cashier')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Cashier', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Technician')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Technician', 'Active');

-- Seed standard Permissions
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'USER_CREATE')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('USER_CREATE', N'Thêm nhân viên', N'Cho phép thêm tài khoản nhân viên mới');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'USER_UPDATE')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('USER_UPDATE', N'Sửa nhân viên', N'Cho phép cập nhật thông tin tài khoản nhân viên');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'USER_DELETE')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('USER_DELETE', N'Xóa nhân viên', N'Cho phép xóa tài khoản nhân viên');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'USER_VIEW')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('USER_VIEW', N'Xem nhân viên', N'Cho phép xem danh sách nhân viên');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'OPEN_BARRIER')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('OPEN_BARRIER', N'Mở Barie thủ công', N'Cho phép điều khiển mở barie từ xa');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'CONFIG_SYSTEM')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('CONFIG_SYSTEM', N'Cấu hình hệ thống', N'Cho phép thiết lập camera và kết nối cơ sở dữ liệu');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'CONFIG_CONTROLLER')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('CONFIG_CONTROLLER', N'Cấu hình bộ điều khiển ZK', N'Cho phép thay đổi thông số bộ điều khiển C3-200');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'VIEW_REPORT')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('VIEW_REPORT', N'Xem báo cáo lịch sử', N'Cho phép xem báo cáo, doanh thu và tìm kiếm lịch sử xe');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'VIEW_LOG')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('VIEW_LOG', N'Xem nhật ký hệ thống', N'Cho phép xem nhật ký nút nhấn, nhật ký vận hành và hệ thống');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'MANAGE_PRICING')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('MANAGE_PRICING', N'Quản lý bảng giá / thẻ', N'Cho phép quản lý loại xe, loại vé, RFID card và bảng giá khung giờ');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'DATABASE_EXPLORER')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('DATABASE_EXPLORER', N'Sử dụng SQL Query Tool', N'Cho phép sử dụng Mini Database Explorer chạy lệnh SQL trực tiếp');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'BACKUP_RESTORE')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('BACKUP_RESTORE', N'Sao lưu và Khôi phục', N'Cho phép thực hiện backup / restore database');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = 'SIMULATE_RECOVERY')
    INSERT INTO dbo.Permissions (Code, Name, Description) VALUES ('SIMULATE_RECOVERY', N'Vận hành QA Panel', N'Cho phép xem và mô phỏng các tình huống tự phục hồi mạng/công nghệ');

-- Link Role permissions
DECLARE @SuperAdminRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'SuperAdmin');
DECLARE @AdminRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Admin');
DECLARE @ManagerRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Manager');
DECLARE @OperatorRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Operator');
DECLARE @GuardRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Guard');
DECLARE @CashierRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Cashier');
DECLARE @TechnicianRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Technician');

-- Map SuperAdmin to all permissions
IF @SuperAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @SuperAdminRoleId, Id FROM dbo.Permissions p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @SuperAdminRoleId AND PermissionId = p.Id);
END

-- Map Admin to all permissions
IF @AdminRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @AdminRoleId, Id FROM dbo.Permissions p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @AdminRoleId AND PermissionId = p.Id);
END

-- Map Manager to specific permissions
IF @ManagerRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @ManagerRoleId, Id FROM dbo.Permissions
    WHERE Code IN ('USER_VIEW', 'VIEW_REPORT', 'VIEW_LOG', 'MANAGE_PRICING', 'OPEN_BARRIER')
      AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @ManagerRoleId AND PermissionId = Permissions.Id);
END

-- Map Operator to specific permissions
IF @OperatorRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @OperatorRoleId, Id FROM dbo.Permissions
    WHERE Code IN ('VIEW_LOG', 'OPEN_BARRIER')
      AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @OperatorRoleId AND PermissionId = Permissions.Id);
END

-- Map Guard to specific permissions
IF @GuardRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @GuardRoleId, Id FROM dbo.Permissions
    WHERE Code IN ('OPEN_BARRIER')
      AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @GuardRoleId AND PermissionId = Permissions.Id);
END

-- Map Cashier to specific permissions
IF @CashierRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @CashierRoleId, Id FROM dbo.Permissions
    WHERE Code IN ('VIEW_REPORT')
      AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @CashierRoleId AND PermissionId = Permissions.Id);
END

-- Map Technician to specific permissions
IF @TechnicianRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @TechnicianRoleId, Id FROM dbo.Permissions
    WHERE Code IN ('VIEW_LOG', 'CONFIG_SYSTEM', 'CONFIG_CONTROLLER', 'SIMULATE_RECOVERY')
      AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions WHERE RoleId = @TechnicianRoleId AND PermissionId = Permissions.Id);
END

-- Populate UserRoles with current role assignments in NhanVien table
INSERT INTO dbo.UserRoles (UserId, RoleId)
SELECT nv.Id, nv.RoleId
FROM dbo.NhanVien nv
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRoles ur WHERE ur.UserId = nv.Id AND ur.RoleId = nv.RoleId);

COMMIT TRANSACTION;
