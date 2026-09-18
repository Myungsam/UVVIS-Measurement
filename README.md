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

## 문서

설치, 장비 설정, 측정 흐름, 흡광도 계산, dual-beam drift 보정, UI 사용법 및 CSV 형식은
[상세 사용 매뉴얼](docs/README.md)을 참고하세요.

## 주요 소스 구조

- `UV-VIS_measurement_code/Driver`: OmniDriver 탐색, 장비 기능 조회, 측정 세션 및 수집 엔진
- `UV-VIS_measurement_code/Analysis`: reference 관리, 흡광도 및 drift 보정 계산, 시간 누적 데이터
- `UV-VIS_measurement_code/Ui`: WinForms 화면, 스펙트럼 차트, heatmap, 하드웨어 설정
- `UV-VIS_measurement_code/Io`: 스펙트럼 및 시간 누적 데이터 CSV 내보내기

## 주의 사항

이 저장소에는 테스트 장비가 필요한 하드웨어 통합 코드가 포함되어 있습니다. 장비별 지원 기능과
허용 범위는 실행 시 OmniDriver에서 읽으며, 실제 측정 전 연결 장비의 적분 시간 및 trigger 설정을
확인해야 합니다.
