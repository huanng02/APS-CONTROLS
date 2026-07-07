-- =============================================
-- Alter Cameras Table: Add Unique Index on IpAddress
-- Created: 2026-06-05
-- Purpose: Prevent duplicate camera IP address registration (allow multiple NULL/empty values)
-- =============================================

-- First, drop the unique index if it already exists
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UIX_Cameras_IpAddress' AND object_id = OBJECT_ID('dbo.Cameras'))
BEGIN
    DROP INDEX UIX_Cameras_IpAddress ON dbo.Cameras;
END

-- Delete duplicate IP addresses if any (keeping the oldest camera / lowest Id)
;WITH CTE AS (
    SELECT IpAddress, Id,
           ROW_NUMBER() OVER (PARTITION BY IpAddress ORDER BY Id) as RowNum
    FROM dbo.Cameras
    WHERE IpAddress IS NOT NULL AND IpAddress <> ''
)
DELETE FROM CTE WHERE RowNum > 1;

-- Create a unique filtered index to enforce uniqueness for non-empty IP addresses
CREATE UNIQUE NONCLUSTERED INDEX UIX_Cameras_IpAddress
ON dbo.Cameras (IpAddress)
WHERE IpAddress IS NOT NULL AND IpAddress <> '';
