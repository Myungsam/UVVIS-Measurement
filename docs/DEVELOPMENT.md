# 개발자 안내

소스에서 직접 빌드하거나 프로그램을 고치려는 분을 위한 문서입니다.
프로그램을 **설치해서 쓰기만** 하신다면 [README](../README.md)와 [사용 설명서](USER_GUIDE.md)를 보세요.

## 개발 환경

| 항목 | 내용 |
|---|---|
| OS | Windows 10 / 11 |
| 빌드 도구 | Visual Studio 2022 또는 Build Tools (MSBuild), .NET Framework 4.8 |
| 언어 | C# 7.3, WinForms |
| 플랫폼 | **x86 전용**. OmniDriver의 .NET 어셈블리가 x86 전용 혼합 모드(C++/CLI) 바이너리이기 때문입니다. AnyCPU/x64로 빌드하면 `BadImageFormatException`이 납니다 |
| 드라이버 | Ocean Optics OmniDriver (`NETOmniDriver-NET40.dll`). **빌드할 때도 필요합니다** |
| 설치 프로그램 | [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install -e --id JRSoftware.InnoSetup`) |

OmniDriver와 공급사 문서는 이 저장소에 들어 있지 않습니다.

## 빌드

```powershell
cd UV-VIS_measurement_code
.\build.ps1                                   # Release x86 -> bin\Release\UV-VIS_meas.exe
.\build.ps1 -Configuration Debug
.\build.ps1 -OmniDriverHome "C:\path\to\OOI_HOME"   # OmniDriver를 자동으로 못 찾을 때
```

빌드한 프로그램을 장비 없이 실행하려면:

```powershell
.\bin\Release\UV-VIS_meas.exe --demo
```

## 테스트

```powershell
dotnet test tests\UvVisMeasurement.Tests
```

흡광도·드리프트 보정·리샘플링 계산, CSV 형식, 설정 저장, 가상 분광기, 취득 엔진, 한국어 번역 누락 여부를 확인합니다.
테스트 프로젝트는 OmniDriver 없이 빌드됩니다.

## 실행 폴더 만들기와 설치 프로그램 빌드

```powershell
cd UV-VIS_measurement_code
.\publish.ps1                       # 빌드 + ..\UV-VIS_meas\ 폴더와 바로가기

cd ..\installer
.\build-installer.ps1               # -> dist\UV-VIS_meas-Setup-<버전>.exe
.\build-installer.ps1 -CertificateThumbprint 1A2B3C...   # 코드 서명할 때
```

- 설치 파일의 버전은 빌드된 EXE에서 읽습니다. 버전은 `Properties/AssemblyInfo.cs` 한 곳에서만 바꿉니다.
- 서명은 선택입니다. 인증서가 있으면 EXE와 설치 파일 둘 다 서명하고 타임스탬프를 찍습니다.
- 릴리스 절차(브랜치, 버전, 체크섬, 태그, GitHub Release)는 [AGENTS.md](../AGENTS.md)의 *RELEASE PROCEDURE*에 있습니다.

## 화면 문구와 번역

프로그램 화면은 한국어와 영어를 지원합니다. 코드에서는 `L.T("key", "English")`로 영어 문구를 쓰고,
한국어는 `UV-VIS_measurement_code/Localization/Strings_ko.txt`(`key = 문구`)에 있습니다.
키가 빠지거나 쓰이지 않는 키가 남으면 테스트가 실패합니다.
CSV의 열 이름과 `#` 헤더는 다른 프로그램이 읽는 형식이므로 언어와 관계없이 영어로 둡니다.

문서는 한국어(`README.md`, `docs/USER_GUIDE.md`)가 원본이고, 영어·일본어·중국어(간체) 번역이 같은 폴더에
`.en.md`, `.ja.md`, `.zh-CN.md`로 있습니다. 원본을 고치면 번역도 함께 고쳐 주세요.

## 소스 구조

| 폴더 | 내용 |
|---|---|
| `UV-VIS_measurement_code/Driver` | OmniDriver 탐색, 장비 기능 조회, 측정 세션, 취득 스레드, 가상 분광기 |
| `UV-VIS_measurement_code/Analysis` | 흡광도·드리프트 보정 계산, 레퍼런스 저장, 평활화·기준선·피크, Beer-Lambert |
| `UV-VIS_measurement_code/Io` | CSV 저장·불러오기, 측정 방법 파일, 측정 중 디스크 기록, PNG/PDF 보고서, 사용자 설정 |
| `UV-VIS_measurement_code/Ui` | WinForms 화면, 스펙트럼 그래프, 2D 맵, 하드웨어 설정 |
| `UV-VIS_measurement_code/Localization` | 한국어 / 영어 화면 문구 |
| `tests/UvVisMeasurement.Tests` | 하드웨어 없이 도는 단위·통합 테스트 |
| `installer` | Inno Setup 스크립트, 빌드·서명 스크립트, 라이선스 한국어 참고 번역 |

설계 이유와 드라이버의 특이 동작은 [참고 매뉴얼](README.md) 3·9장과 각 폴더의 `AGENTS.md`에 있습니다.
남은 작업은 [docs/TODO.md](TODO.md)에 있습니다.
