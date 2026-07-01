# ==============================================================================
# APS-CONTROLS LIGHTWEIGHT DATABASE SYNC SERVICE (SQL EXPRESS EDITION COMPATIBLE)
# Performs bidirectional synchronization between Primary (1433) and Secondary (1434).
# Loop interval: 3 seconds.
# ==============================================================================

$primaryServer = "192.168.1.50,1433"
$secondaryServer = "192.168.1.50,1434"

$primaryConnStr = "Server=$primaryServer;Database=BaiXe;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=3"
$secondaryConnStr = "Server=$secondaryServer;Database=BaiXe;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=3"

write-host "======================================================================" -ForegroundColor Green
write-host "           APS DATABASE SYNC SERVICE IS RUNNING..." -ForegroundColor Green
write-host "  Primary Server:   $primaryServer" -ForegroundColor Cyan
write-host "  Secondary Server: $secondaryServer" -ForegroundColor Cyan
write-host "  Loop Interval:    3 seconds. Press Ctrl+C to stop." -ForegroundColor Yellow
write-host "======================================================================" -ForegroundColor Green

# Helper to execute a query and return DataTable
function Get-SqlData($connStr, $query) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
        $dt = New-Object System.Data.DataTable
        [void]$adapter.Fill($dt)
        return $dt
    }
    catch {
        # Silent fail or return null
        return $null
    }
    finally {
        $conn.Close()
    }
}

# Helper to execute non-query SQL
function Invoke-SqlNonQuery($connStr, $query, $parameters = @{}) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $query
        foreach ($p in $parameters.Keys) {
            [void]$cmd.Parameters.AddWithValue($p, $parameters[$p])
        }
        return $cmd.ExecuteNonQuery()
    }
    catch {
        write-host "SQL Execution Error on $connStr: $_" -ForegroundColor Red
        return -1
    }
    finally {
        $conn.Close()
    }
}

# Realtime sync loop
while ($true) {
    # 1. Test connection to both servers
    $pConn = New-Object System.Data.SqlClient.SqlConnection($primaryConnStr)
    $pOnline = $false
    try { $pConn.Open(); $pOnline = $true; $pConn.Close() } catch {}

    $sConn = New-Object System.Data.SqlClient.SqlConnection($secondaryConnStr)
    $sOnline = $false
    try { $sConn.Open(); $sOnline = $true; $sConn.Close() } catch {}

    if ($pOnline -and $sOnline) {
        # Both servers are online -> perform bidirectional sync
        
        # --- A. CLEANUP EXITED CARS FROM INVENTORY ON BOTH SIDES ---
        # If a card has completed an exit transaction, it shouldn't remain in XeTrongBai
        $cleanupSql = "DELETE FROM XeTrongBai WHERE EXISTS (SELECT 1 FROM LichSuXe WHERE LichSuXe.CardId = XeTrongBai.CardId AND LichSuXe.ThoiGianVao = XeTrongBai.ThoiGianVao AND LichSuXe.ThoiGianRa IS NOT NULL)"
        [void]$pRowsAffected = Invoke-SqlNonQuery $primaryConnStr $cleanupSql
        [void]$sRowsAffected = Invoke-SqlNonQuery $secondaryConnStr $cleanupSql

        # --- B. BIDIRECTIONAL SYNC FOR LichSuXe (Transactions) ---
        $pLichSu = Get-SqlData $primaryConnStr "SELECT CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId FROM LichSuXe"
        $sLichSu = Get-SqlData $secondaryConnStr "SELECT CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId FROM LichSuXe"

        if ($null -ne $pLichSu -and $null -ne $sLichSu) {
            # Find rows in Primary not in Secondary
            foreach ($pRow in $pLichSu.Rows) {
                # Match by CardId and Entry time
                $cardId = $pRow["CardId"]
                $timeVao = $pRow["ThoiGianVao"]
                
                # Check if it exists in Secondary
                $exists = $sLichSu.Rows | Where-Object { $_["CardId"] -eq $cardId -and $_["ThoiGianVao"] -eq $timeVao }
                if (-not $exists) {
                    write-host "[Sync] Copying transaction CardId $cardId (Vào: $timeVao) from Primary -> Secondary..." -ForegroundColor Gray
                    $queryInsert = @"
                        INSERT INTO LichSuXe (CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId)
                        VALUES (@CardId, @BienSo, @ThoiGianVao, @ThoiGianRa, @Tien, @TrangThai, @AnhVao, @AnhRa, @LoaiXeId, @LoaiVeId, @SiteId, @ZoneId, @EntryLaneId, @ExitLaneId, @EntryReaderId, @ExitReaderId, @EntryWorkstationId, @ExitWorkstationId, @EntryControllerId, @ExitControllerId)
"@
                    $params = @{
                        "@CardId" = $pRow["CardId"]; "@BienSo" = $pRow["BienSo"]; "@ThoiGianVao" = $pRow["ThoiGianVao"]; "@ThoiGianRa" = $pRow["ThoiGianRa"];
                        "@Tien" = $pRow["Tien"]; "@TrangThai" = $pRow["TrangThai"]; "@AnhVao" = $pRow["AnhVao"]; "@AnhRa" = $pRow["AnhRa"];
                        "@LoaiXeId" = $pRow["LoaiXeId"]; "@LoaiVeId" = $pRow["LoaiVeId"]; "@SiteId" = $pRow["SiteId"]; "@ZoneId" = $pRow["ZoneId"];
                        "@EntryLaneId" = $pRow["EntryLaneId"]; "@ExitLaneId" = $pRow["ExitLaneId"]; "@EntryReaderId" = $pRow["EntryReaderId"]; "@ExitReaderId" = $pRow["ExitReaderId"];
                        "@EntryWorkstationId" = $pRow["EntryWorkstationId"]; "@ExitWorkstationId" = $pRow["ExitWorkstationId"]; "@EntryControllerId" = $pRow["EntryControllerId"]; "@ExitControllerId" = $pRow["ExitControllerId"]
                    }
                    [void]Invoke-SqlNonQuery $secondaryConnStr $queryInsert $params
                }
            }

            # Find rows in Secondary not in Primary
            foreach ($sRow in $sLichSu.Rows) {
                $cardId = $sRow["CardId"]
                $timeVao = $sRow["ThoiGianVao"]
                
                $exists = $pLichSu.Rows | Where-Object { $_["CardId"] -eq $cardId -and $_["ThoiGianVao"] -eq $timeVao }
                if (-not $exists) {
                    write-host "[Sync] Copying transaction CardId $cardId (Vào: $timeVao) from Secondary -> Primary..." -ForegroundColor Gray
                    $queryInsert = @"
                        INSERT INTO LichSuXe (CardId, BienSo, ThoiGianVao, ThoiGianRa, Tien, TrangThai, AnhVao, AnhRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, ExitLaneId, EntryReaderId, ExitReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId)
                        VALUES (@CardId, @BienSo, @ThoiGianVao, @ThoiGianRa, @Tien, @TrangThai, @AnhVao, @AnhRa, @LoaiXeId, @LoaiVeId, @SiteId, @ZoneId, @EntryLaneId, @ExitLaneId, @EntryReaderId, @ExitReaderId, @EntryWorkstationId, @ExitWorkstationId, @EntryControllerId, @ExitControllerId)
"@
                    $params = @{
                        "@CardId" = $sRow["CardId"]; "@BienSo" = $sRow["BienSo"]; "@ThoiGianVao" = $sRow["ThoiGianVao"]; "@ThoiGianRa" = $sRow["ThoiGianRa"];
                        "@Tien" = $sRow["Tien"]; "@TrangThai" = $sRow["TrangThai"]; "@AnhVao" = $sRow["AnhVao"]; "@AnhRa" = $sRow["AnhRa"];
                        "@LoaiXeId" = $sRow["LoaiXeId"]; "@LoaiVeId" = $sRow["LoaiVeId"]; "@SiteId" = $sRow["SiteId"]; "@ZoneId" = $sRow["ZoneId"];
                        "@EntryLaneId" = $sRow["EntryLaneId"]; "@ExitLaneId" = $sRow["ExitLaneId"]; "@EntryReaderId" = $sRow["EntryReaderId"]; "@ExitReaderId" = $sRow["ExitReaderId"];
                        "@EntryWorkstationId" = $sRow["EntryWorkstationId"]; "@ExitWorkstationId" = $sRow["ExitWorkstationId"]; "@EntryControllerId" = $sRow["EntryControllerId"]; "@ExitControllerId" = $sRow["ExitControllerId"]
                    }
                    [void]Invoke-SqlNonQuery $primaryConnStr $queryInsert $params
                }
            }
        }

        # --- C. BIDIRECTIONAL SYNC FOR XeTrongBai (Active Parked Inventory) ---
        $pInventory = Get-SqlData $primaryConnStr "SELECT CardId, BienSo, ThoiGianVao, AnhXe, ThoiGianRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, EntryReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId, ExitLaneId FROM XeTrongBai"
        $sInventory = Get-SqlData $secondaryConnStr "SELECT CardId, BienSo, ThoiGianVao, AnhXe, ThoiGianRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, EntryReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId, ExitLaneId FROM XeTrongBai"

        if ($null -ne $pInventory -and $null -ne $sInventory) {
            # Copy active parked cars from Primary to Secondary
            foreach ($pRow in $pInventory.Rows) {
                $cardId = $pRow["CardId"]
                $exists = $sInventory.Rows | Where-Object { $_["CardId"] -eq $cardId }
                if (-not $exists) {
                    write-host "[Sync] Copying parked vehicle CardId $cardId (BienSo: $($pRow['BienSo'])) from Primary -> Secondary..." -ForegroundColor Green
                    $queryInsert = @"
                        INSERT INTO XeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe, ThoiGianRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, EntryReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId, ExitLaneId)
                        VALUES (@CardId, @BienSo, @ThoiGianVao, @AnhXe, @ThoiGianRa, @LoaiXeId, @LoaiVeId, @SiteId, @ZoneId, @EntryLaneId, @EntryReaderId, @EntryWorkstationId, @ExitWorkstationId, @EntryControllerId, @ExitControllerId, @ExitLaneId)
"@
                    $params = @{
                        "@CardId" = $pRow["CardId"]; "@BienSo" = $pRow["BienSo"]; "@ThoiGianVao" = $pRow["ThoiGianVao"]; "@AnhXe" = $pRow["AnhXe"]; "@ThoiGianRa" = $pRow["ThoiGianRa"];
                        "@LoaiXeId" = $pRow["LoaiXeId"]; "@LoaiVeId" = $pRow["LoaiVeId"]; "@SiteId" = $pRow["SiteId"]; "@ZoneId" = $pRow["ZoneId"];
                        "@EntryLaneId" = $pRow["EntryLaneId"]; "@EntryReaderId" = $pRow["EntryReaderId"]; "@EntryWorkstationId" = $pRow["EntryWorkstationId"]; "@ExitWorkstationId" = $pRow["ExitWorkstationId"];
                        "@EntryControllerId" = $pRow["EntryControllerId"]; "@ExitControllerId" = $pRow["ExitControllerId"]; "@ExitLaneId" = $pRow["ExitLaneId"]
                    }
                    [void]Invoke-SqlNonQuery $secondaryConnStr $queryInsert $params
                }
            }

            # Copy active parked cars from Secondary to Primary
            foreach ($sRow in $sInventory.Rows) {
                $cardId = $sRow["CardId"]
                $exists = $pInventory.Rows | Where-Object { $_["CardId"] -eq $cardId }
                if (-not $exists) {
                    write-host "[Sync] Copying parked vehicle CardId $cardId (BienSo: $($sRow['BienSo'])) from Secondary -> Primary..." -ForegroundColor Green
                    $queryInsert = @"
                        INSERT INTO XeTrongBai (CardId, BienSo, ThoiGianVao, AnhXe, ThoiGianRa, LoaiXeId, LoaiVeId, SiteId, ZoneId, EntryLaneId, EntryReaderId, EntryWorkstationId, ExitWorkstationId, EntryControllerId, ExitControllerId, ExitLaneId)
                        VALUES (@CardId, @BienSo, @ThoiGianVao, @AnhXe, @ThoiGianRa, @LoaiXeId, @LoaiVeId, @SiteId, @ZoneId, @EntryLaneId, @EntryReaderId, @EntryWorkstationId, @ExitWorkstationId, @EntryControllerId, @ExitControllerId, @ExitLaneId)
"@
                    $params = @{
                        "@CardId" = $sRow["CardId"]; "@BienSo" = $sRow["BienSo"]; "@ThoiGianVao" = $sRow["ThoiGianVao"]; "@AnhXe" = $sRow["AnhXe"]; "@ThoiGianRa" = $sRow["ThoiGianRa"];
                        "@LoaiXeId" = $sRow["LoaiXeId"]; "@LoaiVeId" = $sRow["LoaiVeId"]; "@SiteId" = $sRow["SiteId"]; "@ZoneId" = $sRow["ZoneId"];
                        "@EntryLaneId" = $sRow["EntryLaneId"]; "@EntryReaderId" = $sRow["EntryReaderId"]; "@EntryWorkstationId" = $sRow["EntryWorkstationId"]; "@ExitWorkstationId" = $sRow["ExitWorkstationId"];
                        "@EntryControllerId" = $sRow["EntryControllerId"]; "@ExitControllerId" = $sRow["ExitControllerId"]; "@ExitLaneId" = $sRow["ExitLaneId"]
                    }
                    [void]Invoke-SqlNonQuery $primaryConnStr $queryInsert $params
                }
            }
        }
    }
    else {
        write-host "[Sync] One or both databases are offline (Primary: $(if($pOnline){'ON'}else{'OFF'}), Secondary: $(if($sOnline){'ON'}else{'OFF'})). Waiting..." -ForegroundColor Red
    }

    Start-Sleep -Seconds 3
}
