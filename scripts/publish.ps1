# Builds Managey and packs it into a zip you copy to the server.
#   .\scripts\publish.ps1                    -> publish\Managey-win-x64.zip  (Windows Server / Windows 10/11)
#   .\scripts\publish.ps1 -Runtime linux-x64 -> publish\Managey-linux-x64.zip (cloud Linux VM)
# Needs Node.js 20+ and the .NET 8 SDK on the build machine. The server needs neither: the build is self-contained.
param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "publish\Managey-$Runtime"

Push-Location "$root\client"
npm ci
npm run build
Pop-Location

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish "$root\server\Managey.Api\Managey.Api.csproj" -c Release -r $Runtime --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# Ship the helper scripts inside the package.
if ($Runtime -like 'win-*') {
    Copy-Item "$root\scripts\install.ps1", "$root\scripts\uninstall.ps1", "$root\scripts\backup.ps1" $out
} else {
    Copy-Item "$root\deploy\linux\*" $out
}

$zip = "$out.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$out\*" -DestinationPath $zip
Write-Host "Package ready: $zip"
