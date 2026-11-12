<#
.SYNOPSIS
Installs or removes FPFC Anywhere in a copy of Beat Saber 1.46.0 or 1.45.3.

.DESCRIPTION
Unity builds list their assemblies and startup methods in two JSON files. This script adds one
entry to each and copies the mod into Managed. No game binary is changed, and -Uninstall takes the
mod and both entries back out.

.PARAMETER GameDir
The game folder, or Beat Saber.exe inside it. Left out, the script looks through BSManager, Steam
and Meta by itself, and only asks if it finds no copy it can use or more than one.

.PARAMETER DllPath
The built mod. Defaults to FpfcAnywhere.dll beside this script.

.PARAMETER Uninstall
Removes the mod instead of installing it.

.PARAMETER AllowStoreInstall
Does nothing now. Steam and Meta installs are treated like any other, and this stays so older
instructions keep working.

.PARAMETER SteamRoot
Where Steam is. Defaults to what Windows says.

.PARAMETER MetaLibraries
Meta library folders, separated by semicolons. Defaults to what Windows says.
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$DllPath,
    [switch]$Uninstall,
    [switch]$AllowStoreInstall,
    [string]$SteamRoot,
    [string]$MetaLibraries
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell leaves $PSScriptRoot empty in the param block of an advanced script.
if (-not $DllPath) {
    $DllPath = Join-Path $PSScriptRoot 'FpfcAnywhere.dll'
}

$AssemblyFile = 'FpfcAnywhere.dll'
$SupportedVersions = @('1.46.0', '1.45.3')
$SupportedText = $SupportedVersions -join ' or '
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
            return "'$Folder' is missing $name, so it isn't a full copy of the game. $(Get-RepairAdvice $Folder)"
        }
    }

    if ($Uninstall) {
        return $null
    }

    # A copy that doesn't say is let through, because the mod checks the real version when it
    # starts and switches itself off on anything else. Turning a working game away is worse.
    $version = Get-GameVersion $Folder
    if ($version -and -not (Test-SupportedVersion $version)) {
        return "This needs Beat Saber $SupportedText, but '$Folder' is $version."
    }

    return $null
}

function Test-SupportedVersion([string]$Version) {
    foreach ($supported in $SupportedVersions) {
        if ($Version -eq $supported -or $Version.StartsWith("${supported}_", [StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

# The game keeps its real version in its own data, which every update rewrites. BeatSaberVersion.txt
# can lag behind: right after the 1.46.0 update it still said 1.45.3. So the data wins, and the text
# file is only a fallback.
function Get-GameVersion([string]$Folder) {
    try {
        $data = "$Folder\Beat Saber_Data\globalgamemanagers"
        if ([IO.File]::Exists($data)) {
            $found = [regex]::Match([Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($data)), '\d+\.\d+\.\d+_\d+')
            if ($found.Success) {
                return $found.Value
            }
        }
    }
    catch {
    }

    $versionFile = "$Folder\BeatSaberVersion.txt"
    if ([IO.File]::Exists($versionFile)) {
        return (Read-Text $versionFile).Trim()
    }

    return $null
}

# Where a copy came from decides how it starts with FPFC and how a broken one gets fixed.
function Get-CopyKind([string]$Folder) {
    if ($Folder -match '(?i)\\BSManager\\BSInstances\\') {
        return 'BSManager'
    }

    if ($Folder -match '(?i)\\steamapps\\') {
        return 'Steam'
    }

    if ($Folder -match '(?i)hyperbolic-magnetism-beat-saber') {
        return 'Meta'
    }

    return 'Other'
}

# Steam and Meta put their own updates over these folders, which only takes the mod's entries back out.
function Test-StoreFolder([string]$Folder) {
    return (Get-CopyKind $Folder) -in @('Steam', 'Meta')
}

function Get-RepairAdvice([string]$Folder) {
    switch (Get-CopyKind $Folder) {
        'BSManager' { return 'Download it again in BSManager.' }
        'Steam' { return 'In Steam, right-click Beat Saber, pick Properties, then Installed Files, then Verify integrity of game files.' }
        'Meta' { return 'Repair or reinstall it in the Meta app.' }
        default { return 'Copy the game folder again from a working install.' }
    }
}

# Meta has no launch options box, so those copies, and any loose copy, get the exact line to run.
function Write-LaunchSteps([string]$Folder) {
    switch (Get-CopyKind $Folder) {
        'BSManager' {
            Write-Host 'Launch that copy from BSManager with FPFC on.'
        }
        'Steam' {
            Write-Host 'To start it with FPFC on, right-click Beat Saber in Steam, pick Properties and type fpfc under Launch Options.'
        }
        default {
            Write-Host 'To start it with FPFC on, copy the line below, press Windows+R, paste it and press Enter.'
            Write-Host "`"$Folder\Beat Saber.exe`" fpfc"
        }
    }
}

function Read-SteamRoot {
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $values = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($values -and $values.SteamPath) {
            return $values.SteamPath
        }

        if ($values -and $values.InstallPath) {
            return $values.InstallPath
        }
    }

    return (Join-Path ${env:ProgramFiles(x86)} 'Steam')
}

function Read-MetaLibraries {
    $libraries = @()
    foreach ($library in @(Get-ChildItem -Path 'HKCU:\Software\Oculus VR, LLC\Oculus\Libraries' -ErrorAction SilentlyContinue)) {
        $original = (Get-ItemProperty -LiteralPath $library.PSPath -ErrorAction SilentlyContinue).OriginalPath
        if ($original) {
            $libraries += $original
        }
    }

    $libraries += Join-Path $env:ProgramFiles 'Meta Horizon\Software'
    $libraries += Join-Path $env:ProgramFiles 'Oculus\Software'
    return ($libraries -join ';')
}

# Everything below reads files other programs own, so a damaged one just means nothing found there.
function Get-BsManagerFolders {
    $roots = @($env:USERPROFILE)
    try {
        $settings = "$env:APPDATA\bs-manager\config.json"
        if ([IO.File]::Exists($settings)) {
            $moved = (Read-Text $settings | ConvertFrom-Json).'installation-folder'
            if ($moved) {
                $roots = @($moved) + $roots
            }
        }
    }
    catch {
    }

    $folders = @()
    foreach ($root in $roots) {
        try {
            $folders += @([IO.Directory]::GetDirectories("$root\BSManager\BSInstances"))
        }
        catch {
        }
    }

    return $folders
}

function Get-SteamFolders {
    $libraries = @()
    try {
        foreach ($match in [regex]::Matches((Read-Text "$SteamRoot\steamapps\libraryfolders.vdf"), '"path"\s+"([^"]+)"')) {
            $libraries += $match.Groups[1].Value.Replace('\\', '\')
        }
    }
    catch {
    }

    $libraries += $SteamRoot
    return @($libraries | ForEach-Object { "$_\steamapps\common\Beat Saber" })
}

function Get-MetaFolders {
    return @($MetaLibraries -split ';' | Where-Object { $_ } | ForEach-Object { "$($_.TrimEnd('\'))\Software\hyperbolic-magnetism-beat-saber" })
}

function Find-Copies {
    $places = @()
    foreach ($folder in @(Get-BsManagerFolders)) { $places += , @('BSManager', $folder) }
    foreach ($folder in @(Get-SteamFolders)) { $places += , @('Steam', $folder) }
    foreach ($folder in @(Get-MetaFolders)) { $places += , @('Meta', $folder) }

    $seen = @{}
    foreach ($place in $places) {
        try {
            $folder = [IO.Path]::GetFullPath($place[1]).TrimEnd('\')
        }
        catch {
            continue
        }

        $key = $folder.ToLowerInvariant()
        if ($seen.ContainsKey($key) -or -not [IO.File]::Exists("$folder\Beat Saber.exe")) {
            continue
        }

        $seen[$key] = $true
        $version = Get-GameVersion $folder
        New-Object PSObject -Property @{
            Kind = $place[0]
            Name = Split-Path $folder -Leaf
            Folder = $folder
            Version = $version
            OtherVersion = [bool]($version -and -not (Test-SupportedVersion $version))
            Problem = (Get-GameProblem $folder)
            HasMod = [IO.File]::Exists("$folder\Beat Saber_Data\Managed\$AssemblyFile")
        }
    }
}

function Read-GameFolder {
    while ($true) {
        Write-Host "Drag your Beat Saber $SupportedText game folder (the one with Beat Saber.exe in it) into this window and press Enter."
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

# 1.46.0_34060 reads as 1.46.0, the way people talk about it.
function Get-ShortVersion([string]$Version) {
    return ($Version -split '_')[0]
}

# BSManager copies go by the name they were given, since that's how a person tells them apart.
function Get-CopyLabel($Copy) {
    $label = if ($Copy.Kind -eq 'BSManager') { "BSManager `"$($Copy.Name)`"" } else { $Copy.Kind }
    if ($Copy.Version) {
        $label += ", $(Get-ShortVersion $Copy.Version)"
    }

    if ($Copy.HasMod -and -not $Uninstall) {
        $label += ', already has the mod'
    }

    return $label
}

function Read-CopyChoice($Copies) {
    Write-Host 'Found more than one copy:'
    for ($index = 0; $index -lt $Copies.Count; $index++) {
        Write-Host "  $($index + 1). $(Get-CopyLabel $Copies[$index])"
        Write-Host "     $($Copies[$index].Folder)"
    }

    while ($true) {
        Write-Host 'Type the number of the one you want and press Enter, or drag a different game folder in.'
        Write-Host 'To quit, just press Enter.'

        $answer = [Console]::In.ReadLine()
        if ([string]::IsNullOrWhiteSpace($answer)) {
            return $null
        }

        $number = 0
        if ([int]::TryParse($answer.Trim(), [ref]$number)) {
            if ($number -ge 1 -and $number -le $Copies.Count) {
                return $Copies[$number - 1].Folder
            }

            Write-Host "There's no number $number on that list." -ForegroundColor Red
            continue
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

    $copies = @(Find-Copies)
    $usable = @($copies | Where-Object { -not $_.Problem -and (-not $Uninstall -or $_.HasMod) })

    # Copies on other versions are named in one line, so nobody wonders where theirs went.
    $otherVersions = @($copies | Where-Object { $_.OtherVersion } | ForEach-Object { Get-ShortVersion $_.Version } | Sort-Object -Unique)
    if ($otherVersions.Count -gt 0) {
        Write-Host "Left out copies on $($otherVersions -join ', '). This needs Beat Saber $SupportedText."
    }

    foreach ($copy in $copies) {
        if ($copy.Problem -and -not $copy.OtherVersion) {
            Write-Host $copy.Problem
        }
    }

    if ($usable.Count -eq 1) {
        Write-Host "Found $(Get-CopyLabel $usable[0]) in $($usable[0].Folder)"
        return $usable[0].Folder
    }

    if ($usable.Count -gt 1) {
        $folder = Read-CopyChoice $usable
    }
    else {
        $wanted = if ($Uninstall) { 'FPFC Anywhere' } else { "Beat Saber $SupportedText" }
        Write-Host "Couldn't find $wanted in BSManager, Steam or Meta."
        $folder = Read-GameFolder
    }

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
    if (-not $PSBoundParameters.ContainsKey('SteamRoot')) {
        $SteamRoot = Read-SteamRoot
    }

    if (-not $PSBoundParameters.ContainsKey('MetaLibraries')) {
        $MetaLibraries = Read-MetaLibraries
    }

    $GameDir = Find-GameFolder

    # Closing the game and pressing Enter carries on, so a game left open doesn't mean starting over.
    while (Test-GameRunning $GameDir) {
        Write-Host 'Beat Saber is open. Close it, then press Enter here to carry on.'
        if ($null -eq [Console]::In.ReadLine()) {
            Stop-Setup 'Close Beat Saber first, then run this again.'
        }
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
        Stop-Setup "The game's file lists in '$GameDir' look damaged, so nothing was changed. $(Get-RepairAdvice $GameDir)"
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
    if (-not (Get-GameVersion $GameDir)) {
        Write-Host "Couldn't tell which version this copy is. The mod switches itself off on anything but $SupportedText."
    }

    Write-LaunchSteps $GameDir
    if (Test-StoreFolder $GameDir) {
        Write-Host 'A game update takes the mod back out, so just run Install.cmd again after one.'
    }
}
catch [System.IO.IOException], [System.UnauthorizedAccessException] {
    Stop-Setup "Windows wouldn't let this change the game files. Close Beat Saber and anything else using that folder, then run this again. If it still says no, right-click Install.cmd and choose Run as administrator." $_.Exception.Message
}
catch {
    Stop-Setup "Setup failed: $($_.Exception.Message)"
}
