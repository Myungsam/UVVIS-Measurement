<#
.SYNOPSIS
    Builds UV-VIS meas, optionally signs it, and packages it into a setup program.

.DESCRIPTION
    Stages the Release build with publish.ps1, then compiles installer\UV-VIS_meas.iss
    with Inno Setup. The version written into the installer is read from the built EXE,
    so the setup file name, the Programs-and-Features entry and the program itself can
    never disagree.

    Code signing is optional. Without a certificate the installer still builds, and
    Windows SmartScreen will warn the first users; with -CertificateThumbprint or
    -CertificateFile both the program and the setup file are signed and timestamped.

.PARAMETER OmniDriverHome
    Folder holding NETOmniDriver-NET40.dll, if the build cannot find it automatically.

.PARAMETER CertificateThumbprint
    SHA-1 thumbprint of a signing certificate in the current user's certificate store.

.PARAMETER CertificateFile
    Path to a .pfx signing certificate. Used with -CertificatePassword.

.PARAMETER CertificatePassword
    Password for the .pfx file.

.PARAMETER TimestampUrl
    RFC 3161 timestamp server. Signatures stay valid after the certificate expires only
    if they are timestamped, so this has a working default.

.PARAMETER IsccPath
    Full path to ISCC.exe, if Inno Setup is installed somewhere unusual.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -CertificateFile C:\keys\myungsam.pfx -CertificatePassword (Read-Host -AsSecureString)
#>
[CmdletBinding()]
param(
    [string]$OmniDriverHome,
    [string]$CertificateThumbprint,
    [string]$CertificateFile,
    [System.Security.SecureString]$CertificatePassword,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
$installerDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoDir = Split-Path -Parent $installerDir
$projectDir = Join-Path $repoDir 'UV-VIS_measurement_code'
$stageDir = Join-Path $installerDir 'staging'
$distDir = Join-Path $repoDir 'dist'

# ---------------------------------------------------------------- tools
function Find-Tool([string]$explicit, [string]$exeName, [string[]]$candidates) {
    if ($explicit) {
        if (-not (Test-Path $explicit)) { throw "$exeName not found at '$explicit'." }
        return $explicit
    }

    $onPath = Get-Command $exeName -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    return $null
}

$iscc = Find-Tool $IsccPath 'ISCC.exe' @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
)

if (-not $iscc) {
    throw @'
Inno Setup 6 was not found. Install it and run this script again:

    winget install -e --id JRSoftware.InnoSetup

or pass -IsccPath "C:\path\to\ISCC.exe".
'@
}

function Get-SignTool {
    $kit = 'C:\Program Files (x86)\Windows Kits\10\bin'
    $found = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($found) { return $found.Source }
    if (-not (Test-Path $kit)) { return $null }

    return Get-ChildItem -Path $kit -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

$signingRequested = $CertificateThumbprint -or $CertificateFile
$signTool = $null
if ($signingRequested) {
    $signTool = Get-SignTool
    if (-not $signTool) {
        throw 'signtool.exe was not found. Install the Windows SDK signing tools, or omit the certificate parameters to build an unsigned installer.'
    }
}

function Invoke-Sign([string]$path) {
    if (-not $signingRequested) { return }

    $arguments = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')
    if ($CertificateThumbprint) {
        $arguments += @('/sha1', $CertificateThumbprint)
    }
    else {
        if (-not (Test-Path $CertificateFile)) { throw "Certificate not found: $CertificateFile" }
        $arguments += @('/f', $CertificateFile)
        if ($CertificatePassword) {
            $plain = [System.Net.NetworkCredential]::new('', $CertificatePassword).Password
            $arguments += @('/p', $plain)
        }
    }
    $arguments += $path

    Write-Host "  signing $(Split-Path -Leaf $path)" -ForegroundColor DarkCyan
    & $signTool @arguments
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for '$path' (exit $LASTEXITCODE)." }
}

# ---------------------------------------------------------------- stage
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }

$publishArgs = @{ TargetDir = $stageDir; NoShortcuts = $true }
if ($OmniDriverHome) { $publishArgs['OmniDriverHome'] = $OmniDriverHome }
& (Join-Path $projectDir 'publish.ps1') @publishArgs

$stagedExe = Join-Path $stageDir 'UV-VIS_meas.exe'
if (-not (Test-Path $stagedExe)) { throw "Staged program not found: $stagedExe" }

# The EXE is the single source of the version; see Properties\AssemblyInfo.cs.
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($stagedExe).ProductVersion
if (-not $version) { throw "Could not read the version out of '$stagedExe'." }
$version = $version.Trim()

Invoke-Sign $stagedExe

# ---------------------------------------------------------------- compile
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

$script = Join-Path $installerDir 'UV-VIS_meas.iss'
& $iscc "/DAppVersion=$version" "/DStageDir=$stageDir" $script
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed (exit $LASTEXITCODE)." }

$setup = Join-Path $distDir "UV-VIS_meas-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "Setup file not found: $setup" }

Invoke-Sign $setup

# ---------------------------------------------------------------- report
$size = (Get-Item $setup).Length
Write-Host ''
Write-Host 'Installer built:' -ForegroundColor Green
Write-Host ("  {0}  ({1:N0} bytes)" -f $setup, $size)
Write-Host ("  version : {0}" -f $version)
if ($signingRequested) { Write-Host '  signed  : yes' }
else {
    Write-Host '  signed  : no - Windows will show a SmartScreen warning to the first users.' -ForegroundColor Yellow
    Write-Host '            Pass -CertificateFile or -CertificateThumbprint to sign it.' -ForegroundColor Yellow
}
