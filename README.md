# UV-VIS meas

Ocean Optics / Ocean Insight 분광기를 위한 C# WinForms 기반 UV-VIS 측정 프로그램입니다.
OmniDriver가 인식하는 장비를 탐색하고, 스펙트럼 측정, dark/white reference 기반 흡광도 계산,
두 분광기를 이용한 실시간 광원 drift 보정, 시간 누적 데이터 시각화 및 CSV 저장을 지원합니다.

## 요구 사항

- Windows 10 또는 Windows 11
- .NET Framework 4.8
- Ocean Optics OmniDriver 및 `NETOmniDriver-NET40.dll`
- x86 빌드 환경

OmniDriver의 .NET 어셈블리는 x86 전용 mixed-mode 바이너리이므로 이 프로젝트는 x86으로만
빌드 및 실행해야 합니다. OmniDriver와 관련 공급사 문서는 이 저장소에 포함하지 않습니다.

## 빌드

```powershell
cd UV-VIS_measurement_code
.\build.ps1
```

OmniDriver 설치 위치를 자동으로 찾지 못하는 경우 다음과 같이 지정할 수 있습니다.

```powershell
.\build.ps1 -OmniDriverHome "C:\path\to\OOI_HOME"
```

Release 실행 파일은 `UV-VIS_measurement_code\bin\Release\UV-VIS_meas.exe`에 생성됩니다.

## 배포 (설치 프로그램 만들기)

```powershell
cd installer
.\build-installer.ps1
```

`dist\UV-VIS_meas-Setup-<버전>.exe` 가 만들어집니다. 버전은 빌드된 실행 파일에서 읽으므로
프로그램과 설치 파일의 버전이 어긋날 수 없습니다. [Inno Setup 6](https://jrsoftware.org/isinfo.php)
이 필요합니다(`winget install -e --id JRSoftware.InnoSetup`).

설치 프로그램은 한국어/영어를 모두 지원하며, 기본적으로 **현재 사용자용**으로 설치되어
관리자 권한이 필요 없습니다(설치 시작 시 모든 사용자용도 고를 수 있습니다). 시작 메뉴 항목,
선택 가능한 바탕화면 바로가기, 제거 기능, 라이선스(MIT) 표시, OmniDriver 설치 여부 확인이 들어 있습니다.

배포 파일은 **코드 서명 없이** 만듭니다. 그래서 처음 실행할 때 Windows가 "Windows의 PC 보호"
경고를 띄울 수 있습니다. **추가 정보 → 실행**을 누르면 설치가 진행됩니다. 소스가 모두 공개돼 있으니
의심스러우면 직접 빌드해서 쓰셔도 됩니다.

나중에 코드 서명 인증서를 쓰게 되면 같은 스크립트에 지문만 넘기면 됩니다.

```powershell
.\build-installer.ps1 -CertificateThumbprint 1A2B3C...
```

외부 구성 요소 고지는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) 에 있습니다.

## 화면 구성

메뉴(파일 / 측정 / 보기 / 도구 / 도움말)와 아이콘 툴바에서 자주 쓰는 동작을 바로 실행할 수 있습니다.
왼쪽 설정 패널 위에는 **Dark → White → 측정** 순서를 보여 주는 단계 표시가 있고, 각 단계를 눌러
그 동작을 바로 실행할 수 있습니다. 설정 패널은 `F9` 로 접었다 펼 수 있습니다.

| 단축키 | 동작 | | 단축키 | 동작 |
|---|---|---|---|---|
| `F5` | 측정 시작 / 중지 | | `Ctrl+S` | 스펙트럼 저장 |
| `Ctrl+D` | Dark 측정 | | `Ctrl+Shift+S` | 시간별 스펙트럼 저장 |
| `Ctrl+W` | White 측정 | | `Ctrl+R` | 시간별 데이터 기록 |
| `F9` | 설정 패널 접기/펴기 | | `Ctrl+1`~`Ctrl+6` | 화면 탭 이동 |
| `F1` | 사용 설명서 | | | |

## 분석 기능

- 표시 방식 전환: 흡광도(OD) / 투과율(%T) / 원시 counts
- 평활화(Savitzky-Golay), 기준선 보정(0점·두 점 직선·아래 포락선), 피크 찾기와 표식
- 저장한 CSV 스펙트럼 겹쳐 보기, 커서 위치 값 표시
- Beer-Lambert 몰흡광계수 계산 (농도·셀 두께는 단위를 함께 입력 가능)
- 측정 방법 파일(`.uvmethod`) 저장/불러오기 — Dark/White는 담지 않습니다
- 측정 중 디스크 기록으로 비정상 종료에도 데이터 보존
- 그래프 PNG 저장, 측정 조건이 들어간 A4 PDF 보고서 (외부 라이브러리 없음)

## 한국어 / English

처음 실행할 때 Windows 표시 언어를 따르고, **도구 > 언어 / Language** 에서 바꿀 수 있습니다.
선택한 언어는 저장되어 다음 실행에도 유지됩니다. 번역은 실행 파일에 포함된
`UV-VIS_measurement_code/Localization/Strings_ko.txt` 한 곳에 있어 코드를 고치지 않고 수정할 수 있습니다.
CSV 파일의 열 이름과 헤더는 기계가 읽는 형식이므로 언어와 상관없이 영어로 유지됩니다.

## 데모 모드 (장비 없이 실행)

```powershell
.\bin\Release\UV-VIS_meas.exe --demo
```

가상 분광기 2대(Flame-S / Ocean FX 모사)로 실행합니다. OmniDriver가 설치되지 않은 PC에서도 동작하며,
OmniDriver를 찾지 못했을 때 뜨는 대화상자에서 `No`를 눌러도 데모 모드로 시작합니다.
창 제목에 `[DEMO MODE]`가 표시됩니다. 자세한 내용은 [사용 매뉴얼](docs/README.md) 1장을 참고하세요.

## 테스트

```powershell
dotnet test tests\UvVisMeasurement.Tests
```

흡광도·드리프트 보정·리샘플링 계산, CSV 형식, 설정 저장, 가상 분광기, 취득 엔진,
한국어 번역 누락 여부를 검증합니다.
테스트 프로젝트는 OmniDriver 없이 빌드됩니다.

## 문서

설치, 장비 설정, 측정 흐름, 흡광도 계산, dual-beam drift 보정, UI 사용법 및 CSV 형식은
[상세 사용 매뉴얼](docs/README.md)을 참고하세요.

## 주요 소스 구조

- `UV-VIS_measurement_code/Driver`: OmniDriver 탐색, 장비 기능 조회, 측정 세션 및 수집 엔진
- `UV-VIS_measurement_code/Analysis`: reference 관리, 흡광도 및 drift 보정 계산, 시간 누적 데이터
- `UV-VIS_measurement_code/Ui`: WinForms 화면, 스펙트럼 차트, heatmap, 하드웨어 설정
- `UV-VIS_measurement_code/Io`: 스펙트럼 및 시간 누적 데이터 CSV 내보내기, 사용자 설정 저장
- `UV-VIS_measurement_code/Localization`: 한국어 / 영어 화면 문자열
- `tests/UvVisMeasurement.Tests`: 하드웨어 없이 실행되는 단위·통합 테스트
- `installer`: 설치 프로그램 스크립트(Inno Setup), 빌드·서명 스크립트, 라이선스 한국어 참고 번역

## 주의 사항

이 저장소에는 테스트 장비가 필요한 하드웨어 통합 코드가 포함되어 있습니다. 장비별 지원 기능과
허용 범위는 실행 시 OmniDriver에서 읽으며, 실제 측정 전 연결 장비의 적분 시간 및 trigger 설정을
확인해야 합니다.

측정 결과의 정확도는 분광기 상태, 광원, 레퍼런스 측정과 측정 조건에 따라 달라집니다. 이 프로그램은
실험실 측정용이며 의료 진단 용도로 만들어지지 않았습니다.

## 라이선스

[MIT 라이선스](LICENSE)로 공개합니다. 설치 프로그램에는 한국어 참고 번역(`installer/LICENSE-ko.txt`)이
함께 들어가며, 법적 효력은 영문 원문에 있습니다.

Ocean Optics OmniDriver는 이 라이선스에 포함되지 않으며 함께 배포하지도 않습니다. 사용자가 Ocean Optics에서
받아 직접 설치해야 합니다. 자세한 내용은 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)를 참고하세요.
