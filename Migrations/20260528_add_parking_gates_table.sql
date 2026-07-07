SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) Create ParkingGates table if it does not exist
IF OBJECT_ID(N'dbo.ParkingGates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ParkingGates (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SiteId INT NOT NULL,
        GateCode NVARCHAR(50) NOT NULL UNIQUE,
        GateName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(250) NULL,
        IsActive BIT NOT NULL DEFAULT(1),
        CreatedUtc DATETIME NOT NULL DEFAULT(GETUTCDATE()),
        CONSTRAINT FK_ParkingGates_ParkingSites FOREIGN KEY (SiteId) REFERENCES dbo.ParkingSites(Id)
    );
END

-- 2) Add LoaiXeId to ParkingZones if it does not exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ParkingZones' AND COLUMN_NAME = 'LoaiXeId')
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.ParkingZones ADD LoaiXeId INT NULL;';
    EXEC sp_executesql N'ALTER TABLE dbo.ParkingZones ADD CONSTRAINT FK_ParkingZones_LoaiXe FOREIGN KEY (LoaiXeId) REFERENCES dbo.LoaiXe(Id);';
END

-- 3) Add GateId to C3Controllers if it does not exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'C3Controllers' AND COLUMN_NAME = 'GateId')
BEGIN
    -- Alter existing columns to make ZoneId nullable since controllers will now belong to Gates
    EXEC sp_executesql N'ALTER TABLE dbo.C3Controllers ALTER COLUMN ZoneId INT NULL;';
    
    EXEC sp_executesql N'ALTER TABLE dbo.C3Controllers ADD GateId INT NULL;';
    EXEC sp_executesql N'ALTER TABLE dbo.C3Controllers ADD CONSTRAINT FK_C3Controllers_ParkingGates FOREIGN KEY (GateId) REFERENCES dbo.ParkingGates(Id);';
END

-- 4) Add GateId to Lanes if it does not exist
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Lanes' AND COLUMN_NAME = 'GateId')
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD GateId INT NULL;';
    EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD CONSTRAINT FK_Lanes_ParkingGates FOREIGN KEY (GateId) REFERENCES dbo.ParkingGates(Id);';
END

-- 5) DATA MIGRATION: Migrate existing Zone-based topology to Gate-based topology
-- Create a default Gate for each existing Zone to prevent configuration loss
IF EXISTS (SELECT 1 FROM dbo.ParkingZones)
BEGIN
    -- Auto-insert Gates from existing Zones
    EXEC sp_executesql N'
    INSERT INTO dbo.ParkingGates (SiteId, GateCode, GateName, Description, IsActive)
    SELECT z.SiteId, z.ZoneCode + ''-GATE'', z.ZoneName + N'' Gate'', z.Description, z.IsActive
    FROM dbo.ParkingZones z
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ParkingGates g WHERE g.GateCode = z.ZoneCode + ''-GATE'');';

    -- Connect existing Controllers to the new Gates
    EXEC sp_executesql N'
    UPDATE c
    SET c.GateId = g.Id
    FROM dbo.C3Controllers c
    JOIN dbo.ParkingZones z ON c.ZoneId = z.Id
    JOIN dbo.ParkingGates g ON z.ZoneCode + ''-GATE'' = g.GateCode
    WHERE c.GateId IS NULL;';

    -- Connect existing Lanes to the new Gates
    EXEC sp_executesql N'
    UPDATE l
    SET l.GateId = g.Id
    FROM dbo.Lanes l
    JOIN dbo.ParkingZones z ON l.ZoneId = z.Id
    JOIN dbo.ParkingGates g ON z.ZoneCode + ''-GATE'' = g.GateCode
    WHERE l.GateId IS NULL;';
END

-- 6) Map Vehicle Types to default zones if they exist (Helper for Dynamic Resolution)
DECLARE @XeMayId INT = (SELECT TOP 1 Id FROM dbo.LoaiXe WHERE TenLoai = N'Xe máy');
DECLARE @OToId INT = (SELECT TOP 1 Id FROM dbo.LoaiXe WHERE TenLoai = N'Ô tô');

IF @XeMayId IS NOT NULL
BEGIN
    EXEC sp_executesql N'
    UPDATE dbo.ParkingZones
    SET LoaiXeId = @val
    WHERE (ZoneCode LIKE ''%XE_MAY%'' OR ZoneCode LIKE ''%MOTOR%'' OR ZoneName LIKE N''%Xe máy%'' OR ZoneName LIKE N''%Xe may%'')
      AND LoaiXeId IS NULL;', N'@val INT', @val = @XeMayId;
END

IF @OToId IS NOT NULL
BEGIN
    EXEC sp_executesql N'
    UPDATE dbo.ParkingZones
    SET LoaiXeId = @val
    WHERE (ZoneCode LIKE ''%OTO%'' OR ZoneCode LIKE ''%CAR%'' OR ZoneName LIKE N''%Ô tô%'' OR ZoneName LIKE N''%O to%'')
      AND LoaiXeId IS NULL;', N'@val INT', @val = @OToId;
END

COMMIT TRANSACTION;
