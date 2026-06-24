-- Recreate UIX_Cameras_IpAddress index to only enforce uniqueness on plate cameras (Direction <> 'Overview')
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UIX_Cameras_IpAddress' AND object_id = OBJECT_ID('dbo.Cameras'))
BEGIN
    DROP INDEX UIX_Cameras_IpAddress ON dbo.Cameras;
END

CREATE UNIQUE NONCLUSTERED INDEX UIX_Cameras_IpAddress
ON dbo.Cameras (IpAddress)
WHERE IpAddress IS NOT NULL AND IpAddress <> '' AND Direction <> 'Overview';
