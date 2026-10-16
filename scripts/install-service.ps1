# Installs Managey as a Windows Service. Run in an elevated PowerShell on the server.
#   .\install-service.ps1 -InstallDir C:\Managey -Port 5080
param(
    [string]$InstallDir = 'C:\Managey',
    [int]$Port = 5080
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $InstallDir 'Managey.Api.exe'
if (-not (Test-Path $exe)) { throw "Managey.Api.exe not found in $InstallDir. Copy the publish folder there first." }

if (Get-Service Managey -ErrorAction SilentlyContinue) {
    Stop-Service Managey
    sc.exe delete Managey | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name Managey -DisplayName 'Managey HR & Approvals' `
    -BinaryPathName "`"$exe`" --urls http://0.0.0.0:$Port" `
    -StartupType Automatic -Description 'Managey HR, leave, attendance and approvals'
sc.exe failure Managey reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null

New-NetFirewallRule -DisplayName "Managey (TCP $Port)" -Direction Inbound -Protocol TCP -LocalPort $Port -Action Allow -ErrorAction SilentlyContinue | Out-Null

Start-Service Managey
Write-Host "Managey is running. Open http://$($env:COMPUTERNAME):$Port from any PC on the network."
