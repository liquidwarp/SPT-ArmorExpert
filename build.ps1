#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds ArmorExpert and produces Releases/Liquidwarp.ArmorExpert.zip

.DESCRIPTION
    The SPT install is only ever read from: the mod compiles against the game's
    assemblies, which this repo expects in Shared\. Nothing is written back
    into the SPT install, and nothing is auto-installed into BepInEx\plugins.
    Everything the build produces stays in the repo, under bin/, obj/, Shared/ and
    Releases/ — all gitignored.

    Shared\ is not in the repo. If any reference assembly is missing it is
    copied in from -TarkovDir; an existing Shared folder is left alone, so pass
    -SyncRefs after an SPT update to refresh it.

    The archive unpacks as BepInEx/plugins/Liquidwarp.ArmorExpert.dll.

.PARAMETER TarkovDir
    Root of the SPT install to source the reference assemblies from. Read-only.

.PARAMETER SyncRefs
    Re-copy every reference assembly into Shared\ even if it already exists.

.EXAMPLE
    ./build.ps1

.EXAMPLE
    ./build.ps1 -TarkovDir 'D:\SPT 4.1 BE' -SyncRefs
#>
[CmdletBinding()]
param(
    [string]$TarkovDir = 'E:\Games\SPT 4.1 FIKA',
    [switch]$SyncRefs
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot

try {
    $shared = Join-Path $PSScriptRoot 'Shared'

    # Reference assembly -> where it lives inside an SPT install.
    $refs = [ordered]@{
        'Assembly-CSharp.dll'                   = 'EscapeFromTarkov_Data/Managed'
        'Comfort.dll'                           = 'EscapeFromTarkov_Data/Managed'
        'Sirenix.Serialization.dll'             = 'EscapeFromTarkov_Data/Managed'
        'ItemComponent.Types.dll'               = 'EscapeFromTarkov_Data/Managed'
        'UnityEngine.dll'                       = 'EscapeFromTarkov_Data/Managed'
        'UnityEngine.CoreModule.dll'            = 'EscapeFromTarkov_Data/Managed'
        'BepInEx.dll'                           = 'BepInEx/core'
        'spt-reflection.dll'                    = 'BepInEx/plugins/spt'
    }

    $missing = @($refs.Keys | Where-Object { -not (Test-Path (Join-Path $shared $_)) })
    if ($SyncRefs -or $missing.Count -gt 0) {
        if (-not (Test-Path (Join-Path $TarkovDir 'EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll'))) {
            throw "Not an SPT install: $TarkovDir`nPass a different one with -TarkovDir '<path>'."
        }
        $want = if ($SyncRefs) { @($refs.Keys) } else { $missing }
        Write-Host ("==> Reference assemblies ({0} from {1})" -f $want.Count, $TarkovDir) -ForegroundColor Cyan
        New-Item -ItemType Directory -Path $shared -Force | Out-Null
        foreach ($dll in $want) {
            $src = Join-Path $TarkovDir (Join-Path $refs[$dll] $dll)
            if (-not (Test-Path $src)) { throw "Missing in the SPT install: $src" }
            Copy-Item $src (Join-Path $shared $dll) -Force
        }
    }

    Write-Host "`n==> ArmorExpert (Release)" -ForegroundColor Cyan
    dotnet build ArmorExpert.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

    $zip = Join-Path $PSScriptRoot 'Releases/Liquidwarp.ArmorExpert.zip'
    if (-not (Test-Path $zip)) { throw "Build reported success but no archive at $zip." }

    $item = Get-Item $zip
    Write-Host "`n==> Archive" -ForegroundColor Green
    Write-Host ("    {0}" -f $item.FullName)
    Write-Host ("    {0:N0} bytes" -f $item.Length)
}
finally {
    Pop-Location
}
