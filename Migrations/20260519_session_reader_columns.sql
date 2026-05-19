-- ============================================================================
-- APS-CONTROLS Phase 8.2: Zone-Aware Session Engine
-- Migration: 20260519_session_reader_columns.sql
-- Safe: IF NOT EXISTS guards, idempotent, transaction-protected
-- ============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Add EntryReaderId to VehicleSessions
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'EntryReaderId')
BEGIN
    ALTER TABLE dbo.VehicleSessions ADD EntryReaderId INT NULL;
END

-- Add ExitReaderId to VehicleSessions
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'VehicleSessions' AND COLUMN_NAME = 'ExitReaderId')
BEGIN
    ALTER TABLE dbo.VehicleSessions ADD ExitReaderId INT NULL;
END

-- Add index for active session lookups by CardId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleSessions_CardId_Active' AND object_id = OBJECT_ID('dbo.VehicleSessions'))
BEGIN
    CREATE INDEX IX_VehicleSessions_CardId_Active ON dbo.VehicleSessions(CardId) WHERE ThoiGianRa IS NULL;
END

-- Add index for site-wide anti-passback queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VehicleSessions_SiteId_Active' AND object_id = OBJECT_ID('dbo.VehicleSessions'))
BEGIN
    CREATE INDEX IX_VehicleSessions_SiteId_Active ON dbo.VehicleSessions(SiteId) WHERE ThoiGianRa IS NULL;
END

COMMIT TRANSACTION;
