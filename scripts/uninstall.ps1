# Removes the Managey service and firewall rule. Your data is kept unless you add -RemoveData.
# Run in PowerShell as administrator:  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
param(
    [string]$InstallDir = 'C:\Managey',
    [switch]$RemoveData
)
$ErrorActionPreference = 'Stop'
if (Get-Service Managey -ErrorAction SilentlyContinue) {
    Stop-Service Managey -ErrorAction SilentlyContinue
    sc.exe delete Managey | Out-Null
    Write-Host 'Service removed.'
}
Get-NetFirewallRule -DisplayName 'Managey (TCP *)' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if (Test-Path $InstallDir) {
    Get-ChildItem $InstallDir -Exclude 'data', 'appsettings.json' | Remove-Item -Recurse -Force
    if ($RemoveData) { Remove-Item $InstallDir -Recurse -Force; Write-Host 'Program and data deleted.' }
    else { Write-Host "Program removed. Your data and settings are still in $InstallDir (data, appsettings.json)." }
}
