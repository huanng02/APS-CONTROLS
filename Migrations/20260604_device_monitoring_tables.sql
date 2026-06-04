-- =============================================
-- Device Monitoring Infrastructure Tables
-- Created: 2026-06-04
-- Purpose: Add Cameras, Barriers, and DeviceEvents tables
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Cameras')
BEGIN
    CREATE TABLE Cameras (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CameraName NVARCHAR(100) NOT NULL,
        CameraKey NVARCHAR(50) NOT NULL UNIQUE,
        IpAddress NVARCHAR(50),
        RtspUrl NVARCHAR(500),
        LaneId INT NULL,
        Direction NVARCHAR(10),
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Cameras_Lanes FOREIGN KEY (LaneId) REFERENCES Lanes(Id)
    );
    PRINT 'Created table: Cameras';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Barriers')
BEGIN
    CREATE TABLE Barriers (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        BarrierName NVARCHAR(100) NOT NULL,
        ControllerId INT NULL,
        RelayNumber INT NOT NULL DEFAULT 1,
        LaneId INT NULL,
        Direction NVARCHAR(10),
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Barriers_Controllers FOREIGN KEY (ControllerId) REFERENCES C3Controllers(Id),
        CONSTRAINT FK_Barriers_Lanes FOREIGN KEY (LaneId) REFERENCES Lanes(Id)
    );
    PRINT 'Created table: Barriers';
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DeviceEvents')
BEGIN
    CREATE TABLE DeviceEvents (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DeviceType NVARCHAR(50) NOT NULL,
        DeviceName NVARCHAR(100) NOT NULL,
        EventType NVARCHAR(50) NOT NULL,
        Severity NVARCHAR(20) NOT NULL DEFAULT 'Info',
        Description NVARCHAR(500)
    );
    CREATE NONCLUSTERED INDEX IX_DeviceEvents_Timestamp ON DeviceEvents (Timestamp DESC);
    PRINT 'Created table: DeviceEvents';
END
