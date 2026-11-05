<#
.SYNOPSIS
Installs or removes FPFC Anywhere in a copy of Beat Saber 1.45.3.

.DESCRIPTION
Unity builds list their assemblies and startup methods in two JSON files. This script adds one
entry to each and copies the mod into Managed. No game binary is changed, and -Uninstall removes
exactly what was added.

.PARAMETER GameDir
The game folder. Defaults to the BSManager instance named 1.45.3.

.PARAMETER DllPath
The built mod. Defaults to FpfcAnywhere.dll beside this script.

.PARAMETER Uninstall
Removes the mod instead of installing it.

.PARAMETER AllowStoreInstall
Allows installing into a Steam or Meta managed folder. Store updates and file checks can undo or
break the install, so a BSManager copy is the supported route.
#>
[CmdletBinding()]
param(
    [string]$GameDir = (Join-Path $env:USERPROFILE 'BSManager\BSInstances\1.45.3'),
    [string]$DllPath,
    [switch]$Uninstall,
    [switch]$AllowStoreInstall
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell leaves $PSScriptRoot empty in the param block of an advanced script.
if (-not $DllPath) {
    $DllPath = Join-Path $PSScriptRoot 'FpfcAnywhere.dll'
}

$AssemblyFile = 'FpfcAnywhere.dll'
$UserAssemblyType = 16
$InitEntry = '{"assemblyName":"FpfcAnywhere","nameSpace":"FpfcAnywhere","className":"Bootstrap","methodName":"Init","loadTypes":1,"isUnityClass":false}'

function Read-Text([string]$Path) {
    return [IO.File]::ReadAllText($Path)
}

function Write-Text([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}

function Format-Assemblies([string[]]$Names, [int[]]$Types) {
    $quoted = $Names | ForEach-Object { '"' + $_ + '"' }
    return '{"names":[' + ($quoted -join ',') + '],"types":[' + ($Types -join ',') + ']}'
}

function Add-Assembly([string]$Text) {
    $parsed = $Text | ConvertFrom-Json
    $names = @($parsed.names)
    if ($names -contains $AssemblyFile) {
        return $Text
    }

    return Format-Assemblies ($names + $AssemblyFile) (@($parsed.types) + $UserAssemblyType)
}

# The names and types lists run in parallel, so the matching type is found by position.
function Remove-Assembly([string]$Text) {
    $parsed = $Text | ConvertFrom-Json
    $names = @($parsed.names)
    $types = @($parsed.types)
    $index = [array]::IndexOf($names, $AssemblyFile)
    if ($index -lt 0) {
        return $Text
    }

    $keptNames = @(); $keptTypes = @()
    for ($i = 0; $i -lt $names.Count; $i++) {
        if ($i -ne $index) {
            $keptNames += $names[$i]
            $keptTypes += $types[$i]
        }
    }

    return Format-Assemblies $keptNames $keptTypes
}

function Add-InitEntry([string]$Text) {
    if ($Text.Contains($InitEntry)) {
        return $Text
    }

    $end = $Text.LastIndexOf(']')
    if ($end -lt 0) {
        throw 'RuntimeInitializeOnLoads.json is not in the expected format.'
    }

    return $Text.Insert($end, ',' + $InitEntry)
}

function Remove-InitEntry([string]$Text) {
    return $Text.Replace(',' + $InitEntry, '')
}

function Assert-Game {
    if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Beat Saber.exe'))) {
        throw "No Beat Saber.exe in '$GameDir'. Pass the game folder with -GameDir."
    }
}

function Assert-CanInstall {
    $versionFile = Join-Path $GameDir 'BeatSaberVersion.txt'
    $version = if (Test-Path -LiteralPath $versionFile) { (Read-Text $versionFile).Trim() } else { 'unknown' }
    if ($version -notmatch '^1\.45\.3(_|$)') {
        throw "This build supports Beat Saber 1.45.3 only, but '$GameDir' is $version."
    }

    if (-not $AllowStoreInstall -and $GameDir -match '(?i)steamapps|hyperbolic-magnetism-beat-saber') {
        throw "'$GameDir' is managed by a store. Use a BSManager copy, or pass -AllowStoreInstall."
    }

    if (-not (Test-Path -LiteralPath $DllPath)) {
        throw "Cannot find the mod at '$DllPath'."
    }
}

Assert-Game

$dataDir = Join-Path $GameDir 'Beat Saber_Data'
$assembliesPath = Join-Path $dataDir 'ScriptingAssemblies.json'
$initPath = Join-Path $dataDir 'RuntimeInitializeOnLoads.json'
$installedDll = Join-Path $dataDir "Managed\$AssemblyFile"

$assemblies = Read-Text $assembliesPath
$init = Read-Text $initPath

if ($Uninstall) {
    $newAssemblies = Remove-Assembly $assemblies
    $newInit = Remove-InitEntry $init
}
else {
    Assert-CanInstall
    $newAssemblies = Add-Assembly $assemblies
    $newInit = Add-InitEntry $init
}

# Both results are checked before either file is touched, so a failure cannot leave half an install.
$null = $newAssemblies | ConvertFrom-Json
$null = $newInit | ConvertFrom-Json

if ($Uninstall) {
    Write-Text $assembliesPath $newAssemblies
    Write-Text $initPath $newInit
    if (Test-Path -LiteralPath $installedDll) {
        Remove-Item -LiteralPath $installedDll -Force
    }

    Write-Host "FPFC Anywhere removed from $GameDir"
    return
}

foreach ($path in @($assembliesPath, $initPath)) {
    $backup = "$path.fpfc-backup"
    if (-not (Test-Path -LiteralPath $backup)) {
        Copy-Item -LiteralPath $path -Destination $backup
    }
}

Copy-Item -LiteralPath $DllPath -Destination $installedDll -Force
Write-Text $assembliesPath $newAssemblies
Write-Text $initPath $newInit

Write-Host "FPFC Anywhere installed in $GameDir"
Write-Host 'Launch this version from BSManager with FPFC turned on.'
