[System.Reflection.Assembly]::LoadWithPartialName('System.Data') | Out-Null
$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = 'Server=192.168.2.13,1433;Database=BaiXe;User Id=appuser;Password=123456;Timeout=5'
try {
    $conn.Open()
    Write-Host "--- C3Controllers ---"
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = 'SELECT Id, ControllerName, IpAddress, GateId, IsActive FROM dbo.C3Controllers'
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host ("Controller: Id=" + $reader["Id"] + ", Name=" + $reader["ControllerName"] + ", IP=" + $reader["IpAddress"] + ", GateId=" + $reader["GateId"] + ", IsActive=" + $reader["IsActive"])
    }
    $reader.Close()

    Write-Host "`n--- Lanes ---"
    $cmd.CommandText = 'SELECT Id, LaneCode, LaneName, GateId, IsActive FROM dbo.Lanes'
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host ("Lane: Id=" + $reader["Id"] + ", Code=" + $reader["LaneCode"] + ", Name=" + $reader["LaneName"] + ", GateId=" + $reader["GateId"] + ", IsActive=" + $reader["IsActive"])
    }
    $reader.Close()
} catch {
    Write-Error $_.Exception.Message
} finally {
    $conn.Close()
}
