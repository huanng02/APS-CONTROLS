-- =============================================
-- Failover Infrastructure Tables
-- Created: 2026-06-30
-- =============================================

-- 1. Create Workstations Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Workstations')
BEGIN
    CREATE TABLE Workstations (
        WorkstationId VARCHAR(50) NOT NULL PRIMARY KEY,
        Hostname NVARCHAR(100) NOT NULL,
        IpAddress NVARCHAR(50) NOT NULL,
        LastHeartbeatUtc DATETIME2 NOT NULL,
        Status NVARCHAR(20) NOT NULL
    );
    PRINT 'Created table: Workstations';
END

-- 2. Create LaneOwnership Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LaneOwnership')
BEGIN
    CREATE TABLE LaneOwnership (
        LaneId INT NOT NULL PRIMARY KEY,
        PrimaryWorkstationId VARCHAR(50) NOT NULL,
        ActiveWorkstationId VARCHAR(50) NULL,
        OwnershipStatus NVARCHAR(20) NOT NULL, -- 'NORMAL', 'TAKEN_OVER', 'WAITING_RECLAIM'
        LastTakeoverUtc DATETIME2 NULL,
        LastChangedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_LaneOwnership_Lanes FOREIGN KEY (LaneId) REFERENCES Lanes(Id)
    );
    PRINT 'Created table: LaneOwnership';
END

-- 3. Add columns to LichSuXe
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'EntryWorkstationId')
    ALTER TABLE dbo.LichSuXe ADD EntryWorkstationId VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'ExitWorkstationId')
    ALTER TABLE dbo.LichSuXe ADD ExitWorkstationId VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'EntryControllerId')
    ALTER TABLE dbo.LichSuXe ADD EntryControllerId INT NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'LichSuXe' AND COLUMN_NAME = 'ExitControllerId')
    ALTER TABLE dbo.LichSuXe ADD ExitControllerId INT NULL;


-- 4. Add columns to XeTrongBai
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'EntryWorkstationId')
    ALTER TABLE dbo.XeTrongBai ADD EntryWorkstationId VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'ExitWorkstationId')
    ALTER TABLE dbo.XeTrongBai ADD ExitWorkstationId VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'EntryControllerId')
    ALTER TABLE dbo.XeTrongBai ADD EntryControllerId INT NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'ExitControllerId')
    ALTER TABLE dbo.XeTrongBai ADD ExitControllerId INT NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'XeTrongBai' AND COLUMN_NAME = 'ExitLaneId')
    ALTER TABLE dbo.XeTrongBai ADD ExitLaneId INT NULL;


-- 5. Add columns to VehicleSessions
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'EntryWorkstationId')
    ALTER TABLE dbo.VehicleSessions ADD EntryWorkstationId VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ExitWorkstationId')
    ALTER TABLE dbo.VehicleSessions ADD ExitWorkstationId VARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'EntryControllerId')
    ALTER TABLE dbo.VehicleSessions ADD EntryControllerId INT NULL;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ExitControllerId')
    ALTER TABLE dbo.VehicleSessions ADD ExitControllerId INT NULL;
