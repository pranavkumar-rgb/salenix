# Salenix_Crypto: test -> publish -> zip for the ODC Portal (Integrate > External Logic).
# Usage (from this folder): powershell -ExecutionPolicy Bypass -File .\pack.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test .\Salenix_Crypto.slnx -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed, nothing packaged.' }

$publish = Join-Path $PSScriptRoot 'Salenix_Crypto\bin\Release\net10.0\publish'
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
dotnet publish .\Salenix_Crypto\Salenix_Crypto.csproj -c Release --no-self-contained
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist 'Salenix_Crypto.zip'
# Files must sit at the zip root (content of the publish folder, not the folder).
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip -Force
Write-Host "Zip ready: $zip"
