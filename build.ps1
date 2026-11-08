# Builds CamView and the MSI installer on Windows (e.g. pc-render).
# Requires the .NET 8 SDK:  winget install Microsoft.DotNet.SDK.8
# Usage:  .\build.ps1                 -> version from VERSION + .0, channel dev
#         .\build.ps1 -Build 42 -Channel test
param(
  [int]$Build = 0,
  [ValidateSet('production', 'test', 'dev')][string]$Channel = 'dev'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$version = "$((Get-Content VERSION).Trim()).$Build"
$publish = Join-Path $PSScriptRoot 'publish'
Remove-Item $publish, out -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Publishing CamView $version ($Channel)..." -ForegroundColor Cyan
dotnet publish src/CamView/CamView.csproj -c Release -o $publish -p:Version=$version -p:Channel=$Channel
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

Write-Host 'Building MSI...' -ForegroundColor Cyan
dotnet build installer/CamView.Installer.wixproj -c Release -o out -p:Version=$version "-p:PublishDir=$publish\"
if ($LASTEXITCODE) { throw 'MSI build failed' }

$msi = "out\CamView-$version.msi"
Move-Item out\CamView.msi $msi -Force
Write-Host "Done: $(Resolve-Path $msi)" -ForegroundColor Green
