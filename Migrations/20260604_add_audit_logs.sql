SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Create AuditLogs table if it does not exist
IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs (
        AuditLogId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NULL,
        Username NVARCHAR(100) NULL,
        ActionType NVARCHAR(50) NOT NULL,
        EntityType NVARCHAR(100) NOT NULL,
        EntityId NVARCHAR(100) NULL,
        OldValue NVARCHAR(MAX) NULL, -- Stored as JSON string
        NewValue NVARCHAR(MAX) NULL, -- Stored as JSON string
        Description NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE()
    );

    -- Create indexes to optimize queries
    CREATE INDEX IX_AuditLogs_UserId ON dbo.AuditLogs(UserId);
    CREATE INDEX IX_AuditLogs_ActionType ON dbo.AuditLogs(ActionType);
    CREATE INDEX IX_AuditLogs_EntityType ON dbo.AuditLogs(EntityType);
    CREATE INDEX IX_AuditLogs_CreatedAt ON dbo.AuditLogs(CreatedAt);
END

COMMIT TRANSACTION;
