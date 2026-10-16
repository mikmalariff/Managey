# Installs or updates Managey as a Windows Service.
# Run from the extracted package folder, in PowerShell opened with "Run as administrator":
#   powershell -ExecutionPolicy Bypass -File .\install.ps1
#   powershell -ExecutionPolicy Bypass -File .\install.ps1 -InstallDir D:\Managey -Port 8080
# Updating: run the new package's install.ps1 the same way. Your data folder and appsettings.json are kept.
param(
    [string]$InstallDir = 'C:\Managey',
    [int]$Port = 0
)
$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Please run PowerShell as administrator (right-click > Run as administrator).'
}

$source = $PSScriptRoot
if (-not (Test-Path (Join-Path $source 'Managey.Api.exe'))) { throw "Managey.Api.exe not found next to this script ($source)." }
$isUpdate = Test-Path (Join-Path $InstallDir 'Managey.Api.exe')

$service = Get-Service Managey -ErrorAction SilentlyContinue
if ($service -and $service.Status -ne 'Stopped') {
    Write-Host 'Stopping the Managey service...'
    Stop-Service Managey
    Start-Sleep -Seconds 2
}

Write-Host "Copying files to $InstallDir ..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
# Never overwrite the data folder (database, uploads, keys) or an existing configuration.
$robocopyArgs = @($source, $InstallDir, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/XD', 'data')
if ($isUpdate -and (Test-Path (Join-Path $InstallDir 'appsettings.json'))) { $robocopyArgs += @('/XF', 'appsettings.json') }
robocopy @robocopyArgs | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Copying files failed (robocopy exit code $LASTEXITCODE)." }

# Port: keep the configured one unless -Port was given.
$settingsPath = Join-Path $InstallDir 'appsettings.json'
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
if ($Port -gt 0) {
    $settings.Urls = "http://0.0.0.0:$Port"
    $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
}
if ($settings.Urls -match ':(\d+)') { $Port = [int]$Matches[1] } else { $Port = 5080 }

$exe = Join-Path $InstallDir 'Managey.Api.exe'
if (-not $service) {
    Write-Host 'Creating the Managey service...'
    New-Service -Name Managey -DisplayName 'Managey' -BinaryPathName "`"$exe`"" `
        -StartupType Automatic -Description 'Managey HR, leave, attendance, approvals and payroll' | Out-Null
    # Restart automatically if it ever stops unexpectedly.
    sc.exe failure Managey reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null
}

$ruleName = "Managey (TCP $Port)"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -Profile Any | Out-Null
}

Start-Service Managey
Write-Host 'Starting...'
$ok = $false
for ($i = 0; $i -lt 30 -and -not $ok; $i++) {
    Start-Sleep -Seconds 1
    try { $ok = (Invoke-RestMethod "http://localhost:$Port/api/health" -TimeoutSec 2).status -eq 'ok' } catch { }
}
if (-not $ok) { throw "Managey did not start. Check Event Viewer > Windows Logs > Application, or run $exe in a console to see the error." }

$ips = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } | Select-Object -ExpandProperty IPAddress
Write-Host ''
Write-Host ($(if ($isUpdate) { 'Managey updated and running.' } else { 'Managey installed and running.' })) -ForegroundColor Green
Write-Host "Open one of these from any PC on the network:"
Write-Host "  http://$($env:COMPUTERNAME):$Port"
foreach ($ip in $ips) { Write-Host "  http://${ip}:$Port" }
if (-not $isUpdate) { Write-Host "First sign-in: admin / ChangeMe123!  (change it straight away under Account)" -ForegroundColor Yellow }
