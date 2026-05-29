SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Create AccessSchedules table
IF OBJECT_ID(N'dbo.AccessSchedules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccessSchedules (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ScheduleName NVARCHAR(100) NOT NULL UNIQUE,
        StartTime TIME NOT NULL,
        EndTime TIME NOT NULL,
        DaysOfWeek NVARCHAR(100) NOT NULL, -- e.g. 'Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday'
        IsEmergencyOverride BIT NOT NULL DEFAULT(0),
        TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
    );
END

-- 2. Create CardGroups table
IF OBJECT_ID(N'dbo.CardGroups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CardGroups (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        GroupName NVARCHAR(100) NOT NULL UNIQUE,
        Description NVARCHAR(500) NULL,
        TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
    );
END

-- 3. Create CardGroupLanePermissions table
IF OBJECT_ID(N'dbo.CardGroupLanePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CardGroupLanePermissions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        GroupId INT NOT NULL,
        LaneId INT NOT NULL,
        ScheduleId INT NULL,
        TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
        CONSTRAINT FK_CardGroupLanePermissions_CardGroups FOREIGN KEY (GroupId) REFERENCES dbo.CardGroups(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CardGroupLanePermissions_Lanes FOREIGN KEY (LaneId) REFERENCES dbo.Lanes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CardGroupLanePermissions_AccessSchedules FOREIGN KEY (ScheduleId) REFERENCES dbo.AccessSchedules(Id) ON DELETE SET NULL
    );
END

-- 4. Create RFIDAccessRules table
IF OBJECT_ID(N'dbo.RFIDAccessRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RFIDAccessRules (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CardUID NVARCHAR(50) NOT NULL,
        LaneId INT NOT NULL,
        ScheduleId INT NULL,
        RuleType NVARCHAR(20) NOT NULL DEFAULT 'Allow', -- 'Allow', 'Deny'
        TrangThai NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
        CONSTRAINT FK_RFIDAccessRules_Lanes FOREIGN KEY (LaneId) REFERENCES dbo.Lanes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_RFIDAccessRules_AccessSchedules FOREIGN KEY (ScheduleId) REFERENCES dbo.AccessSchedules(Id) ON DELETE SET NULL
    );
END

-- 5. Add GroupId to RFIDCards if it does not exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'RFIDCards' AND COLUMN_NAME = 'GroupId')
BEGIN
    ALTER TABLE dbo.RFIDCards ADD GroupId INT NULL;
    ALTER TABLE dbo.RFIDCards ADD CONSTRAINT FK_RFIDCards_CardGroups FOREIGN KEY (GroupId) REFERENCES dbo.CardGroups(Id) ON DELETE SET NULL;
END

-- Seed default schedules
IF NOT EXISTS (SELECT 1 FROM dbo.AccessSchedules WHERE ScheduleName = '24/7 Full Access')
BEGIN
    INSERT INTO dbo.AccessSchedules (ScheduleName, StartTime, EndTime, DaysOfWeek, IsEmergencyOverride, TrangThai)
    VALUES ('24/7 Full Access', '00:00:00', '23:59:59', 'Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday', 0, 'Active');
END

IF NOT EXISTS (SELECT 1 FROM dbo.AccessSchedules WHERE ScheduleName = 'Office Hours Only')
BEGIN
    INSERT INTO dbo.AccessSchedules (ScheduleName, StartTime, EndTime, DaysOfWeek, IsEmergencyOverride, TrangThai)
    VALUES ('Office Hours Only', '08:00:00', '17:00:00', 'Monday,Tuesday,Wednesday,Thursday,Friday', 0, 'Active');
END

IF NOT EXISTS (SELECT 1 FROM dbo.AccessSchedules WHERE ScheduleName = 'Emergency Override Mode')
BEGIN
    INSERT INTO dbo.AccessSchedules (ScheduleName, StartTime, EndTime, DaysOfWeek, IsEmergencyOverride, TrangThai)
    VALUES ('Emergency Override Mode', '00:00:00', '23:59:59', 'Monday,Tuesday,Wednesday,Thursday,Friday,Saturday,Sunday', 1, 'Active');
END

-- Seed default Card Groups
IF NOT EXISTS (SELECT 1 FROM dbo.CardGroups WHERE GroupName = 'Default Card Group')
BEGIN
    INSERT INTO dbo.CardGroups (GroupName, Description, TrangThai)
    VALUES ('Default Card Group', N'Nhóm thẻ mặc định có quyền truy cập toàn bộ cổng', 'Active');
END

IF NOT EXISTS (SELECT 1 FROM dbo.CardGroups WHERE GroupName = 'Technician Access')
BEGIN
    INSERT INTO dbo.CardGroups (GroupName, Description, TrangThai)
    VALUES ('Technician Access', N'Nhóm kỹ thuật truy cập bảo trì', 'Active');
END

-- Link default group permissions to existing lanes
DECLARE @DefaultGroupId INT = (SELECT Id FROM dbo.CardGroups WHERE GroupName = 'Default Card Group');
DECLARE @FullAccessSchedId INT = (SELECT Id FROM dbo.AccessSchedules WHERE ScheduleName = '24/7 Full Access');
IF @DefaultGroupId IS NOT NULL AND @FullAccessSchedId IS NOT NULL
BEGIN
    INSERT INTO dbo.CardGroupLanePermissions (GroupId, LaneId, ScheduleId, TrangThai)
    SELECT @DefaultGroupId, l.Id, @FullAccessSchedId, 'Active'
    FROM dbo.Lanes l
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CardGroupLanePermissions cglp WHERE cglp.GroupId = @DefaultGroupId AND cglp.LaneId = l.Id);
END

-- Seed standard roles if not exist
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Auditor')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Auditor', 'Active');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Name = 'Viewer')
    INSERT INTO dbo.Roles (Name, TrangThai) VALUES ('Viewer', 'Active');

-- Seed functional separation permissions
-- === OPERATIONAL ===
-- OPEN_BARRIER, FORCE_EXIT, MANUAL_OVERRIDE, HANDLE_LOST_TICKET, MANUAL_PLATE_EDIT
-- === RFID MANAGEMENT ===
-- RFID_CREATE, RFID_UPDATE, RFID_DELETE, RFID_RENEW
-- === SYSTEM / TECHNICAL ===
-- CONFIG_CONTROLLER, CONFIG_CAMERA, CONFIG_SYSTEM, VIEW_DEVICE_STATUS, RESTART_CONTROLLER, SIMULATE_RECOVERY
-- === FINANCIAL ===
-- VIEW_REVENUE, PAYMENT_PROCESS, REFUND_PAYMENT, EXPORT_FINANCIAL_REPORT
-- === SECURITY / ADMIN ===
-- USER_CREATE, USER_UPDATE, USER_DELETE, ROLE_ASSIGN, VIEW_AUDIT_LOG
-- === MONITORING ===
-- VIEW_CAMERA, VIEW_REALTIME_LOG, VIEW_OCCUPANCY, VIEW_DASHBOARD

DECLARE @Perms TABLE (Code NVARCHAR(100), Name NVARCHAR(200), Description NVARCHAR(500));
INSERT INTO @Perms (Code, Name, Description) VALUES
('OPEN_BARRIER', N'Mở Barie thủ công', N'Cho phép điều khiển mở barie từ xa (Yêu cầu nhập lý do)'),
('FORCE_EXIT', N'Cho xe ra cưỡng chế', N'Cho phép cưỡng chế cho xe qua lane ra'),
('MANUAL_OVERRIDE', N'Ghi đè thủ công', N'Ghi đè tín hiệu cảm biến hoặc gate control'),
('HANDLE_LOST_TICKET', N'Xử lý mất vé/thẻ', N'Cho phép giải quyết xe mất thẻ vãng lai'),
('MANUAL_PLATE_EDIT', N'Sửa biển số thủ công', N'Cho phép sửa thông tin biển số xe khi quẹt thẻ'),

('RFID_CREATE', N'Thêm RFID Card', N'Cho phép cấp phát thẻ RFID mới'),
('RFID_UPDATE', N'Sửa thông tin RFID Card', N'Cho phép sửa thông tin xe, biển số của thẻ'),
('RFID_DELETE', N'Xóa RFID Card', N'Cho phép vô hiệu hóa/xóa thẻ khỏi hệ thống'),
('RFID_RENEW', N'Gia hạn RFID Card', N'Cho phép gia hạn thẻ tháng và thanh toán phí'),

('CONFIG_CONTROLLER', N'Cấu hình bộ điều khiển ZK', N'Thay đổi thông số IP, relay, sensor của C3-200'),
('CONFIG_CAMERA', N'Cấu hình Camera IP', N'Thay đổi địa chỉ RSTP stream camera đầu vào/ra'),
('CONFIG_SYSTEM', N'Cấu hình hệ thống', N'Thiết lập cấu hình chung và kết nối DB'),
('VIEW_DEVICE_STATUS', N'Xem trạng thái phần cứng', N'Xem tình trạng online/offline của Controller, Camera'),
('RESTART_CONTROLLER', N'Khởi động lại Controller', N'Gửi lệnh reboot đến C3-200 controller'),
('SIMULATE_RECOVERY', N'Vận hành QA Panel', N'Mô phỏng phục hồi mạng và tự chẩn đoán'),

('VIEW_REVENUE', N'Xem báo cáo doanh thu', N'Xem tổng quan doanh thu bãi xe'),
('PAYMENT_PROCESS', N'Thanh toán & Soát vé', N'Thực hiện soát vé vãng lai, thu tiền checkout'),
('REFUND_PAYMENT', N'Hoàn trả tiền vé', N'Hoàn trả các giao dịch lỗi'),
('EXPORT_FINANCIAL_REPORT', N'Xuất báo cáo tài chính', N'Xuất Excel/PDF doanh thu báo cáo ca làm việc'),

('USER_CREATE', N'Thêm nhân viên', N'Tạo tài khoản nhân viên mới'),
('USER_UPDATE', N'Sửa nhân viên', N'Cập nhật thông tin tài khoản nhân viên'),
('USER_DELETE', N'Xóa nhân viên', N'Vô hiệu hóa hoặc xóa tài khoản nhân viên'),
('ROLE_ASSIGN', N'Phân quyền Role', N'Gán quyền và phân vai trò truy cập'),
('VIEW_AUDIT_LOG', N'Xem nhật ký kiểm toán', N'Xem lịch sử thao tác nguy hiểm, lý do mở barie thủ công'),

('VIEW_CAMERA', N'Xem camera trực tuyến', N'Xem live stream camera giám sát'),
('VIEW_REALTIME_LOG', N'Xem log sự kiện realtime', N'Theo dõi quẹt thẻ, sensor trigger trực quan'),
('VIEW_OCCUPANCY', N'Xem mật độ đỗ xe', N'Xem số chỗ trống hiện tại trong bãi xe'),
('VIEW_DASHBOARD', N'Xem Dashboard thống kê', N'Xem biểu đồ thống kê xe vào ra tổng quan');

-- Insert or update permissions
MERGE dbo.Permissions AS target
USING @Perms AS source
ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET Name = source.Name, Description = source.Description
WHEN NOT MATCHED THEN
    INSERT (Code, Name, Description) VALUES (source.Code, source.Name, source.Description);

-- Update RolePermissions for exact function separation
-- Clean up all existing mappings for the roles we are redefining to avoid stale data
DECLARE @SuperAdminRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'SuperAdmin');
DECLARE @AdminRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Admin');
DECLARE @ManagerRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Manager');
DECLARE @OperatorRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Operator');
DECLARE @GuardRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Guard');
DECLARE @CashierRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Cashier');
DECLARE @TechnicianRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Technician');
DECLARE @AuditorRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Auditor');
DECLARE @ViewerRoleId INT = (SELECT Id FROM dbo.Roles WHERE Name = 'Viewer');

DELETE FROM dbo.RolePermissions WHERE RoleId IN (
    @SuperAdminRoleId, @AdminRoleId, @ManagerRoleId, @OperatorRoleId,
    @GuardRoleId, @CashierRoleId, @TechnicianRoleId, @AuditorRoleId, @ViewerRoleId
);

-- SuperAdmin: Bypass/all
IF @SuperAdminRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @SuperAdminRoleId, Id FROM dbo.Permissions;
END

-- Admin: Full operational + admin
IF @AdminRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @AdminRoleId, Id FROM dbo.Permissions;
END

-- Manager: Monitoring + reports + full RFID + management + limited operational
IF @ManagerRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @ManagerRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'OPEN_BARRIER', 'HANDLE_LOST_TICKET', 'MANUAL_PLATE_EDIT',
        'RFID_CREATE', 'RFID_UPDATE', 'RFID_DELETE', 'RFID_RENEW',
        'MANAGE_PRICING', 'VIEW_REPORT', 'USER_VIEW',
        'VIEW_DEVICE_STATUS',
        'VIEW_REVENUE', 'EXPORT_FINANCIAL_REPORT',
        'VIEW_AUDIT_LOG',
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG', 'VIEW_OCCUPANCY', 'VIEW_DASHBOARD'
    );
END

-- Operator: Realtime lane operations
IF @OperatorRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @OperatorRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'OPEN_BARRIER', 'FORCE_EXIT', 'HANDLE_LOST_TICKET', 'MANUAL_PLATE_EDIT',
        'PAYMENT_PROCESS',
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG', 'VIEW_OCCUPANCY', 'VIEW_DASHBOARD'
    );
END

-- Guard: Basic barrier operation only
IF @GuardRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @GuardRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'OPEN_BARRIER',
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG'
    );
END

-- Cashier: Payment and billing only, NO RFID management, NO barrier control
IF @CashierRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @CashierRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'PAYMENT_PROCESS', 'EXPORT_FINANCIAL_REPORT',
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG', 'VIEW_OCCUPANCY', 'VIEW_DASHBOARD'
    );
END

-- Technician: Technical functions only, NO realtime gate operation
IF @TechnicianRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @TechnicianRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'RFID_CREATE', 'RFID_UPDATE', 'RFID_DELETE', 'RFID_RENEW', -- test reader/relay, diagnostic quẹt RFID
        'CONFIG_CONTROLLER', 'CONFIG_CAMERA', 'CONFIG_SYSTEM', 'VIEW_DEVICE_STATUS', 'RESTART_CONTROLLER', 'SIMULATE_RECOVERY',
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG'
    );
END

-- Auditor: Read-only logs/reports
IF @AuditorRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @AuditorRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'VIEW_REVENUE', 'EXPORT_FINANCIAL_REPORT',
        'VIEW_AUDIT_LOG',
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG', 'VIEW_OCCUPANCY', 'VIEW_DASHBOARD'
    );
END

-- Viewer: Camera + monitoring only
IF @ViewerRoleId IS NOT NULL
BEGIN
    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
    SELECT @ViewerRoleId, Id FROM dbo.Permissions
    WHERE Code IN (
        'VIEW_CAMERA', 'VIEW_REALTIME_LOG', 'VIEW_OCCUPANCY', 'VIEW_DASHBOARD'
    );
END

COMMIT TRANSACTION;
