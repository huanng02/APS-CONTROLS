SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1. Create Companies table
IF OBJECT_ID(N'dbo.Companies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Companies (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL UNIQUE,
        Name NVARCHAR(250) NOT NULL,
        Address NVARCHAR(500) NULL,
        Phone NVARCHAR(50) NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

-- 2. Create Departments table
IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Departments (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId INT NOT NULL,
        DepartmentName NVARCHAR(250) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Departments_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(Id)
    );
END

-- 3. Create Positions table
IF OBJECT_ID(N'dbo.Positions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Positions (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PositionName NVARCHAR(100) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

-- 4. Create Employees table
IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeCode NVARCHAR(50) NOT NULL UNIQUE,
        FullName NVARCHAR(250) NOT NULL,
        CompanyId INT NOT NULL,
        PositionId INT NOT NULL,
        Phone NVARCHAR(50) NULL,
        Email NVARCHAR(100) NULL,
        CCCD NVARCHAR(50) NULL,
        Avatar NVARCHAR(max) NULL,
        DepartmentId INT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT 'Active',
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_Employees_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(Id),
        CONSTRAINT FK_Employees_Positions FOREIGN KEY (PositionId) REFERENCES dbo.Positions(Id),
        CONSTRAINT FK_Employees_Departments FOREIGN KEY (DepartmentId) REFERENCES dbo.Departments(Id)
    );
END

-- 5. Seed default Positions if table is empty
IF NOT EXISTS (SELECT 1 FROM dbo.Positions)
BEGIN
    INSERT INTO dbo.Positions (PositionName, Status, IsDeleted)
    VALUES 
    (N'Director', 'Active', 0),
    (N'Manager', 'Active', 0),
    (N'Employee', 'Active', 0),
    (N'Security', 'Active', 0),
    (N'Visitor', 'Active', 0);
END

-- 6. Add EmployeeId and Foreign Key to RFIDCards
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'RFIDCards' AND COLUMN_NAME = 'EmployeeId')
BEGIN
    ALTER TABLE dbo.RFIDCards ADD EmployeeId INT NULL;
    ALTER TABLE dbo.RFIDCards ADD CONSTRAINT FK_RFIDCards_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(Id);
END

COMMIT TRANSACTION;
