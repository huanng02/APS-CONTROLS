param(
    [string]$Server = 'DESKTOP-BFOEO42\SQLEXPRESS02',
    [string]$Database = 'BaiXe',
    [string]$MigrationFile = '20260508_alter_password_column.sql'
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$migrationPath = Join-Path $scriptDir $MigrationFile
if (-not (Test-Path $migrationPath)) {
    Write-Error "Migration file not found: $migrationPath"
    exit 2
}

# Backup directory under repo
$backupDir = Join-Path $scriptDir "..\..\backups" | Resolve-Path -Relative:$false
if (-not (Test-Path $backupDir)) { New-Item -Path $backupDir -ItemType Directory | Out-Null }

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupFile = Join-Path $backupDir "${Database}_backup_$timestamp.bak"

Write-Host "Using Server: $Server" -ForegroundColor Cyan
Write-Host "Database: $Database" -ForegroundColor Cyan
Write-Host "Migration: $migrationPath" -ForegroundColor Cyan
Write-Host "Backup file: $backupFile" -ForegroundColor Cyan

function Run-UsingInvokeSqlcmd($server, $database, $query, $inputFile) {
    try {
        if ($inputFile) {
            Invoke-Sqlcmd -ServerInstance $server -Database $database -InputFile $inputFile -ErrorAction Stop
        } else {
            Invoke-Sqlcmd -ServerInstance $server -Database $database -Query $query -ErrorAction Stop
        }
        return $true
    } catch {
        Write-Error "Invoke-Sqlcmd failed: $($_.Exception.Message)"
        return $false
    }
}

function Run-UsingSqlcmd($server, $database, $query, $inputFile) {
    try {
        if ($inputFile) {
            & sqlcmd -S $server -d $database -E -i $inputFile
        } else {
            & sqlcmd -S $server -d $database -E -Q $query
        }
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd exited with code $LASTEXITCODE" }
        return $true
    } catch {
        Write-Error "sqlcmd failed: $($_.Exception.Message)"
        return $false
    }
}

# Determine available runner
$haveInvoke = (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) -ne $null
$haveSqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue) -ne $null

if (-not $haveInvoke -and -not $haveSqlcmd) {
    Write-Error "Neither Invoke-Sqlcmd (SqlServer module) nor sqlcmd utility is available. Install SqlServer PowerShell module or SQL Server Command Line Utilities."
    exit 3
}

# 1) Backup database
$backupQuery = "BACKUP DATABASE [$Database] TO DISK = N'$backupFile' WITH INIT, COMPRESSION;"
if ($haveInvoke) {
    Write-Host "Backing up using Invoke-Sqlcmd..."
    if (-not (Run-UsingInvokeSqlcmd -server $Server -database 'master' -query $backupQuery -inputFile $null)) { exit 4 }
} else {
    Write-Host "Backing up using sqlcmd..."
    if (-not (Run-UsingSqlcmd -server $Server -database 'master' -query $backupQuery -inputFile $null)) { exit 4 }
}
Write-Host "Backup complete: $backupFile" -ForegroundColor Green

# 2) Run migration script
if ($haveInvoke) {
    Write-Host "Running migration with Invoke-Sqlcmd..."
    if (-not (Run-UsingInvokeSqlcmd -server $Server -database $Database -query $null -inputFile $migrationPath)) { exit 5 }
} else {
    Write-Host "Running migration with sqlcmd..."
    if (-not (Run-UsingSqlcmd -server $Server -database $Database -query $null -inputFile $migrationPath)) { exit 5 }
}
Write-Host "Migration executed successfully." -ForegroundColor Green

# 3) Verify column
$verifyQuery = @"
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'NhanVien' AND COLUMN_NAME = 'Password';
"@

if ($haveInvoke) {
    $res = Invoke-Sqlcmd -ServerInstance $Server -Database $Database -Query $verifyQuery
} else {
    $res = & sqlcmd -S $Server -d $Database -E -Q $verifyQuery -W -s "," | Out-String
}

Write-Host "Verification result:" -ForegroundColor Cyan
Write-Host $res

Write-Host "Done." -ForegroundColor Green
exit 0
