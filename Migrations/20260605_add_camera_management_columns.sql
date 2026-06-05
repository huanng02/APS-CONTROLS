-- =============================================
-- Alter Cameras Table: Add Management Columns
-- Created: 2026-06-05
-- Purpose: Add Port, Protocol, Username, Password columns
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cameras') AND name = 'Port')
BEGIN
    ALTER TABLE dbo.Cameras ADD Port INT NULL;
    PRINT 'Added column: Port to Cameras';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cameras') AND name = 'Protocol')
BEGIN
    ALTER TABLE dbo.Cameras ADD Protocol NVARCHAR(20) NULL;
    PRINT 'Added column: Protocol to Cameras';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cameras') AND name = 'Username')
BEGIN
    ALTER TABLE dbo.Cameras ADD Username NVARCHAR(50) NULL;
    PRINT 'Added column: Username to Cameras';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cameras') AND name = 'Password')
BEGIN
    ALTER TABLE dbo.Cameras ADD Password NVARCHAR(100) NULL;
    PRINT 'Added column: Password to Cameras';
END
