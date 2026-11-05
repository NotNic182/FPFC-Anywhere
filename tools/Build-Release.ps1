<#
.SYNOPSIS
Builds the mod and packs everything a moderator needs into release\FpfcAnywhere-<version>.zip.
#>
[CmdletBinding()]
param(
    [string]$Version = '0.1.7'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $root 'release\FpfcAnywhere'
$zip = Join-Path $root "release\FpfcAnywhere-$Version.zip"

& dotnet build (Join-Path $root 'src\FpfcAnywhere\FpfcAnywhere.csproj') -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'The build failed.'
}

if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force -Confirm:$false
}

New-Item -ItemType Directory -Force -Path $stage | Out-Null

$files = @(
    'src\FpfcAnywhere\bin\Release\net472\FpfcAnywhere.dll',
    'tools\FpfcAnywhere-Setup.ps1',
    'tools\Install.cmd',
    'tools\Uninstall.cmd',
    'README.md'
)

foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $root $file) -Destination $stage
}

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force -Confirm:$false
}

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Write-Host "Packed $zip"
