SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Add Level column if it does not exist in Roles table
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Roles' AND COLUMN_NAME = 'Level'
)
BEGIN
    ALTER TABLE dbo.Roles ADD Level INT NOT NULL DEFAULT 0;
END

-- 2. Update default hierarchy levels for existing roles using dynamic SQL to prevent compile-time column validation errors
EXEC sp_executesql N'
UPDATE dbo.Roles SET Level = 100 WHERE Name = ''SuperAdmin'';
UPDATE dbo.Roles SET Level = 80 WHERE Name = ''Admin'';
UPDATE dbo.Roles SET Level = 60 WHERE Name = ''Manager'';
UPDATE dbo.Roles SET Level = 40 WHERE Name = ''Operator'';
UPDATE dbo.Roles SET Level = 30 WHERE Name = ''Technician'';
UPDATE dbo.Roles SET Level = 20 WHERE Name = ''Cashier'';
UPDATE dbo.Roles SET Level = 10 WHERE Name = ''Guard'';
UPDATE dbo.Roles SET Level = 5 WHERE Name = ''Auditor'';
UPDATE dbo.Roles SET Level = 1 WHERE Name = ''Viewer'';
';

COMMIT TRANSACTION;

