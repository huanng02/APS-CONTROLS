-- Add DisplayIndex to Lanes table (nullable – NULL or 0 means unconfigured / default sorting)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Lanes' AND COLUMN_NAME = 'DisplayIndex')
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.Lanes ADD DisplayIndex INT NULL;';
END
