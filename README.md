# UV-VIS meas

**한국어** | [English](README.en.md) | [日本語](README.ja.md) | [简体中文](README.zh-CN.md)

[![Release](https://img.shields.io/github/v/release/Myungsam/UVVIS-Measurement)](https://github.com/Myungsam/UVVIS-Measurement/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Ocean Optics / Ocean Insight 분광기로 **UV-VIS 흡광도와 형광 스펙트럼을 측정**하는 Windows 프로그램입니다.
Dark·White 기준을 찍고 시료를 넣으면 흡광도가 바로 그려지고, 측정값은 CSV로 저장됩니다.
무료로 쓸 수 있는 오픈소스(MIT)입니다.

![흡광도 측정 화면](docs/images/guide/08_analysis.png)

## 주요 기능

- **흡광도·투과율·형광(원시 세기) 측정**, Dark·White 기준 관리를 단계 표시로 안내
- 평활화, 기준선 보정, 피크 찾기, 커서 위치 값, **Beer-Lambert 몰흡광계수 계산**
- **시간에 따른 흡광도 변화** 기록과 2D 맵, 측정 중 디스크에 바로 기록해 비정상 종료에도 데이터 보존
- 분광기 두 대로 광원 흔들림을 실시간 보정(dual-beam)
- CSV 저장(측정 조건 포함), 저장한 스펙트럼 겹쳐 보기, 그래프 PNG, A4 PDF 측정 보고서
- 측정 조건을 파일로 저장해 같은 조건으로 반복 측정
- 한국어 / English 화면, 장비 없이 연습하는 **데모 모드**

## 설치

### 필요한 것

| 항목 | 내용 |
|---|---|
| PC | Windows 10 / 11 (.NET Framework 4.8은 Windows에 기본 포함) |
| 드라이버 | **Ocean Optics OmniDriver**. 이 프로그램과 함께 배포되지 않으므로 먼저 설치해야 합니다 |
| 분광기 | OmniDriver가 인식하는 Ocean Optics 분광기, USB 연결 |

### 설치 순서

1. **OmniDriver를 설치**하고 분광기를 USB로 연결합니다.
2. [최신 릴리스](https://github.com/Myungsam/UVVIS-Measurement/releases/latest)에서 `UV-VIS_meas-Setup-<버전>.exe`를 내려받습니다.
3. 설치 파일을 실행합니다. **"Windows의 PC 보호"** 창이 뜨면 **추가 정보 → 실행**을 누르세요.
   코드 서명을 하지 않은 오픈소스 배포라서 나오는 경고입니다.
4. **현재 사용자용으로 설치**를 고르면 관리자 권한 없이 설치됩니다. 라이선스에 동의하고 **다음**을 누르면 끝납니다.
5. 시작 메뉴의 **UV-VIS meas**로 실행합니다.

받은 파일이 올바른지 확인하려면 PowerShell에서 아래 명령의 결과를 릴리스 페이지의 SHA-256 값과 비교하세요.

```powershell
Get-FileHash .\UV-VIS_meas-Setup-2.3.1.exe -Algorithm SHA256
```

화면과 함께 보려면 [사용 설명서 2장](docs/USER_GUIDE.md#2-설치)을 보세요.

### 업데이트와 제거

- **업데이트**: 새 버전의 설치 파일을 그대로 실행하면 덮어 설치됩니다. 측정 설정은 유지됩니다.
- **제거**: Windows 설정 → 앱, 또는 시작 메뉴의 **UV-VIS meas 제거**. 측정 데이터(CSV)는 지우지 않습니다.

## 빠른 시작 — 흡광도 측정

1. 분광기 표의 **사용** 칸에서 측정할 분광기를 고르고, 광원을 켭니다.
2. **측정 시작**(`F5`)을 누릅니다.
3. 광원 셔터를 닫거나 광로를 가리고 **Dark 측정**(`Ctrl+D`).
4. 용매만 담은 큐벳을 넣고 **White 측정**(`Ctrl+W`).
5. 시료 큐벳으로 바꾸면 **흡광도** 그래프가 그려집니다. **스펙트럼 저장**(`Ctrl+S`)으로 CSV를 저장합니다.

형광 측정, 시간별 측정, 분석 기능, 저장 형식은 [사용 설명서](docs/USER_GUIDE.md)에 순서대로 나와 있습니다.

## 장비 없이 체험하기 (데모 모드)

가상 분광기 두 대로 모든 기능을 연습할 수 있습니다. OmniDriver가 없어도 됩니다.

```powershell
& "$env:LOCALAPPDATA\Programs\UV-VIS meas\UV-VIS_meas.exe" --demo
```

OmniDriver를 찾지 못했다는 창에서 **아니요**를 눌러도 데모 모드로 시작합니다.

## 문서

| 문서 | 내용 |
|---|---|
| [사용 설명서](docs/USER_GUIDE.md) | 설치, 흡광도·형광·시간별 측정, 저장, 문제 해결을 화면과 함께 단계별로 |
| [참고 매뉴얼](docs/README.md) | 계산식의 근거, 하드웨어 설정, 저장 형식, 자세한 문제 해결 (프로그램에서 `F1`) |
| [변경 기록](CHANGELOG.md) | 버전별로 바뀐 내용 |
| [외부 구성 요소 고지](THIRD-PARTY-NOTICES.md) | OmniDriver 등 이 프로그램이 쓰는 구성 요소 |

## 확인된 환경

| 항목 | 내용 |
|---|---|
| 분광기 | Flame-S (USB2000+ 계열), OmniDriver 2.56 — 실장비 확인 |
| OS | Windows 11, x86 프로그램 |
| 아직 확인하지 못한 것 | 표준 시료로 흡광도 값 비교, 실제 형광 측정, Flame-S 외 모델, 실제 150% 배율 모니터 ([남은 작업](docs/TODO.md)) |

다른 Ocean Optics 모델도 OmniDriver가 인식하면 동작하도록 만들었습니다. 장비별 기능과 허용 범위는 실행할 때 드라이버에서 읽습니다.

## 데이터와 설정이 저장되는 곳

| 무엇 | 어디 |
|---|---|
| 측정 데이터(CSV), 보고서 | 화면의 **저장 폴더**에서 고른 곳 |
| 프로그램 설정 | `%LOCALAPPDATA%\UV-VIS_meas\settings.xml` |
| 오류 기록 | `%LOCALAPPDATA%\UV-VIS_meas\error.log` (문의할 때 함께 보내 주세요) |

## 주의 사항

측정 결과의 정확도는 분광기 상태, 광원, Dark·White 기준과 측정 조건에 따라 달라집니다.
이 프로그램은 실험실 측정용이며 의료 진단 용도로 만들어지지 않았습니다.

## 라이선스

[MIT 라이선스](LICENSE)로 공개합니다. 설치 프로그램에는 한국어 참고 번역이 함께 들어가며, 법적 효력은 영문 원문에 있습니다.
Ocean Optics OmniDriver는 이 라이선스에 포함되지 않으며 함께 배포하지도 않습니다([외부 구성 요소 고지](THIRD-PARTY-NOTICES.md)).

## 개발에 참여하려면

소스에서 빌드하는 방법, 테스트, 설치 프로그램 만들기, 소스 구조는 [개발자 안내](docs/DEVELOPMENT.md)에 있습니다.
