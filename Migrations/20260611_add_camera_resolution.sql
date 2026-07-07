-- =============================================
-- Alter Cameras Table: Add Resolution Columns
-- Created: 2026-06-11
-- Purpose: Add ResolutionWidth, ResolutionHeight columns
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cameras') AND name = 'ResolutionWidth')
BEGIN
    ALTER TABLE dbo.Cameras ADD ResolutionWidth INT NULL;
    PRINT 'Added column: ResolutionWidth to Cameras';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Cameras') AND name = 'ResolutionHeight')
BEGIN
    ALTER TABLE dbo.Cameras ADD ResolutionHeight INT NULL;
    PRINT 'Added column: ResolutionHeight to Cameras';
END
