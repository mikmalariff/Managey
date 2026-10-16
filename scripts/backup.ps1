# Backs up Managey's data (database, uploaded files, encryption keys) to a dated zip.
#   powershell -ExecutionPolicy Bypass -File C:\Managey\backup.ps1 -BackupDir D:\Backups\Managey
# Schedule it daily with Task Scheduler (see the installation guide). The service pauses for a few seconds
# so the database is copied in a consistent state. Old backups beyond -Keep are deleted.
param(
    [string]$InstallDir = 'C:\Managey',
    [string]$BackupDir = 'C:\ManageyBackups',
    [int]$Keep = 30
)
$ErrorActionPreference = 'Stop'
$data = Join-Path $InstallDir 'data'
if (-not (Test-Path $data)) { throw "No data folder at $data." }
New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$staging = Join-Path $env:TEMP "managey-backup-$stamp"
$running = (Get-Service Managey -ErrorAction SilentlyContinue).Status -eq 'Running'
if ($running) { Stop-Service Managey }
try {
    Copy-Item $data $staging -Recurse
    Copy-Item (Join-Path $InstallDir 'appsettings.json') $staging -ErrorAction SilentlyContinue
} finally {
    if ($running) { Start-Service Managey }
}

$zip = Join-Path $BackupDir "managey-$stamp.zip"
Compress-Archive -Path "$staging\*" -DestinationPath $zip
Remove-Item $staging -Recurse -Force
Get-ChildItem $BackupDir -Filter 'managey-*.zip' | Sort-Object Name -Descending | Select-Object -Skip $Keep | Remove-Item
Write-Host "Backup saved: $zip"
