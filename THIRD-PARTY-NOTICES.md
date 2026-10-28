# 외부 구성 요소 고지 / Third-party notices

UV-VIS meas — Copyright (C) 2026 Myungsam

이 프로그램 자체는 [MIT 라이선스](LICENSE)로 공개된 오픈소스입니다. 아래 구성 요소는 MIT 라이선스의
적용을 받지 않으며 각자의 사용권 조건을 따릅니다. / The program itself is open source under the
[MIT License](LICENSE); the components below are not covered by it.

이 프로그램이 사용하는 구성 요소 / Components the program uses:

## OmniDriver (NETOmniDriver-NET40, OmniDriver32)

- 공급자 / Supplier: Ocean Optics, Inc. (Ocean Insight)
- **이 프로그램과 함께 배포되지 않습니다.** 사용자가 OmniDriver를 직접 설치해야 하며,
  프로그램은 실행할 때 `OOI_HOME` 폴더에서 어셈블리를 찾아 불러옵니다.
  / **Not redistributed with this program.** It is installed separately by the user and
  loaded from `OOI_HOME` at run time.
- 사용 조건은 Ocean Optics가 OmniDriver와 함께 제공하는 사용권 계약을 따릅니다.
  / Its use is governed by the licence agreement Ocean Optics supplies with OmniDriver.

## Microsoft .NET Framework 4.8

- 공급자 / Supplier: Microsoft Corporation
- Windows 10 / 11에 기본 포함되어 있으며 이 프로그램과 함께 배포되지 않습니다.
  / Part of Windows 10 / 11; not redistributed with this program.
- 그래프에 쓰는 `System.Windows.Forms.DataVisualization` 어셈블리도 .NET Framework의
  일부입니다. / The charting assembly `System.Windows.Forms.DataVisualization` is part of
  the .NET Framework as well.

## 설치 프로그램 / Installer

- Inno Setup — Copyright (C) 1997-2026 Jordan Russell, portions Copyright (C) 2000-2026
  Martijn Laan. 설치 프로그램을 만드는 데에만 사용되며, 생성된 설치 파일의 배포에는
  제한이 없습니다. / Used only to build the installer; the resulting setup may be
  distributed freely.

## 이 프로그램이 만드는 파일 / Files this program writes

측정 데이터(CSV), 측정 방법 파일(`.uvmethod`), 보고서(PNG/PDF)는 사용자의 것입니다.
/ Your measurement data, method files and reports are yours.
