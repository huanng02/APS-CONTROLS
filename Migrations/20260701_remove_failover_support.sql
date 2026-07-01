-- =============================================
-- Remove Failover Infrastructure Tables
-- Created: 2026-07-01
-- =============================================

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'LaneOwnership')
BEGIN
    DROP TABLE LaneOwnership;
    PRINT 'Dropped table: LaneOwnership';
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Workstations')
BEGIN
    DROP TABLE Workstations;
    PRINT 'Dropped table: Workstations';
END
