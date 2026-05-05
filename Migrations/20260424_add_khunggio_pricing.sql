-- Migration: Add KhungGio and BangGiaKhungGio tables and migrate legacy pricing
-- Date: 2026-04-24
-- This script preserves existing BangGia columns (GiaBanNgay, GiaQuaDem, GiaThang)
-- and migrates their values into the new normalized time-slot pricing tables.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- 1) Create KhungGio table
IF OBJECT_ID(N'dbo.KhungGio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KhungGio (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TenKhungGio NVARCHAR(100) NOT NULL,
        GioBatDau TIME NOT NULL,
        GioKetThuc TIME NOT NULL,
        QuaDem BIT NOT NULL CONSTRAINT DF_KhungGio_QuaDem DEFAULT(0),
        TrangThai BIT NOT NULL CONSTRAINT DF_KhungGio_TrangThai DEFAULT(1)
    );
END

-- 2) Create BangGiaKhungGio table
IF OBJECT_ID(N'dbo.BangGiaKhungGio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BangGiaKhungGio (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BangGiaId INT NOT NULL,
        KhungGioId INT NOT NULL,
        GiaTien DECIMAL(18,2) NOT NULL,
        CONSTRAINT FK_BangGiaKhungGio_BangGia FOREIGN KEY (BangGiaId) REFERENCES dbo.BangGia(Id),
        CONSTRAINT FK_BangGiaKhungGio_KhungGio FOREIGN KEY (KhungGioId) REFERENCES dbo.KhungGio(Id)
    );
END

-- 3) Seed KhungGio (day / night). Use localized names as requested.
IF NOT EXISTS (SELECT 1 FROM dbo.KhungGio WHERE TenKhungGio = N'Ban ngày')
BEGIN
    INSERT INTO dbo.KhungGio (TenKhungGio, GioBatDau, GioKetThuc, QuaDem, TrangThai)
    VALUES (N'Ban ngày', '06:00:00', '18:00:00', 0, 1);
END

IF NOT EXISTS (SELECT 1 FROM dbo.KhungGio WHERE TenKhungGio = N'Ban đêm')
BEGIN
    -- Night is overnight: starts 18:00, ends 06:00 (QuaDem = 1)
    INSERT INTO dbo.KhungGio (TenKhungGio, GioBatDau, GioKetThuc, QuaDem, TrangThai)
    VALUES (N'Ban đêm', '18:00:00', '06:00:00', 1, 1);
END

-- 4) Ensure LoaiXe and LoaiVe seed values exist and capture their ids
-- Note: adjust table names if your schema uses different names.
-- LoaiXe: Xe máy, Ô tô
IF OBJECT_ID(N'dbo.LoaiXe', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiXe WHERE Ten = N'Xe máy')
        INSERT INTO dbo.LoaiXe (Ten) VALUES (N'Xe máy');
    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiXe WHERE Ten = N'Ô tô')
        INSERT INTO dbo.LoaiXe (Ten) VALUES (N'Ô tô');
END

-- LoaiVe: Vãng lai, Tháng
IF OBJECT_ID(N'dbo.LoaiVe', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiVe WHERE Ten = N'Vãng lai')
        INSERT INTO dbo.LoaiVe (Ten) VALUES (N'Vãng lai');
    IF NOT EXISTS (SELECT 1 FROM dbo.LoaiVe WHERE Ten = N'Tháng')
        INSERT INTO dbo.LoaiVe (Ten) VALUES (N'Tháng');
END

-- 5) Migrate existing BangGia.GiaBanNgay and GiaQuaDem values into BangGiaKhungGio
-- Map GiaBanNgay -> KhungGio 'Ban ngày'
-- Map GiaQuaDem  -> KhungGio 'Ban đêm'

DECLARE @DayKhungId INT = (SELECT TOP(1) Id FROM dbo.KhungGio WHERE TenKhungGio = N'Ban ngày');
DECLARE @NightKhungId INT = (SELECT TOP(1) Id FROM dbo.KhungGio WHERE TenKhungGio = N'Ban đêm');

-- If KhungGio entries missing, abort (safety)
IF @DayKhungId IS NULL OR @NightKhungId IS NULL
BEGIN
    RAISERROR('Required KhungGio seed data missing. Aborting migration.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- Insert day prices where present and not already migrated
INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
SELECT b.Id, @DayKhungId, b.GiaBanNgay
FROM dbo.BangGia b
WHERE b.GiaBanNgay IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @DayKhungId);

-- Insert night prices where present and not already migrated
INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
SELECT b.Id, @NightKhungId, b.GiaQuaDem
FROM dbo.BangGia b
WHERE b.GiaQuaDem IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @NightKhungId);

-- 6) Insert sample BangGia entries (if not present) and assign KhungGio prices
-- Sample prices (adjust after validation):
-- Xe máy: day 5.00, night 3.00, month 200.00
-- Ô tô  : day 15.00, night 10.00, month 1000.00

DECLARE @XeMayId INT = NULL, @OToId INT = NULL, @VangLaiId INT = NULL, @ThangId INT = NULL;
IF OBJECT_ID(N'dbo.LoaiXe', N'U') IS NOT NULL
BEGIN
    SELECT @XeMayId = Id FROM dbo.LoaiXe WHERE Ten = N'Xe máy';
    SELECT @OToId = Id FROM dbo.LoaiXe WHERE Ten = N'Ô tô';
END
IF OBJECT_ID(N'dbo.LoaiVe', N'U') IS NOT NULL
BEGIN
    SELECT @VangLaiId = Id FROM dbo.LoaiVe WHERE Ten = N'Vãng lai';
    SELECT @ThangId = Id FROM dbo.LoaiVe WHERE Ten = N'Tháng';
END

-- Helper: create BangGia if missing
IF @XeMayId IS NOT NULL AND @VangLaiId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @XeMayId AND LoaiVeId = @VangLaiId)
    BEGIN
        INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai, GiaBanNgay, GiaQuaDem)
        VALUES (@XeMayId, @VangLaiId, NULL, N'1', 5.00, 3.00);
    END
END

IF @XeMayId IS NOT NULL AND @ThangId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @XeMayId AND LoaiVeId = @ThangId)
    BEGIN
        INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai)
        VALUES (@XeMayId, @ThangId, 200.00, N'1');
    END
END

IF @OToId IS NOT NULL AND @VangLaiId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @OToId AND LoaiVeId = @VangLaiId)
    BEGIN
        INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai, GiaBanNgay, GiaQuaDem)
        VALUES (@OToId, @VangLaiId, NULL, N'1', 15.00, 10.00);
    END
END

IF @OToId IS NOT NULL AND @ThangId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.BangGia WHERE LoaiXeId = @OToId AND LoaiVeId = @ThangId)
    BEGIN
        INSERT INTO dbo.BangGia (LoaiXeId, LoaiVeId, GiaThang, TrangThai)
        VALUES (@OToId, @ThangId, 1000.00, N'1');
    END
END

-- 7) For any newly created BangGia rows above, ensure BangGiaKhungGio day/night pricing exist
-- Insert for Day
INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
SELECT b.Id, @DayKhungId,
       CASE WHEN b.LoaiVeId = @ThangId THEN 0.00 ELSE
            CASE WHEN b.LoaiXeId = @XeMayId THEN 5.00 WHEN b.LoaiXeId = @OToId THEN 15.00 ELSE 0.00 END END
FROM dbo.BangGia b
WHERE NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @DayKhungId)
  AND (b.GiaBanNgay IS NULL OR b.GiaBanNgay = 0) -- only for ones we seeded
  AND (b.LoaiVeId = @VangLaiId OR b.LoaiVeId = @ThangId);

-- Insert for Night
INSERT INTO dbo.BangGiaKhungGio (BangGiaId, KhungGioId, GiaTien)
SELECT b.Id, @NightKhungId,
       CASE WHEN b.LoaiVeId = @ThangId THEN 0.00 ELSE
            CASE WHEN b.LoaiXeId = @XeMayId THEN 3.00 WHEN b.LoaiXeId = @OToId THEN 10.00 ELSE 0.00 END END
FROM dbo.BangGia b
WHERE NOT EXISTS (SELECT 1 FROM dbo.BangGiaKhungGio bgk WHERE bgk.BangGiaId = b.Id AND bgk.KhungGioId = @NightKhungId)
  AND (b.GiaQuaDem IS NULL OR b.GiaQuaDem = 0)
  AND (b.LoaiVeId = @VangLaiId OR b.LoaiVeId = @ThangId);

COMMIT TRANSACTION;

PRINT 'Migration completed. KhungGio and BangGiaKhungGio added and legacy prices migrated.';
