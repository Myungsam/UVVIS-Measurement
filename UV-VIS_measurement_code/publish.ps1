<#
.SYNOPSIS
    Builds the UV-VIS meas application and stages a double-clickable copy.

.DESCRIPTION
    Runs build.ps1, copies the Release output into a plain folder next to the
    source tree, and creates shortcuts that launch it. Nothing else has to be
    installed: .NET Framework 4.8 ships with Windows 10/11, and the OmniDriver
    assemblies are loaded from OOI_HOME at run time rather than copied here.

.PARAMETER TargetDir
    Where the runnable copy goes. Defaults to "<repo>\UV-VIS_meas".

.PARAMETER OmniDriverHome
    Folder holding NETOmniDriver-NET40.dll, if it cannot be found automatically.

.PARAMETER NoDesktopShortcut
    Skips creating the desktop shortcut (the folder shortcut is still created).

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -NoDesktopShortcut
#>
[CmdletBinding()]
param(
    [string]$TargetDir,
    [string]$OmniDriverHome,
    [switch]$NoDesktopShortcut
)

$ErrorActionPreference = 'Stop'
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoDir = Split-Path -Parent $projectDir

if (-not $TargetDir) { $TargetDir = Join-Path $repoDir 'UV-VIS_meas' }

$shortcutName = 'UV-VIS meas.lnk'
$description = 'Ocean Optics 분광기 UV-VIS 스펙트럼 측정 프로그램'

# ---------------------------------------------------------------- build
$buildArgs = @{ Configuration = 'Release' }
if ($OmniDriverHome) { $buildArgs['OmniDriverHome'] = $OmniDriverHome }
& (Join-Path $projectDir 'build.ps1') @buildArgs

$releaseDir = Join-Path $projectDir 'bin\Release'
$sourceExe = Join-Path $releaseDir 'UV-VIS_meas.exe'
if (-not (Test-Path $sourceExe)) { throw "Build output not found: $sourceExe" }

# ---------------------------------------------------------------- stage
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$targetExe = Join-Path $TargetDir 'UV-VIS_meas.exe'
if (Test-Path $targetExe) {
    # A running instance holds a lock; say so plainly instead of failing on
    # an "access is denied" from Copy-Item.
    try { [System.IO.File]::OpenWrite($targetExe).Close() }
    catch { throw "'$targetExe' is in use. Close the running program and try again." }
}

Copy-Item $sourceExe $TargetDir -Force
Copy-Item (Join-Path $releaseDir 'UV-VIS_meas.exe.config') $TargetDir -Force

$readme = Join-Path $TargetDir '사용법.txt'
@"
UV-VIS meas
===========

실행 방법
---------
UV-VIS_meas.exe 를 더블클릭하면 바로 실행됩니다.
(바탕화면 / 상위 폴더의 "UV-VIS meas" 바로가기도 같은 프로그램입니다.)

필요 조건
---------
1. Windows 10 / 11  - .NET Framework 4.8 이 기본 포함되어 있어 별도 설치가 필요 없습니다.
2. OmniDriver 설치  - NETOmniDriver-NET40.dll 이 있는 OOI_HOME 폴더가 필요합니다.
                      보통 C:\Program Files (x86)\Ocean Optics\OmniDriverSPAM\OOI_HOME
                      자동으로 찾지 못하면 실행 시 폴더 선택 창이 뜨며,
                      한 번 지정하면 다음 실행부터 기억합니다.
3. Ocean Optics 분광기를 USB 로 연결한 뒤 실행하세요.
                      (Ocean FX, Flame-S 등 OmniDriver 가 인식하는 장비면 됩니다.)

주의
----
이 프로그램은 x86(32비트) 전용입니다. OmniDriver .NET 어셈블리가
32비트 전용 혼합 모드 바이너리이기 때문이며, 64비트로 실행하면
BadImageFormatException 이 발생합니다.

문제가 생기면
-------------
오류 기록: %LOCALAPPDATA%\UV-VIS_meas\error.log
자세한 설명은 docs 폴더의 README.md (사용 매뉴얼) 를 참고하세요.
"@ | Out-File -LiteralPath $readme -Encoding utf8

# ---------------------------------------------------------------- shortcuts
function New-AppShortcut([string]$path) {
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($path)
    $link.TargetPath = $targetExe
    $link.WorkingDirectory = $TargetDir
    $link.IconLocation = "$targetExe,0"
    $link.Description = $description
    $link.Save()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
}

$folderShortcut = Join-Path $repoDir $shortcutName
New-AppShortcut $folderShortcut

$desktopShortcut = $null
if (-not $NoDesktopShortcut) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    if ($desktop -and (Test-Path $desktop)) {
        $desktopShortcut = Join-Path $desktop $shortcutName
        New-AppShortcut $desktopShortcut
    }
}

# ---------------------------------------------------------------- report
Write-Host ''
Write-Host 'Published:' -ForegroundColor Green
Get-ChildItem $TargetDir | ForEach-Object { '  {0,-34} {1,10:N0} bytes' -f $_.Name, $_.Length }
Write-Host ''
Write-Host "  folder  : $TargetDir"
Write-Host "  shortcut: $folderShortcut"
if ($desktopShortcut) { Write-Host "  shortcut: $desktopShortcut" }
