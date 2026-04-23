<#
Copies ZKTeco x64 SDK DLLs from your local SDK download folder into the repo so the app can load native DLLs.
Usage (from repo root):
  powershell -ExecutionPolicy Bypass -File .\scripts\copy-zkteco-sdk.ps1 -SourcePath "C:\Users\WIN 11\Downloads\ZKTeco_PullSDK" -CopyToRuntimes -RegisterCom

Parameters:
  -SourcePath    Path to the SDK folder you downloaded (default matches your message path).
  -CopyToRuntimes Also copy files into runtimes\win-x64\native (helpful for dotnet publish).
  -RegisterCom   Attempt to register known COM DLLs (runs regsvr32 - requires admin/elevated shell).
#>

param(
    [string]$SourcePath = "C:\Users\WIN 11\Downloads\ZKTeco_PullSDK",
    [switch]$CopyToRuntimes,
    [switch]$RegisterCom
)

function Write-Info([string]$m) { Write-Host "[INFO]  $m" -ForegroundColor Cyan }
function Write-Ok([string]$m) { Write-Host "[OK]    $m" -ForegroundColor Green }
function Write-Warn([string]$m) { Write-Host "[WARN]  $m" -ForegroundColor Yellow }
function Write-Err([string]$m) { Write-Host "[ERR]   $m" -ForegroundColor Red }

try {
    $scriptDir = Split-Path -Parent $PSCommandPath
    $repoRoot = Resolve-Path (Join-Path $scriptDir '..')
} catch {
    $repoRoot = Get-Location
}

$repoRoot = $repoRoot -replace "\\$", ''
Write-Info "Repo root: $repoRoot"
Write-Info "Source SDK path: $SourcePath"

if (-not (Test-Path $SourcePath)) {
    Write-Err "Source path not found: $SourcePath"
    exit 2
}

# Prefer x64 subfolder if exists
$x64Source = Join-Path $SourcePath 'x64'
if (Test-Path $x64Source) { Write-Info "Using x64 subfolder: $x64Source"; $source = $x64Source } else { $source = $SourcePath }

# Files we expect to copy (common names for ZKTeco Pull SDK)
$dllNames = @(
    'plcommpro.dll',
    'plcomms.dll',
    'plrscagent.dll',
    'plrscomm.dll',
    'pltcpcomm.dll',
    'plusbcomm.dll',
    'zkemkeeper.dll' # optional COM wrapper
)

$destLib = Join-Path $repoRoot 'Libs\C3SDK\x64'
$destRuntimes = Join-Path $repoRoot 'runtimes\win-x64\native'

New-Item -ItemType Directory -Force -Path $destLib | Out-Null
if ($CopyToRuntimes) { New-Item -ItemType Directory -Force -Path $destRuntimes | Out-Null }

$copied = @()
foreach ($name in $dllNames) {
    $candidate = Join-Path $source $name
    if (-not (Test-Path $candidate)) {
        # try root of provided SDK
        $candidate = Join-Path $SourcePath $name
    }

    if (Test-Path $candidate) {
        $dest = Join-Path $destLib $name
        Copy-Item -Path $candidate -Destination $dest -Force
        Write-Ok "Copied $name -> $dest"
        $copied += $dest

        if ($CopyToRuntimes) {
            $dest2 = Join-Path $destRuntimes $name
            Copy-Item -Path $candidate -Destination $dest2 -Force
            Write-Ok "Also copied to runtimes: $dest2"
        }
    } else {
        Write-Warn "Not found in SDK: $name"
    }
}

if ($copied.Count -eq 0) {
    Write-Warn "No SDK DLLs were copied. Verify the SourcePath and contents (check for x64 folder or specific DLL names)."
    exit 3
}

if ($RegisterCom) {
    # attempt to register any COM DLLs we copied (e.g., zkemkeeper.dll)
    $regsvr32 = Join-Path $env:windir 'System32\regsvr32.exe'
    if (-not (Test-Path $regsvr32)) { Write-Err "regsvr32 not found: $regsvr32"; exit 4 }

    foreach ($dll in $copied) {
        $fname = [IO.Path]::GetFileName($dll).ToLowerInvariant()
        if ($fname -in @('zkemkeeper.dll')) {
            Write-Info "Registering COM DLL: $dll (requires admin)"
            try {
                Start-Process -FilePath $regsvr32 -ArgumentList "/s `"$dll`"" -Wait -NoNewWindow -Verb RunAs
                Write-Ok "Registered $dll"
            } catch {
                Write-Warn ("Failed to register {0}: {1}" -f $dll, $_)
            }
        }
    }
}

Write-Info "Done. Remember to Clean (delete bin/obj) and Rebuild solution in Visual Studio (PlatformTarget = x64)."
exit 0
