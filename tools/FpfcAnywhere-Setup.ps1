<#
.SYNOPSIS
Installs or removes FPFC Anywhere in a copy of Beat Saber 1.45.3.

.DESCRIPTION
Unity builds list their assemblies and startup methods in two JSON files. This script adds one
entry to each and copies the mod into Managed. No game binary is changed, and -Uninstall takes the
mod and both entries back out.

.PARAMETER GameDir
The game folder, or Beat Saber.exe inside it. Defaults to the BSManager instance named 1.45.3,
and asks where the game is if that folder is not usable.

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
    [string]$GameDir,
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

# Install.cmd stays open after this, so a person reads the message rather than a stack trace.
function Stop-Setup([string]$Message, [string]$Detail) {
    Write-Host $Message -ForegroundColor Red
    if ($Detail) {
        Write-Host $Detail
    }

    exit 1
}

# Takes what was typed, pasted or dragged in. Dragging adds quotes, and some paths come with a slash.
function ConvertTo-GameFolder([string]$Text) {
    $folder = $Text.Trim().Trim('"').Trim().TrimEnd('\')
    if ($folder.EndsWith('\Beat Saber.exe', [StringComparison]::OrdinalIgnoreCase)) {
        return $folder.Substring(0, $folder.Length - '\Beat Saber.exe'.Length)
    }

    return $folder
}

# The folder can be anything a person typed. Join-Path throws on a missing drive and Test-Path on
# characters no path can have, while File.Exists just says no.
function Get-GameProblem([string]$Folder) {
    if (-not [IO.File]::Exists("$Folder\Beat Saber.exe")) {
        return "There's no Beat Saber.exe in '$Folder'."
    }

    foreach ($name in @('ScriptingAssemblies.json', 'RuntimeInitializeOnLoads.json')) {
        if (-not [IO.File]::Exists("$Folder\Beat Saber_Data\$name")) {
            return "'$Folder' is missing $name, so it isn't a full copy of the game. Download it again in BSManager."
        }
    }

    if ($Uninstall) {
        return $null
    }

    $versionFile = "$Folder\BeatSaberVersion.txt"
    if (-not [IO.File]::Exists($versionFile)) {
        return "This needs Beat Saber 1.45.3, but '$Folder' doesn't say what version it is."
    }

    $version = (Read-Text $versionFile).Trim()
    if ($version -notmatch '^1\.45\.3(_|$)') {
        return "This needs Beat Saber 1.45.3, but '$Folder' is $version."
    }

    if (-not $AllowStoreInstall -and $Folder -match '(?i)steamapps|hyperbolic-magnetism-beat-saber') {
        return "'$Folder' is your Steam or Meta install. Use a BSManager copy instead."
    }

    return $null
}

function Read-GameFolder {
    while ($true) {
        Write-Host 'Drag your 1.45.3 game folder (the one with Beat Saber.exe in it) into this window and press Enter.'
        Write-Host 'To quit, just press Enter.'

        # Read-Host fails outright without a console. This returns nothing, which counts as quitting.
        $answer = [Console]::In.ReadLine()
        if ([string]::IsNullOrWhiteSpace($answer)) {
            return $null
        }

        $folder = ConvertTo-GameFolder $answer
        $problem = Get-GameProblem $folder
        if (-not $problem) {
            return $folder
        }

        Write-Host $problem -ForegroundColor Red
    }
}

function Find-GameFolder {
    if ($GameDir) {
        $folder = ConvertTo-GameFolder $GameDir
        $problem = Get-GameProblem $folder
        if ($problem) {
            Stop-Setup $problem
        }

        return $folder
    }

    $folder = Join-Path $env:USERPROFILE 'BSManager\BSInstances\1.45.3'
    $problem = Get-GameProblem $folder
    if (-not $problem) {
        return $folder
    }

    Write-Host $problem
    $folder = Read-GameFolder
    # Choosing to quit is not an error, so this one stays out of the red.
    if (-not $folder) {
        Write-Host 'Nothing was changed.'
        exit 1
    }

    return $folder
}

# Only this copy matters, a different one running elsewhere has no hold on these files.
function Test-GameRunning([string]$Folder) {
    $exe = [IO.Path]::GetFullPath("$Folder\Beat Saber.exe")
    foreach ($process in @(Get-Process -Name 'Beat Saber' -ErrorAction SilentlyContinue)) {
        # Windows hides the path of a process run as admin, so an unknown one counts as this copy.
        if (-not $process.Path -or $process.Path -ieq $exe) {
            return $true
        }
    }

    return $false
}

try {
    $GameDir = Find-GameFolder

    if (Test-GameRunning $GameDir) {
        Stop-Setup 'Close Beat Saber first, then run this again.'
    }

    if (-not $Uninstall -and -not (Test-Path -LiteralPath $DllPath)) {
        Stop-Setup "$AssemblyFile isn't next to this script. Unzip the whole zip, then run this again."
    }

    $dataDir = Join-Path $GameDir 'Beat Saber_Data'
    $assembliesPath = Join-Path $dataDir 'ScriptingAssemblies.json'
    $initPath = Join-Path $dataDir 'RuntimeInitializeOnLoads.json'
    $installedDll = Join-Path $dataDir "Managed\$AssemblyFile"

    $assemblies = Read-Text $assembliesPath
    $init = Read-Text $initPath

    # Both results are checked before either file is touched, so a failure cannot leave half an install.
    try {
        if ($Uninstall) {
            $newAssemblies = Remove-Assembly $assemblies
            $newInit = Remove-InitEntry $init
        }
        else {
            $newAssemblies = Add-Assembly $assemblies
            $newInit = Add-InitEntry $init
        }

        $null = $newAssemblies | ConvertFrom-Json
        $null = $newInit | ConvertFrom-Json
    }
    catch {
        Stop-Setup "The game's file lists in '$GameDir' look damaged, so nothing was changed. Download that copy again in BSManager."
    }

    if ($Uninstall) {
        $hasMod = Test-Path -LiteralPath $installedDll
        if (-not $hasMod -and $newAssemblies -ceq $assemblies -and $newInit -ceq $init) {
            Write-Host "FPFC Anywhere isn't installed in $GameDir"
            return
        }

        # The lists go first: an unlisted DLL is ignored by the game, a listed missing one is not.
        Write-Text $assembliesPath $newAssemblies
        Write-Text $initPath $newInit
        if ($hasMod) {
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
    Write-Host 'Launch that copy from BSManager with FPFC on.'
}
catch [System.IO.IOException], [System.UnauthorizedAccessException] {
    Stop-Setup "Windows wouldn't let this change the game files. Close Beat Saber and anything else using that folder, then run this again." $_.Exception.Message
}
catch {
    Stop-Setup "Setup failed: $($_.Exception.Message)"
}
