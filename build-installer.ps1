# Builds the shop laptop installer:
#   1. runs the tests,
#   2. publishes one self-contained StorePOS.exe into .\publish,
#   3. compiles installer\StorePOS.iss into .\installer-output\StorePOS-Setup-<version>.exe
# Step 3 needs Inno Setup 6 (free): https://jrsoftware.org/isdl.php
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet test StorePOS.Tests -nologo
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; not building the installer.' }

if (Test-Path publish) { Remove-Item -Recurse -Force publish }
dotnet publish StorePOS/StorePOS.csproj -nologo -p:PublishProfile=Shop
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Published to .\publish. Install Inno Setup 6 to also build the one-file installer." -ForegroundColor Yellow
    exit 0
}
& $iscc installer\StorePOS.iss
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
Get-ChildItem installer-output
