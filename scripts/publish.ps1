# Builds the web client and publishes a self-contained Windows build to .\publish
# Requires: Node.js 20+ and .NET 8 SDK on the build machine (the server needs neither).
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Push-Location "$root\client"
npm ci
npm run build
Pop-Location

dotnet publish "$root\server\Managey.Api\Managey.Api.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false `
    -o "$root\publish"

Write-Host "Published to $root\publish. Copy this folder to the server, then run install-service.ps1 there."
