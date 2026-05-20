-- ============================================================================
-- APS-CONTROLS Phase 8.1: Multi-Zone Topology Foundation
-- Migration: 20260519_multi_zone_topology.sql
-- Safe: Uses IF NOT EXISTS guards, idempotent, transaction-protected
-- ============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) ParkingSites
IF OBJECT_ID(N'dbo.ParkingSites', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ParkingSites (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SiteCode NVARCHAR(50) NOT NULL UNIQUE,
        SiteName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
    );
END

-- 2) ParkingZones
IF OBJECT_ID(N'dbo.ParkingZones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ParkingZones (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SiteId INT NOT NULL,
        ZoneCode NVARCHAR(50) NOT NULL UNIQUE,
        ZoneName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        MaxCapacity INT NOT NULL DEFAULT(100),
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
        CONSTRAINT FK_ParkingZones_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id)
    );
END

-- 3) C3Controllers
IF OBJECT_ID(N'dbo.C3Controllers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.C3Controllers (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ControllerName NVARCHAR(100) NOT NULL,
        IpAddress NVARCHAR(50) NOT NULL UNIQUE,
        ZoneId INT NOT NULL,
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
        CONSTRAINT FK_C3Controllers_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id)
    );
END

-- 4) Lanes
IF OBJECT_ID(N'dbo.Lanes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Lanes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        LaneCode NVARCHAR(50) NOT NULL UNIQUE,
        LaneName NVARCHAR(100) NOT NULL,
        Direction NVARCHAR(20) NOT NULL,
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
    );
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Lanes' AND COLUMN_NAME = 'ZoneId')
BEGIN
    ALTER TABLE dbo.Lanes ADD ZoneId INT NULL;
    ALTER TABLE dbo.Lanes ADD CONSTRAINT FK_Lanes_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id);
END

-- 5) VehicleSessions
IF OBJECT_ID(N'dbo.VehicleSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleSessions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CardId INT NULL,
        BienSo NVARCHAR(50) NULL,
        ThoiGianVao DATETIME NOT NULL,
        ThoiGianRa DATETIME NULL,
        Tien DECIMAL(18,2) NULL,
        TrangThai NVARCHAR(50) NULL,
        AnhVao NVARCHAR(500) NULL,
        AnhRa NVARCHAR(500) NULL,
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE())
    );
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'SiteId')
BEGIN
    ALTER TABLE dbo.VehicleSessions ADD SiteId INT NULL;
    ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id);
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ZoneId')
BEGIN
    ALTER TABLE dbo.VehicleSessions ADD ZoneId INT NULL;
    ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_ParkingZones FOREIGN KEY (ZoneId) REFERENCES dbo.ParkingZones(Id);
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'EntryLaneId')
BEGIN
    ALTER TABLE dbo.VehicleSessions ADD EntryLaneId INT NULL;
    ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_EntryLane FOREIGN KEY (EntryLaneId) REFERENCES dbo.Lanes(Id);
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ExitLaneId')
BEGIN
    ALTER TABLE dbo.VehicleSessions ADD ExitLaneId INT NULL;
    ALTER TABLE dbo.VehicleSessions ADD CONSTRAINT FK_VehicleSessions_ExitLane FOREIGN KEY (ExitLaneId) REFERENCES dbo.Lanes(Id);
END

-- 6) Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleSessions_SiteId' AND object_id = OBJECT_ID('dbo.VehicleSessions'))
BEGIN
    CREATE INDEX IX_VehicleSessions_SiteId ON dbo.VehicleSessions(SiteId);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleSessions_ZoneId' AND object_id = OBJECT_ID('dbo.VehicleSessions'))
BEGIN
    CREATE INDEX IX_VehicleSessions_ZoneId ON dbo.VehicleSessions(ZoneId);
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Lanes_ZoneId' AND object_id = OBJECT_ID('dbo.Lanes'))
BEGIN
    CREATE INDEX IX_Lanes_ZoneId ON dbo.Lanes(ZoneId);
END

-- 7) Seeding (Only runs if the database is completely empty)
IF NOT EXISTS (SELECT 1 FROM dbo.ParkingSites)
BEGIN
    INSERT INTO dbo.ParkingSites (SiteCode, SiteName, Description, IsActive, CreatedUtc)
    VALUES ('DEFAULT-SITE', N'Default Parking Site', N'Auto-seeded default site', 1, GETUTCDATE());

    DECLARE @DefaultSiteId INT = (SELECT Id FROM dbo.ParkingSites WHERE SiteCode = 'DEFAULT-SITE');

    INSERT INTO dbo.ParkingZones (SiteId, ZoneCode, ZoneName, Description, MaxCapacity, IsActive, CreatedUtc)
    VALUES (@DefaultSiteId, 'DEFAULT-ZONE', N'Default Parking Zone', N'Auto-seeded default zone', 500, 1, GETUTCDATE());

    DECLARE @DefaultZoneId INT = (SELECT Id FROM dbo.ParkingZones WHERE ZoneCode = 'DEFAULT-ZONE');

    INSERT INTO dbo.Lanes (LaneCode, LaneName, Direction, ZoneId, IsActive, CreatedUtc)
    VALUES 
    ('LANE-1', N'Cổng Vào 1', 'IN', @DefaultZoneId, 1, GETUTCDATE()),
    ('LANE-2', N'Cổng Ra 1', 'OUT', @DefaultZoneId, 1, GETUTCDATE());
END

COMMIT TRANSACTION;
