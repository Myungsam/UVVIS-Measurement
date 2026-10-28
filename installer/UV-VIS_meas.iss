; Inno Setup script for UV-VIS meas.
;
; Build it with installer\build-installer.ps1 - that script stages the files,
; reads the version out of the built EXE and passes it in as AppVersion, so the
; installer can never disagree with the program it installs.
;
;   ISCC.exe /DAppVersion=2.3.0 /DStageDir=...\staging UV-VIS_meas.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef StageDir
  #define StageDir "staging"
#endif

#define AppName "UV-VIS meas"
#define AppPublisher "Myungsam"
#define AppExeName "UV-VIS_meas.exe"
#define AppCopyright "Copyright (C) 2026 Myungsam"

[Setup]
AppId={{B6E2F4A7-3C58-4D91-9E06-2A7C5F81D34B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright={#AppCopyright}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoCopyright={#AppCopyright}
VersionInfoDescription={#AppName} Setup

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}

; The program is a 32-bit process because the OmniDriver .NET assembly is a
; mixed-mode x86 binary. It still runs on 64-bit Windows, so no architecture is
; excluded here; it simply installs into the 32-bit program folder.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
; A lab PC often has no administrator password, so a per-user install is the
; default and installing for all users is offered rather than required.

OutputDir=..\dist
OutputBaseFilename=UV-VIS_meas-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
; Explicit: the default is "auto", which hides the folder page once a previous
; install is on the machine. An operator upgrading a lab PC should still be able
; to see and change where the program goes.
DisableDirPage=no
ShowLanguageDialog=auto
AllowNoIcons=yes

[Languages]
; The program is open source under the MIT License. The Korean page shows a reference
; translation followed by the English original, which is the text that applies.
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"; LicenseFile: "LICENSE-ko.txt"
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "..\LICENSE"

[CustomMessages]
korean.DesktopIcon=바탕화면에 바로가기 만들기
english.DesktopIcon=Create a desktop shortcut
korean.LaunchApp={#AppName} 실행
english.LaunchApp=Launch {#AppName}
korean.ManualLink=사용 설명서
english.ManualLink=User manual
korean.NoDriverTitle=OmniDriver를 찾지 못했습니다
english.NoDriverTitle=OmniDriver was not found
korean.NoDriver=이 프로그램은 분광기와 통신하기 위해 Ocean Optics OmniDriver가 필요합니다.%n%n설치는 계속할 수 있지만, OmniDriver를 설치하기 전에는 분광기를 사용할 수 없습니다.%n(장비 없이 화면만 보려면 --demo 옵션으로 실행하세요.)
english.NoDriver=This program needs Ocean Optics OmniDriver to talk to a spectrometer.%n%nSetup can continue, but no spectrometer will work until OmniDriver is installed.%n(Run with --demo to see the screens without hardware.)
korean.RemoveSettings=측정 설정과 오류 기록도 삭제할까요?%n%n(%1)%n%n저장한 측정 데이터와 측정 방법 파일은 이 폴더에 들어 있지 않으므로 삭제되지 않습니다.
english.RemoveSettings=Also delete the saved settings and the error log?%n%n(%1)%n%nYour measurement data and method files are not kept there and are not deleted.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#StageDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\UV-VIS_meas.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\사용법.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "{#StageDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\docs\README.md"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "{#StageDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE-ko.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:ManualLink}"; Filename: "{app}\docs\README.md"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[Code]
// OmniDriver is installed separately, so Setup only checks whether it is there and
// says what is missing. It never blocks the install: the program has a demo mode,
// and the driver is often installed after the software on a new PC.
function OmniDriverFound(): Boolean;
var
  Home: String;
begin
  Result := False;

  Home := GetEnv('OOI_HOME');
  if (Home <> '') and FileExists(AddBackslash(Home) + 'NETOmniDriver-NET40.dll') then
  begin
    Result := True;
    Exit;
  end;

  if FileExists(ExpandConstant('{commonpf32}\Ocean Optics\OmniDriverSPAM\OOI_HOME\NETOmniDriver-NET40.dll')) or
     FileExists(ExpandConstant('{commonpf32}\Ocean Optics\OmniDriver\OOI_HOME\NETOmniDriver-NET40.dll')) then
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not OmniDriverFound()) and (not WizardSilent()) then
    MsgBox(CustomMessage('NoDriver'), mbInformation, MB_OK);
end;

// Settings and the crash log live in %LOCALAPPDATA%, outside the program folder, so
// uninstalling leaves them behind unless the user asks for them to go. Measurement
// data is saved wherever the operator chose and is never touched.
// (Pascal { } comments end at the first closing brace, so constant names like
//  {app} must not appear inside one.)
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  DataDir := ExpandConstant('{localappdata}\UV-VIS_meas');
  if not DirExists(DataDir) then
    Exit;

  if UninstallSilent() then
    Exit;

  if MsgBox(FmtMessage(CustomMessage('RemoveSettings'), [DataDir]),
            mbConfirmation, MB_YESNO) = IDYES then
    DelTree(DataDir, True, True, True);
end;
