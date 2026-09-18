<#
.SYNOPSIS
    Builds the UV-VIS meas application.

.DESCRIPTION
    Locates MSBuild (Visual Studio or Build Tools, falling back to the .NET Framework
    copy) and builds the project for x86, which is mandatory because the OmniDriver
    .NET assembly is an x86-only mixed-mode binary.

.PARAMETER Configuration
    Debug or Release. Defaults to Release.

.PARAMETER OmniDriverHome
    Folder holding NETOmniDriver-NET40.dll. Only needed when the project cannot find
    the OmniDriver installation by itself.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug
    .\build.ps1 -OmniDriverHome "D:\OceanOptics\OOI_HOME"
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OmniDriverHome
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$project = Join-Path $projectDir 'UV-VIS_meas.csproj'

if (-not (Test-Path $project)) {
    throw "Project file not found: $project"
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $roots = & $vswhere -all -products * -format value -property installationPath
        foreach ($root in $roots) {
            $candidate = Join-Path $root 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path $candidate) { return $candidate }
        }
    }

    $fallback = Join-Path $env:windir 'Microsoft.NET\Framework\v4.0.30319\MSBuild.exe'
    if (Test-Path $fallback) { return $fallback }

    throw 'MSBuild was not found. Install Visual Studio 2022 or the Build Tools.'
}

$msbuild = Find-MSBuild
Write-Host "MSBuild      : $msbuild"
Write-Host "Configuration: $Configuration"

$arguments = @(
    $project,
    "/p:Configuration=$Configuration",
    '/p:Platform=x86',
    '/v:minimal',
    '/nologo'
)

if ($OmniDriverHome) {
    if (-not (Test-Path (Join-Path $OmniDriverHome 'NETOmniDriver-NET40.dll'))) {
        throw "NETOmniDriver-NET40.dll was not found in '$OmniDriverHome'."
    }
    $arguments += "/p:OmniDriverHome=$OmniDriverHome"
}

& $msbuild @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $projectDir "bin\$Configuration\UV-VIS_meas.exe"
Write-Host ''
Write-Host "Build succeeded: $exe" -ForegroundColor Green
