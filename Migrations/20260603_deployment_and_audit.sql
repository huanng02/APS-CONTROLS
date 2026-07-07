-- Migration for Deployment Center and Audit Trail

IF OBJECT_ID(N'dbo.ConfigurationState', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConfigurationState (
        Id INT NOT NULL PRIMARY KEY DEFAULT 1 CHECK (Id = 1),
        ActiveVersion INT NOT NULL DEFAULT 1,
        HasPendingChanges BIT NOT NULL DEFAULT 0,
        LastDeployedAt DATETIME NULL,
        LastDeployedBy NVARCHAR(100) NULL
    );
END

-- Ensure seed row always exists (even if table was pre-created without the INSERT)
IF NOT EXISTS (SELECT 1 FROM dbo.ConfigurationState WHERE Id = 1)
BEGIN
    INSERT INTO dbo.ConfigurationState (Id, ActiveVersion, HasPendingChanges) VALUES (1, 1, 0);
END

IF OBJECT_ID(N'dbo.DeploymentHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DeploymentHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Version INT NOT NULL,
        DeployTime DATETIME NOT NULL DEFAULT GETDATE(),
        DeployBy NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- SUCCESS, FAILED
        Notes NVARCHAR(500) NULL
    );
END

IF OBJECT_ID(N'dbo.ConfigurationAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConfigurationAudit (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
        UserName NVARCHAR(100) NOT NULL,
        EntityType NVARCHAR(100) NOT NULL,
        EntityName NVARCHAR(100) NOT NULL,
        PropertyName NVARCHAR(100) NOT NULL,
        OldValue NVARCHAR(MAX) NULL,
        NewValue NVARCHAR(MAX) NULL
    );
END
