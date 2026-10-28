# UV-VIS meas — 사용 매뉴얼 (v2.3)

**OmniDriver가 지원하는 Ocean Optics / Ocean Insight 분광기** 전반을 위한 스펙트럼
측정 프로그램입니다. OmniDriver의 .NET 어셈블리(`NETOmniDriver-NET40.dll`)를 통해
`openAllSpectrometers()`로 연결된 장비를 모두 열고, 픽셀 수·파장 격자·적분시간 한계·
GPIO·트리거 지연 지원 여부를 **장비마다 드라이버에서 읽어** 적용합니다. 특정 모델을
가정한 상수는 코드에 없습니다. C# / WinForms / .NET Framework 4.8 기반입니다.

검증 환경 (OmniDriver 2.56, 2대 동시 연결):

| Index | 모델 | S/N | 픽셀 | 파장 | 적분시간 | 트리거 지연 |
|---|---|---|---|---|---|---|
| 0 | Flame-S (`getName()`은 `USB2000+`) | `FLMS-EXAMPLE` | 2048 | 179.9–870.7 nm | 1–65535 ms | 0.5 µs/count, ≤32767.5 µs |
| 1 | Ocean FX (`getFirmwareModel()`은 `Flame-X-F`) | `OFX-EXAMPLE` | 2136 | 316.0–1031.3 nm | 0.01–10000 ms | 1.0 µs/count, ≤1000000 µs |

---

## 0. 폴더 구성

| 위치 | 내용 |
|---|---|
| `UV-VIS_measurement_code\` | 소스 코드, `.csproj`, `build.ps1`, `publish.ps1` |
| `UV-VIS_meas\` | 더블클릭 실행용 배포본 (`UV-VIS_meas.exe`, `.exe.config`, `사용법.txt`) |
| `UV-VIS meas.lnk` | 배포본 바로가기 (바탕화면에도 같은 이름으로 생성) |
| `docs\` | 이 매뉴얼(`README.md`)과 `OmniDriver_Programming_Manual.pdf` |

프로그램이 사용자별로 기억하는 파일(직접 지정한 `OOI_HOME`, 오류 로그, 사용자 설정
`settings.xml`)은 `%LOCALAPPDATA%\UV-VIS_meas\`에 저장됩니다.

---

## 1. 빌드 및 실행

아래 명령은 모두 `UV-VIS_measurement_code` 폴더에서 실행합니다.

```powershell
.\build.ps1                      # Release, x86
.\build.ps1 -Configuration Debug
.\build.ps1 -OmniDriverHome "D:\경로\OOI_HOME"   # 자동 탐색 실패 시
```

실행 파일: `bin\Release\UV-VIS_meas.exe`

### 데모 모드 (장비 없이 실행)

```powershell
.\bin\Release\UV-VIS_meas.exe --demo
```

OmniDriver 대신 **가상 분광기 2대**로 실행합니다. OmniDriver가 설치되지 않은 PC에서도
동작하므로 시연, 교육, 화면 확인에 쓸 수 있습니다. OmniDriver를 찾지 못했을 때 뜨는
대화상자에서 `No`를 눌러도 데모 모드로 시작합니다. 창 제목에 `[DEMO MODE - simulated
spectrometers]`, 상태 표시줄에 `Simulator (demo mode)`가 표시되고, 오류 로그에도
`backend Simulator`가 기록되므로 실제 측정 데이터와 혼동되지 않습니다.

| Index | 가상 장비 | 역할 | 특징 |
|---|---|---|---|
| 0 | Flame-S (simulated), S/N `SIM-FLMS-0001` | 시료 빔 | 2048 px, 179–891 nm, 0.5 µs/count, 420·520 nm 흡수띠가 90초 주기로 증감 |
| 1 | Ocean FX (simulated), S/N `SIM-OFX-0002` | 기준 빔 | 2136 px, 316–1031 nm, 1 µs/count, Single-Shot 모드는 거부(회색 표시 확인용) |

- 두 장비는 같은 광원을 보며, 광원 세기가 몇 % 범위에서 천천히 흔들립니다.
  두 장비 모두 Dark/White를 찍으면 실시간 레퍼런스 보정(§6)이 이 흔들림을 지웁니다.
- **Dark 측정:** Hardware Settings 탭에서 **GPIO 0을 Output / HIGH**로 바꾸면 가상 셔터가
  닫혀 빛이 차단됩니다. 이 상태에서 Capture Dark, 다시 LOW로 바꾸면 빛이 돌아옵니다.
- Normal 이외의 트리거 모드는 트리거가 오지 않는 실제 장비처럼 타임아웃됩니다.
- 적분시간·평균·포화(65535 counts)·트리거 지연 환산은 실제 장비와 같은 규칙을 따릅니다.

### 사용자 설정 저장

프로그램을 닫을 때 다음 값을 `%LOCALAPPDATA%\UV-VIS_meas\settings.xml`에 저장하고
다음 실행 때 복원합니다.

- 저장 폴더, Sample / Concentration / Cell Thickness, Set Min Interval, Average
- Timed 탭의 trace 파장 목록, 마지막으로 보던 탭, 창 위치·크기·최대화 여부
- **분광기별** 적분시간과 트리거 지연 — 시리얼 번호 기준이므로 연결 순서가 바뀌어도
  같은 장비에 같은 값이 돌아갑니다. 지금 연결되지 않은 장비의 값도 지우지 않습니다.
  복원할 때 장비의 허용 범위로 다시 클램프합니다.

트리거 모드·GPIO(드라이버 확인 후에만 쓰는 값), Dark/White Reference(그 순간의 광원과
시료에만 유효), 축 범위(§7)는 **저장하지 않습니다.** 저장했던 창 위치가 현재 연결된
모니터 밖이면 기본 위치로 엽니다. 파일이 손상되면 기본값으로 시작하고 상태 표시줄에
알립니다.

### 테스트

저장소 루트에서:

```powershell
dotnet test tests\UvVisMeasurement.Tests
```

흡광도·드리프트 보정·리샘플링 계산, CSV 형식(InvariantCulture, F4/G8, BOM 없음,
CSV 값으로 흡광도 재계산), 설정 저장/복원, 가상 분광기, 취득 엔진(프레임 → White 캡처
→ 흡광도) 35개 항목을 검증합니다. 테스트 프로젝트는 OmniDriver 없이 빌드됩니다.

### 더블클릭 실행용 배포 (`publish.ps1`)

```powershell
.\publish.ps1                    # 빌드 + 배포 폴더 + 바로가기 생성
.\publish.ps1 -NoDesktopShortcut # 바탕화면 바로가기 생략
.\publish.ps1 -TargetDir "D:\UV-VIS_meas"
```

`publish.ps1`은 빌드 후 다음을 만듭니다.

| 위치 | 내용 |
|---|---|
| `..\UV-VIS_meas\` | `UV-VIS_meas.exe`, `.exe.config`, `사용법.txt`, `THIRD-PARTY-NOTICES.md`, `docs\README.md` |
| `..\UV-VIS meas.lnk` | 소스 폴더 옆 바로가기 |
| 바탕화면 `UV-VIS meas.lnk` | 바탕화면 바로가기 |

EXE 또는 바로가기를 **더블클릭하면 바로 실행**됩니다. .NET Framework 4.8은
Windows 10/11에 기본 포함되어 있어 별도 설치가 필요 없고, OmniDriver DLL은
복사하지 않고 실행 시 `OOI_HOME`에서 로드합니다(§2).

아이콘은 `Assets\AppIcon.ico`이며 `.csproj`의 `ApplicationIcon`으로 EXE에
포함됩니다. 창/작업 표시줄 아이콘은 `MainForm.ApplyWindowIcon()`이
`Icon.ExtractAssociatedIcon(Application.ExecutablePath)`으로 같은 리소스를
꺼내 쓰므로, 아이콘을 바꿀 때 수정할 곳은 `.ico` 파일 하나뿐입니다.
`.ico`의 16–128 px 프레임은 PNG가 아닌 비압축 DIB로 저장했습니다
— `System.Drawing.Icon.ToBitmap()`이 PNG 프레임을 디코딩하지 못하기 때문입니다.

> `publish.ps1`은 한글이 포함되어 있어 **UTF-8 BOM**으로 저장되어 있습니다.
> Windows PowerShell 5.1은 BOM이 없는 `.ps1`을 ANSI(949)로 읽어 한글이 깨집니다.

### 설치 프로그램 만들기 (`installer\build-installer.ps1`)

배포용으로 건네줄 때는 폴더를 복사하는 대신 **설치 파일 하나**를 만듭니다.

```powershell
cd ..\installer
.\build-installer.ps1
```

[Inno Setup 6](https://jrsoftware.org/isinfo.php)이 필요합니다
(`winget install -e --id JRSoftware.InnoSetup`). 스크립트는 다음 순서로 동작합니다.

1. `publish.ps1 -NoShortcuts`로 `installer\staging\`에 파일을 모읍니다.
2. 만들어진 EXE에서 **버전을 읽습니다**. 설치 파일 이름, 제어판의 "프로그램 및 기능"
   항목, 프로그램 자체의 버전이 어긋날 수 없게 하기 위해서입니다.
3. 인증서를 주면 EXE에 서명합니다.
4. `UV-VIS_meas.iss`를 컴파일해 `dist\UV-VIS_meas-Setup-<버전>.exe`를 만들고,
   설치 파일에도 서명합니다.

설치 프로그램이 하는 일:

| 항목 | 내용 |
|---|---|
| 언어 | 한국어 / English (설치 시작 시 선택) |
| 권한 | 기본은 **현재 사용자용**이라 관리자 권한이 필요 없습니다. 시작할 때 모든 사용자용도 고를 수 있습니다 |
| 기본 위치 | 사용자 설치는 `%LOCALAPPDATA%\Programs\UV-VIS meas` |
| 설치 내용 | 실행 파일, `.exe.config`, 사용 설명서, 라이선스(`LICENSE.txt`, 한국어 참고 번역 `LICENSE-ko.txt`), 외부 구성 요소 고지, `사용법.txt` |
| 바로가기 | 시작 메뉴(프로그램·사용 설명서·제거), 바탕화면(선택) |
| OmniDriver | 설치 후 드라이버가 없으면 **알려만 주고 설치는 계속합니다**. 데모 모드가 있고, 새 PC에서는 드라이버를 나중에 까는 일이 흔하기 때문입니다 |
| 제거 | 프로그램 폴더를 지우고, `%LOCALAPPDATA%\UV-VIS_meas`의 설정·오류 기록은 **지울지 물어봅니다**. 측정 데이터는 사용자가 정한 폴더에 있으므로 건드리지 않습니다 |

### 코드 서명

이 프로그램은 MIT 라이선스 오픈소스로 **서명 없이** 배포합니다. 서명하지 않은 설치 파일은
Windows SmartScreen이 "알 수 없는 게시자"로 경고하며, 사용자는 **추가 정보 → 실행**으로 진행합니다.

나중에 인증서를 쓰게 되면 스크립트에 넘기기만 하면 됩니다. 2023년 6월 이후 발급되는 인증서는 키가
USB 토큰이나 클라우드 HSM 안에 있어 `.pfx` 파일로 받을 수 없으므로, 보통 지문(`-CertificateThumbprint`)을 씁니다.

```powershell
.\build-installer.ps1 -CertificateThumbprint 1A2B3C...        # 인증서 저장소(토큰 포함)에 있는 경우
.\build-installer.ps1 -CertificateFile C:\keys\signing.pfx -CertificatePassword (Read-Host -AsSecureString)
```

서명은 항상 **타임스탬프**와 함께 합니다(`-TimestampUrl`, 기본값 있음). 타임스탬프가 없으면
인증서가 만료되는 순간 이미 배포된 파일의 서명까지 무효가 되기 때문입니다.

### 반드시 x86으로 빌드해야 합니다

`NETOmniDriver-NET40.dll`은 **x86 전용 혼합 모드(C++/CLI) 어셈블리**입니다
(`PEKind = X86`). AnyCPU 또는 x64로 빌드하면 실행 시 `BadImageFormatException`이
발생합니다. `.csproj`에서 `PlatformTarget`을 x86으로 고정해 두었습니다.

### .NET Framework 개발자 팩이 없는 경우

개발자 팩(타기팅 팩)이 설치되어 있지 않으면 MSBuild가 `MSB3644`로 실패합니다.
`.csproj`에 참조 어셈블리 경로를 프레임워크 디렉터리로 대체하는 폴백을 넣어
두었기 때문에, 런타임 + Build Tools만 있어도 빌드됩니다. 개발자 팩이 설치되어
있으면 해당 블록은 자동으로 건너뜁니다.

---

## 2. 라이브러리 경로 탐색

`Driver\OmniDriverLocator.cs`가 다음 순서로 `OOI_HOME`을 찾습니다.

1. 이전에 사용자가 직접 지정한 폴더 (`%LOCALAPPDATA%\UV-VIS_meas\ooi_home.txt`)
2. `OOI_HOME` 환경 변수
3. 레지스트리 `HKLM\SOFTWARE\Ocean Optics\<제품>\Location` + `\OOI_HOME`
   (32/64비트 뷰 모두 조회)
4. 기본 설치 경로 (`Program Files (x86)|Program Files` × `Ocean Optics|OceanOptics|Ocean Insight`
   × `OmniDriverSPAM|OmniDriver` × `OOI_HOME`)
5. `PATH` 항목 중 `OOI_HOME` / `OmniDriver`를 포함하는 경로
6. 실행 파일과 같은 폴더

모두 실패하면 **폴더 선택 대화상자**를 띄우고, 선택한 경로를 저장해 다음 실행부터
재사용합니다. 폴더를 찾으면 `SetDllDirectory` + `PATH` 선행 추가로 네이티브
`OmniDriver32.dll` 의존성을 해결하고, `AssemblyResolve` 훅으로 관리 어셈블리를
로드합니다. `.csproj`도 빌드 시점에 동일한 후보 경로를 탐색합니다.

---

## 3. 실제 API와 요청 사양의 차이

구현하면서 실제 어셈블리를 리플렉션으로 확인한 결과, 몇 가지를 조정했습니다.

| 요청 사양 | 실제 API | 처리 |
|---|---|---|
| `getFeatureControllerGPIO(i)` → 파일 경로 | `NETGPIO` **객체**를 반환 | 객체를 캐시해 재사용, 종료 시 `Dispose` |
| `setIntegrationTime_2` 등 `_2` 접미사 | LabVIEW의 오버로드 구분용. C#에서는 일반 오버로드 | `setIntegrationTime(i, 0, usec)` 형태의 채널 오버로드 사용 |
| `setExternalTriggerDelay(us)` | 인자는 **counts** (µs 아님) | `triggerDelayCountsToMicroseconds()`로 환산 계수를 읽어 µs→counts 변환, min/max/increment로 클램프, 실제 적용값을 UI·CSV에 표기 |
| `setMuxBit(pin, 0)` | 2번째 인자가 `short` | `(short)` 캐스팅 |
| — | `isFeatureSupportedGPIO/ExternalTriggerDelay(i)` | 매뉴얼 지시대로 지원 여부를 먼저 확인 (미지원이어도 컨트롤러 객체는 반환되므로 필수) |
| 분광기가 지원하는 트리거 모드 목록 조회 | **그런 API가 없음** (아래 참고) | 열 때 모드를 하나씩 제시해 드라이버가 받아들이는지 확인하고, 거부된 모드만 비활성 표시 |
| GPIO 채널별 Input/Output 지원 여부 조회 | **그런 API가 없음.** `getNumberOfPins()`, `getTotalGPIOBits()`, `getDirectionBits()`만 존재 | 드라이버가 노출한 핀은 양방향 가능으로, 핀 수를 넘는 비트는 사용 불가로 표시 |

### 트리거 모드 지원 여부는 SDK가 알려주지 않습니다

OmniDriver .NET 어셈블리(v2.56, 194개 공개 타입)를 전수 조회해 확인한 결과,
`setExternalTriggerMode()` / `getExternalTriggerMode()` 외에 **모드 목록이나 지원 여부를
알려주는 호출은 존재하지 않습니다**. `NETInternalTrigger`, `NETTriggerSource`는 내부 트리거
주기·소스를 다루는 별개 기능입니다.

남은 유일한 확인 수단은 "써 보고 되읽기"인데, **실측 결과 이것도 판별력이 없습니다.**
연결된 Flame-S(FLMS-EXAMPLE)에서:

```
set 0 -> readback 0   lastException=no exception
set 4 -> readback 4   lastException=no exception
set 7 -> readback 7   lastException=no exception   ← 7번 모드는 존재하지도 않음
```

드라이버는 **아무 값이나 그대로 되돌려 줍니다.** 그래서 이 장비에서는 5개 모드가 모두
"수락됨"으로 남고, Hardware Settings 탭은 그 사실을 그대로 적어 둡니다 —
지원 목록인 척하지 않습니다. 드라이버가 실제로 거부하는 장비에서는 해당 모드가
회색으로 비활성화됩니다. 모델별 지원 표를 코드에 넣지 않은 이유이기도 합니다.

**실측값** (`getFeatureController*` / `getMinimum·MaximumIntegrationTime` 직접 조회):

| | Ocean FX | Flame-S |
|---|---|---|
| GPIO | 8핀 | 8핀 |
| 트리거 지연 | 0–1,000,000 counts, **1 count = 1 µs** | 0–65,535 counts, **1 count = 0.5 µs** |
| 적분 시간 | 10 µs – 10 s | 1 ms – 65.535 s |
| 픽셀 | 2136 (dark 0) | 2048 (dark 16) |
| `getSpectrum()` 반환값 | 정수 counts | **소수점이 붙은 counts** |

`1 count = 1 µs`는 Ocean FX에만 해당하므로 코드에 상수로 넣지 않고,
장비마다 `triggerDelayCountsToMicroseconds()`로 읽어 씁니다.

**암전류 보정은 `setCorrectForElectricalDark`를 쓰지 않고 Capture Dark로 저장한
스펙트럼을 빼는 방식입니다.** Ocean FX는 dark pixel이 0개라 그 API 자체를 쓸 수 없고,
Flame-S는 16개가 있어 쓸 수는 있지만, 모든 모델에서 동일하게 동작하는 방식을
택했습니다. dark pixel 개수는 표의 모델 셀 툴팁과 상태 표시줄에 표시됩니다.

---

## 4. 측정 흐름

```
CreateWrapper → openAllSpectrometers()  →  N대
  └ 각 분광기: getWavelengths(i, 0), 픽셀 수, 적분시간 범위, GPIO/트리거지연 지원 여부
  └ 분광기 표에 index / model / integration / trigger delay / probe / reference 행 생성

설정 적용 (변경 시마다, 300 ms 디바운스)
  1. GPIO   : setMuxBit(pin,0) → setDirectionBit(pin,1) → setValueBit(pin,0|1)
              ※ Use로 선택된 분광기에 적용
              ※ 매뉴얼상 호출당 최대 200 ms 지연 → 값이 바뀐 경우에만 재호출
  2. Use로 선택된 그 분광기에만
       setExternalTriggerDelay(counts)          ← 그 행의 Trigger delay (us)
       setIntegrationTime → setScansToAverage   ← 그 행의 Integration time (ms)
       → setExternalTriggerMode → setUSBTimeout
       트리거 모드 변경 시 flushSpectrum()
     ※ 읽지 않는 분광기에까지 USB 왕복을 쓰면 설정 변경이 느려지므로 제외

측정 루프 (전용 스레드, Measurement 토글이 눌려 있는 동안 반복)
  getSpectrum(활성 index, 0) → isUSBTimeout / isSpectrumValid 확인
  → 그 분광기의 Dark·White Reference로 흡광도 계산 → UI로 전달
  Set Min Interval(ms)로 주기 하한 유지

종료
  NETGPIO.Dispose → NETExternalTriggerDelay.Dispose
  → NETWrapper.closeAllSpectrometers() → NETWrapper.Dispose()
```

OmniDriver는 **하나의 분광기를 여러 스레드에서 접근하는 것을 금지**합니다
(매뉴얼 8장). 따라서 모든 드라이버 호출은 전용 취득 스레드 한 곳에서만
일어나고, UI는 명령 큐로 요청을 보내고 `SynchronizationContext`로 결과를 받습니다.

한 프레임(`SpectrumFrame`)은 원시 counts뿐 아니라 그 시점에 유효한 Dark와
White Reference도 함께 담습니다. 그래서 Capture 버튼이 새로 측정하지 않고
화면에 떠 있는 데이터를 그대로 저장할 수 있고, 그래프에 세 스펙트럼을 겹쳐
그릴 수 있습니다.

---

## 5. 분광기 표

`Set parameters` 안의 표가 측정 대상과 빔 역할을 결정합니다.

| 열 | 의미 |
|---|---|
| **Use** | **측정할 분광기 1대**를 고릅니다. Capture Dark / Capture White Ref. / Clear도 이 분광기에 적용됩니다 |
| **Index** | `spectrometerIndex` (드라이버 값, 편집 불가) |
| **Spectrometer model** | `getName()` / `getFirmwareModel()` (편집 불가, 마우스를 올리면 전체 사양) |
| **Integration time (ms)** | 그 분광기의 적분시간. **그 장비의** `getMinimum/MaximumIntegrationTime()` 범위로 자동 클램프 |
| **Trigger delay (us)** | 그 분광기의 외부 트리거 지연. 미지원 장비는 `n/a`로 잠김 |
| **Probe** | 프로브 빔 담당. **여러 대 선택 가능** |
| **Reference** | 레퍼런스 빔 담당. 최대 1대, Probe가 1대일 때만 |
| **Corr.** | 실시간 레퍼런스 보정에 쓸 **2대**. 3대 이상일 때만 편집 가능 |

**Use는 항상 1개만 선택됩니다.** 한 행을 체크하면 나머지 행의 Use 체크박스는
회색으로 잠깁니다. 다른 분광기로 바꾸려면 현재 행의 체크를 먼저 해제하세요.

**Probe / Reference는 분광기 개수에 따라 달라집니다.**

| 인식 개수 | Probe / Reference 체크박스 |
|---|---|
| **1대** | **비활성화**(회색, 선택 불가). 빔을 나눌 상대가 없으므로 의미가 없습니다 |
| **2대 이상** | 활성화. 한쪽을 Probe로 체크하면 **Corr. 짝이 자동으로 Reference**가 됩니다(반대도 동일). 마지막 Probe 체크를 해제하면 Reference도 함께 풀립니다 |

**Probe를 2대 이상 체크하면** 시료를 여러 개 번갈아 재는 뜻이므로 램프를 감시할
분광기가 남지 않습니다. 이때 **Reference 열 전체가 회색으로 죽고** 기존 Reference
지정도 해제되며, 각 분광기는 **자기 자신의 dark / white reference로만** 흡광도를
계산합니다. 다시 Probe를 1대로 줄이면 Reference와 보정이 자동으로 돌아옵니다.

**Corr. 열 (보정에 쓸 2대 지정)**

| 인식 개수 | Corr. 열 |
|---|---|
| **2대 이하** | **비활성화**(회색). 고를 여지가 없으므로 **표시 전용**이며, Probe가 1대일 때만 자동으로 체크됩니다. **Probe를 2대로 만들면 두 체크가 스스로 풀리고**, 다시 1대로 줄이면 돌아옵니다 |
| **3대 이상** | 활성화. **2대까지만** 체크되고, 2대가 차면 나머지 행은 잠깁니다. 바꾸려면 한쪽을 먼저 해제하세요. Probe를 2대로 만들어도 **직접 고른 짝은 지우지 않습니다** — 다시 1대로 줄이면 그대로 씁니다 |

보정은 **Corr. 짝 = {Probe, Reference}** 일 때만 걸립니다. 예를 들어 Corr.가
`[0], [2]`인데 Reference가 `[1]`이면 체크박스 옆에 `Corr. must be [0] and [1]`이
표시되고 보정은 대기 상태가 됩니다.

표가 장비 수에 맞춰 세로로 늘어나므로 3대 이상이어도 모든 행이 한눈에 보입니다
(오른쪽 패널은 필요하면 스크롤됩니다).

- 측정 중에는 Use / Probe / Reference가 잠깁니다 — 도중에 바꾸면 저장해 둔
  Dark·White Reference가 조용히 무효가 되기 때문입니다. Integration과 Trigger delay는
  측정 중에도 바꿀 수 있고 즉시 반영됩니다.
- `Measuring` 표시는 입력이 아니라 **표에서 파생된 상태**입니다:
  1대면 `[0] single`, 2대면 `[0] probe` / `[1] reference`, Use 미선택이면 `none selected`.

### 서로 다른 모델을 섞어 연결한 경우

Model 열에는 **분광기당 이름 하나**만 나옵니다. `getFirmwareModel()`(장비 펌웨어가
보고하는 모델)을 쓰고, 그 값이 비어 있는 장비만 `getName()`으로 대체합니다.

| 장비 | `getName()` | `getFirmwareModel()` | 표에 표시 |
|---|---|---|---|
| Flame-S | `USB2000+` ← USB 제품 ID를 USB2000+와 공유 | `Flame-S` | **`Flame-S`** |
| Ocean FX | `OceanFX` | `Flame-X-F` | **`Flame-X-F`** |

`getName()`은 모델이 아니라 USB 제품군을 돌려주기 때문에 Flame-S에서 틀립니다.
두 문자열 모두 보려면 모델 셀에 마우스를 올리세요
(`Flame-X-F (getName: OceanFX)` 형태로 나옵니다).
`getName()` 쪽을 표시하고 싶으면 `DeviceInfo.ModelLabel`에서 우선순위만 바꾸면 됩니다.

모델이 달라도 다음이 각 장비 기준으로 따로 적용됩니다.

- **적분시간 한계** — Flame-S는 1 ms 미만을 넣을 수 없고, Ocean FX는 0.01 ms까지 됩니다.
- **트리거 지연 환산** — `setExternalTriggerDelay()`는 µs가 아니라 count를 받습니다.
  µs/count가 장비마다 달라서(Flame-S 0.5, Ocean FX 1.0) 같은 `50.0 us`도
  Flame-S에는 **100 counts**, Ocean FX에는 **50 counts**로 나갑니다. 실제 적용값은
  CSV의 `# Trigger delay applied` 줄에 기록됩니다.
- **픽셀 수와 파장 격자** — Use를 바꿔 픽셀 수가 달라지면 그래프 축이 다시 잡히고,
  Timed 시리즈는 격자가 바뀌는 순간 초기화됩니다(행렬이 직사각형이어야 하므로).
- **Dark / White Reference** — `spectrometerIndex`로 따로 보관되므로 두 모델의 보정을
  동시에 들고 있을 수 있습니다. 상태 표시줄에
  `[0] Dark: OK  White: OK    [1]* Dark: OK  White: OK`처럼 표시됩니다(`*`가 현재 Use).

> Flame-S는 드라이버가 **소수점이 붙은 counts**를 돌려줍니다(2048픽셀 전부.
> Ocean FX는 정수). boxcar·전기암전류·비선형 보정은 모두 꺼져 있으므로
> OmniDriver 내부 환산에 의한 것입니다.
> CSV는 유효숫자 8자리(`G8`)로 저장하므로, 파일의 Sample/Dark/White 열만 가지고
> 흡광도를 다시 계산하면 신호가 약한 구간에서 최대 ~1e-5 OD 정도 차이가 날 수 있습니다.
> 이는 저장 자릿수 때문이며 프로그램이 쓰는 값은 double 원본으로 계산됩니다.

---

## 6. 흡광도 계산과 Dark / White Reference

**Dark와 White Reference는 분광기별로 따로 저장됩니다.** 분광기 A를 Use로 선택해
A의 Dark·White를 캡처하고, 그 다음 B를 선택해 B의 것을 캡처하면 둘 다 유지됩니다
(`ReferenceStore`가 `spectrometerIndex`로 키를 잡습니다).

측정 중인 분광기는 **자기 자신의** Dark·White Reference로 흡광도를 계산합니다.

```
A = -log10( (S - D) / (W - D) )
```

### 실시간 레퍼런스 보정 (dual-beam)

광원 세기는 시간에 따라 변합니다. White Reference를 캡처한 뒤 램프가 5% 어두워지면
시료가 없어도 전 파장에서 0.022 OD가 얹힙니다. Probe / Reference를 지정하면
**Reference 분광기가 램프를 실시간 감시**해서 저장된 White Reference를 매 프레임
보정합니다.

**켜고 끄는 스위치는 없습니다.** 표의 상태만으로 결정되며, 조건이 갖춰지면 자동으로
걸리고 아니면 기존 단일빔 계산으로 되돌아갑니다.

```
f(λ_R) = (S_R − D_R) / (W_R − D_R)      ← Reference 분광기에서 측정
f(λ_P) = linear_interp(λ_R, f, λ_P)     ← Probe 파장 격자로 선형보간
A      = −log10( (S_P − D_P) / ((W_P − D_P) · f(λ_P)) )
```

- `f`는 **Reference 분광기 자기 자신의** dark·white 비율입니다. 그래서 그 검출기의
  gain·양자효율·적분시간이 모두 약분되고 **광량 변화만 남습니다.** 두 분광기의
  감도가 아무리 달라도 보정에 영향을 주지 않는 이유입니다.
- `f = 1.0`이면 램프가 White Reference 캡처 시점과 똑같이 밝다는 뜻입니다.
  현재 값은 체크박스 옆에 `active: lamp x1.0032 from [1]`처럼 실시간 표시됩니다.

**설정 순서**

1. 표에서 Probe / Reference를 지정합니다(한쪽만 클릭해도 Corr. 짝이 자동 페어링).
   3대 이상이면 Corr. 열에서 보정에 쓸 2대를 먼저 고릅니다.
2. Use = Probe로 두고 측정 → `Capture Dark` → `Capture White Ref.`
3. Use = Reference로 옮기고 측정 → `Capture Dark` → `Capture White Ref.`
4. Use = Probe로 되돌리면 **자동으로** 보정이 걸립니다.

**적용 조건** — 하나라도 빠지면 조용히 기존 방식으로 되돌아가고, 표 아래 한 줄에
그 이유를 그대로 적어 줍니다.

| 조건 | 못 갖췄을 때 표시 |
|---|---|
| 분광기 2대 이상 | `needs a second spectrometer` |
| Probe가 정확히 1대 | `2 probes - each uses its own dark/white` / `assign a probe` |
| Reference가 다른 분광기에 지정 | `assign a reference` |
| Use가 그 Probe를 가리킴 | `tick the probe [n] under Use` |
| Corr. 짝이 정확히 2대 | `tick exactly 2 under Corr.` |
| Corr. 짝 = {Probe, Reference} | `Corr. must be [n] and [m]` |
| 4개 스펙트럼(양쪽 dark + white) 저장됨 | `armed - capture dark/white on [n] too` |

걸려 있을 때는 `Live reference correction active: lamp x1.0032 from [1]`처럼
현재 광량비가 초록색으로 표시됩니다.

> **파장 겹침 구간에서만 보정됩니다.** Reference 분광기가 볼 수 없는 파장까지
> 외삽하면 없는 광량 데이터를 지어내는 셈이므로, 겹치지 않는 픽셀은 `NaN`이 되어
> 그래프에서 선이 끊기고 CSV에 빈 칸으로 남습니다. Flame-S(180–871 nm) + Ocean
> FX(316–1031 nm) 조합이면 **316–871 nm**에서만 흡광도가 나옵니다.

**캡처 방식**

`Capture Dark`와 `Capture White Ref.`는 새로 측정하지 않고 **연속 측정되어 화면에
떠 있는 그 스펙트럼을 그대로 복사**합니다. 따라서 누른 순간 조작자가 보고 있던
데이터가 그대로 저장됩니다.

- 아직 측정을 시작하지 않아 표시된 스펙트럼이 없으면 상태 표시줄에 안내만 뜨고
  아무것도 저장하지 않습니다.
- Use 선택을 바꾼 직후 화면의 프레임이 아직 이전 분광기의 것이면, 잘못된 분광기에
  저장되지 않도록 거부하고 다음 프레임을 기다리라고 알려 줍니다.
- `Clear Dark/White`는 **현재 Use로 선택된 분광기**의 것만 지웁니다.
- 분모/분자가 0 이하인 픽셀은 `NaN`이 되어 그래프에서 선이 끊기고 CSV에는 빈 칸으로 기록됩니다.

**확인 방법**: `Sample / Dark / White Ref.` 탭에 세 스펙트럼이 함께 그려집니다
(노랑 = 실시간 Sample, 청록 = 저장된 Dark, 주황 = 저장된 White Reference).
캡처가 되었는지는 이 화면과 상태 표시줄의 픽셀 수·평균 counts로 바로 확인됩니다.

---

## 7. UI 구성

### 7.0 창 구성 — 메뉴 / 툴바 / 단계 표시

```
┌───────────────────────────────────────────────────────────────┐
│ 파일   측정   보기   도구   도움말                              │  메뉴
├───────────────────────────────────────────────────────────────┤
│ ▶측정 시작 │ ☾Dark ☀White ✕ │ 저장 저장2 ●기록 │ ▤           │  툴바
├──────────────────────┬────────────────────────────────────────┤
│ ①측정 시작 ②Dark …  │                                        │  단계 표시
│ ┌ 측정 조건 ───────┐ │            그래프 탭                    │
│ │ …                │ │                                        │
│ └──────────────────┘ │                                        │
├──────────────────────┴────────────────────────────────────────┤
│ 장비 정보 │ 진행 상태 │ fps·시간 │ 누적 데이터                  │  상태 표시줄
└───────────────────────────────────────────────────────────────┘
```

설정 패널은 **왼쪽**에 있고, 사이의 구분선을 끌어 너비를 조절할 수 있습니다.
`F9`(또는 툴바 맨 오른쪽 단추)로 패널을 접으면 그래프가 창 전체를 씁니다.
접은 상태는 저장되어 다음 실행에도 유지됩니다.

**단계 표시**는 측정 순서를 그대로 보여 줍니다. 각 단계를 누르면 같은 동작이 실행됩니다.

| 표시 | 뜻 |
|---|---|
| 파란 번호 | 지금 할 차례 |
| 초록 체크 | 끝난 단계 |
| 주황 `!` | 건너뛴 단계 (Dark 없이 White만 측정한 경우) |
| 회색 | 아직 할 수 없는 단계 |

Dark와 White는 **화면에 떠 있는 스펙트럼**을 저장하는 것이므로 측정을 먼저 시작해야 합니다.
그래서 `①측정 시작`이 맨 앞에 옵니다(§6).

**단축키**

| 키 | 동작 | 키 | 동작 |
|---|---|---|---|
| `F5` | 측정 시작 / 중지 | `Ctrl+S` | 스펙트럼 저장 |
| `Ctrl+D` | Dark 측정 | `Ctrl+Shift+S` | 시간별 스펙트럼 저장 |
| `Ctrl+W` | White 측정 | `Ctrl+R` | 시간별 데이터 기록 시작/중지 |
| `Ctrl+1`…`Ctrl+6` | 화면 탭 이동 | `F9` | 설정 패널 접기/펴기 |
| `F1` | 사용 설명서 열기 | | |

### 7.1 한국어 / English

**도구 > 언어 / Language** 에서 바꿉니다. 바꾸는 즉시 메뉴·툴바·패널·탭·하드웨어 설정이
다시 그려지며, 프로그램을 다시 켤 필요가 없습니다. 처음 실행할 때는 Windows 표시 언어를
따르고, 고른 언어는 `settings.xml`에 저장됩니다.

- 번역문은 실행 파일에 포함된 `Localization\Strings_ko.txt`(UTF-8, `키 = 번역`) 한 곳에 있습니다.
  코드를 고치지 않고 문구만 바꿀 수 있고, 번역이 없는 키는 영어로 표시됩니다.
- **CSV 파일의 열 이름과 `#` 헤더, `Copy Spectrum Data`의 열 제목은 언어와 무관하게 영어입니다.**
  저장한 파일을 다른 프로그램이 읽는 형식이므로 언어에 따라 바뀌면 안 되기 때문입니다(§8).
- `Input` / `Output` / `LOW` / `HIGH` 같은 드라이버 용어와 트리거 모드 이름은 번역하지 않습니다.
  장비 설명서·드라이버 문서와 같은 말을 써야 찾아보기 쉽기 때문입니다.

**좌측 탭**

| 탭 | 내용 |
|---|---|
| Absorbance/Fluorescence → Absorption | 흡광도(OD) vs 파장 |
| Absorbance/Fluorescence → Fluorescence | 강도(counts) vs 파장 |
| Sample / Dark / White Ref. | 실시간 Sample + 저장된 Dark + 저장된 White Reference를 한 화면에 겹쳐 표시 (캡처 확인용) |
| Timed Absorption Series | 선택 파장에서 흡광도 vs 시간 (다중 트레이스) |
| Timed Absorption Series (2D plot) | 시간 × 파장 흡광도 맵 (viridis, 컬러바) |
| Hardware Settings | 분광기별 하드웨어 설정 (§7.2) |

보고 있는 탭만 갱신하고, 시계열 계열 화면은 4 Hz로 제한해 CPU를 아낍니다.

스펙트럼 그래프는 **흰 배경**에 어두운 실선으로 그립니다. 트레이스 색은
`MainForm.TraceColors` 한 곳에서 나오며(파랑 → 빨강 → 초록 → 주황 → 보라 → 갈색 →
청록 → 회색), 범례 색은 시리즈 색을 그대로 따르므로 항상 일치합니다.
같은 팔레트를 Timed 탭의 다중 트레이스에도 씁니다.

### 축 범위 조절 (autoscale 끄기 / 직접 입력)

**모든 그래프**에서 **오른쪽 클릭**하면 축별 autoscale 켜기/끄기 메뉴가 나옵니다.

| 그래프 | 메뉴 항목 |
|---|---|
| Absorption / Fluorescence / Sample·Dark·White / Timed | `Auto scale X`, `Auto scale Y` |
| Timed (2D plot) | `Auto scale time (X)`, `Auto scale wavelength (Y)`, `Auto scale colour (OD)` |

autoscale을 **끄면** 그 축의 눈금 숫자가 **파란색**으로 바뀝니다 — 이 축은 더 이상
데이터를 따라가지 않는다는 표시입니다. 2D 맵에서는 밝은 하늘색으로 표시됩니다.

**autoscale은 연속 동작입니다.** 켜져 있는 축은 프레임이 들어올 때마다 화면에 그려진
점들로부터 X·Y 범위를 다시 계산합니다. 켜는 순간에도 **지금 그려져 있는 스펙트럼을
기준으로 즉시** 다시 잡으므로, 측정을 멈췄다 다시 시작할 필요가 없습니다.
계산한 범위는 1·2·5 단위로 바깥쪽으로 반올림해 눈금이 깔끔한 값에 떨어지게 합니다.

**마우스로 확대·이동**

| 조작 | 결과 |
|---|---|
| 왼쪽 드래그 | 드래그한 사각형으로 확대 |
| Shift + 왼쪽 드래그 (또는 가운데 버튼 드래그) | 이동(pan) |
| 휠 | 커서 위치를 중심으로 확대/축소 |

**확대하거나 이동하면 그 축의 autoscale이 자동으로 꺼집니다.** 이후에는 새 프레임이
들어와도 범위가 그대로 유지되며, 오른쪽 클릭 메뉴에서 다시 켤 때까지 바뀌지 않습니다.
(휠은 그래프를 한 번 클릭해 포커스를 준 뒤에 동작합니다 — 휠 메시지는 포커스를 가진
컨트롤에만 전달되기 때문입니다.)

### 그래프 데이터를 클립보드로 복사

오른쪽 클릭 → **`Copy Spectrum Data`** 를 고르면 그래프의 모든 트레이스가
CSV 텍스트로 클립보드에 들어갑니다. 이미지가 아니라 숫자입니다.

```
Wavelength (nm),Sample,Dark,White reference
179.9333,1746.1362,1678.1728,1759.2061
180.1543,1742.0058,1681.4471,1755.6234
...
```

- 첫 열은 **X축**이며, 이름은 그래프의 X축 제목을 그대로 씁니다
  (Timed 탭이면 `Time (s)`).
- **화면에 보이는 구간이 아니라 트레이스가 가진 전체 범위**를 복사합니다.
  확대해 놓은 상태에서 복사해도 스펙트럼 전체(예: 179.9–870.7 nm, 2048행)가 들어갑니다.
- 값이 없는 지점은 빈 칸으로 남으므로 Excel·Origin·MATLAB·pandas에서
  결측치로 그대로 읽힙니다. 숫자 표기는 저장 파일과 같은 규칙입니다
  (X는 소수 4자리, 값은 유효숫자 8자리, InvariantCulture).
- 복사가 끝나면 상태 표시줄에 행 수와 트레이스 수를 알려 줍니다.

**범위 입력** — autoscale이 꺼진 축의 **첫 번째 또는 마지막 숫자를 클릭**하면 그 자리에
입력칸이 열립니다. 값을 치고 **Enter**(또는 다른 곳 클릭)면 그 쪽 끝이 바뀌고,
**Esc**면 취소됩니다. LabVIEW 그래프와 같은 방식입니다.

```
 4500 ┤ ← 클릭하면 여기서 바로 고칠 수 있음 (최댓값)
      │  ...
 2000 ┤ ← 클릭하면 최솟값
      └──┬─────┬─────┬──
        300   675   1050
                     ↑ 클릭 → 800 입력 → X축이 300..800 으로 좁혀짐
```

- **가운데 눈금은 편집 대상이 아닙니다.** 범위의 끝이 아니기 때문이며, 클릭하면
  그 이유를 알려주는 안내가 잠깐 뜹니다.
- autoscale이 **켜진** 상태에서 숫자를 클릭하면 입력칸 대신
  "오른쪽 클릭으로 먼저 끄세요" 안내가 뜹니다. 클릭만으로 몰래 autoscale이
  꺼지지 않도록 일부러 이렇게 했습니다.
- 최솟값이 최댓값 이상이 되는 입력은 **거부**되고 기존 범위가 유지됩니다.
- 고정한 범위는 **측정 중에도 그대로 유지**됩니다. 새 프레임이 들어와도 덮어쓰지 않습니다.
- autoscale을 다시 켜면 **직전에 고정했던 값이 아니라 현재 데이터 범위**로 즉시 돌아갑니다.
- 2D 맵에서 시간·파장 축을 고정하면 눈금만 바뀌는 것이 아니라 **그 구간만 다시 그립니다**
  (확대). 데이터가 없는 구간은 빈 색으로 남습니다.
- 2D 맵의 색상 범위는 위쪽 패널의 `Auto scale` 체크박스 / `Min (OD)` / `Max (OD)` 와
  **같은 값**입니다. 어느 쪽에서 바꾸든 서로 반영됩니다.

범위 설정은 **저장되지 않습니다** — 프로그램을 다시 켜면 모두 autoscale로 시작합니다.

### 7.1.5 흡광도 탭의 분석 막대

흡광도 그래프 위에 있는 줄에서 **보는 방식**과 **데이터 처리**를 고릅니다.
측정값 자체와 저장되는 CSV는 바뀌지 않습니다 — 화면에 그리는 값만 달라집니다.

| 항목 | 설명 |
|---|---|
| 표시 방식 | `흡광도 (OD)` / `투과율 (%T)` / `원시 counts`. %T는 `%T = 100 × 10^-A` 로 계산합니다 |
| 평활화 | Savitzky-Golay(2차). 이동 평균과 달리 **피크 높이와 위치를 유지**합니다. 구간(px)은 홀수로 맞춰집니다 |
| 기준선 보정 | `없음` / `0으로 맞출 파장`(그 파장이 0이 되도록 빼기) / `두 점 직선` / `아래 포락선`(곡선 아래에 고무줄을 친 선, 기준 파장 불필요) |
| 피크 | 국소 최대점 중 **최소 높이(prominence)** 이상인 것에 표식과 파장을 표시합니다. 가장 강한 것부터 최대 12개 |
| 스펙트럼 열기 | 저장해 둔 CSV를 불러와 현재 측정과 **겹쳐 그립니다**. 점선으로 표시되며 여러 개를 동시에 올릴 수 있습니다 |

**피크가 너무 많이 잡히면** 평활화를 켜거나 최소 높이를 올리세요. 잡음이 많은 스펙트럼에서는
잡음의 작은 봉우리도 국소 최대점이기 때문입니다. 반대로 **큰 띠의 어깨(shoulder)는 잡히지 않습니다** —
기울기가 돌아서지 않아 국소 최대점이 아니기 때문이며, 임계값을 아무리 낮춰도 나타나지 않습니다.

겹쳐 그리는 파일은 **같은 종류의 축에만** 올립니다. 흡광도 열이 있는 파일은 흡광도·%T 화면에,
counts만 있는 파일은 counts 화면에만 나타납니다. 서로 다른 물리량이 한 축에 섞이지 않게 하기 위해서입니다.

### 7.1.6 커서 값과 농도 계산

그래프 위에 마우스를 올리면 막대 아래 줄에 **커서 위치의 값**, **가장 높은 피크**, 그리고
**Beer-Lambert 계산 결과**가 표시됩니다.

```
커서 542.0 nm = 0.3520 OD     피크 542.0 nm = 0.3520 OD (모두 2개)
542.0 nm에서: e = 7040 L/(mol·cm)   (c = 50 uM, l = 1 cm)
```

- `e = A / (c · l)` 이며, 농도와 셀 두께는 **데이터 저장** 상자의 입력칸에서 읽습니다.
- 단위를 함께 쓸 수 있습니다: 농도는 `0.05`(=mM), `50 uM`, `1.5 M`, `2e-3 M`;
  셀 두께는 `10`(=mm), `10 mm`, `1 cm`. **숫자만 쓰면 입력칸 이름의 단위**(mM, mm)로 읽습니다.
- 흡광도 화면에서만 표시됩니다. %T와 counts는 이 식이 다루는 양이 아니기 때문입니다.

### 7.2 Hardware Settings 탭

연결된 분광기마다 독립된 영역이 하나씩 생깁니다. 표시 내용은 전부 드라이버가
보고한 값이며, 장비가 없는 기능은 **숨기지 않고 비활성 상태로** 보여 줍니다.

```
Spectrometer [0]  -  Flame-S  (FLMS-EXAMPLE)
  Model               Flame-S (getName: USB2000+)
  Serial              FLMS-EXAMPLE
  Role                probe (correction pair), measured (Use)

  Measurement Mode   [ Normal Mode                    ▼ ]
  (드라이버가 거부한 모드는 회색으로 표시되고 선택되지 않습니다)

  GPIO Channels
  ┌─────────┬───────────────┬────────────┬───────────┬──────────────────────┐
  │ Channel │ Capability    │ Direction  │ Output    │ State                │
  ├─────────┼───────────────┼────────────┼───────────┼──────────────────────┤
  │ GPIO 0  │ Input / Output│ [Output ▼] │ [HIGH ▼]  │ Output, driving HIGH │
  │ GPIO 1  │ Input / Output│ [Input  ▼] │ (회색)     │ Input, reads LOW     │
  │ GPIO 2  │ Unsupported   │   -        │   -       │ Disabled - …         │
  └─────────┴───────────────┴────────────┴───────────┴──────────────────────┘

Spectrometer [1]  -  Flame-X-F  (OFX-EXAMPLE)
  …같은 구성이 분광기마다 하나씩…
```

**GPIO 제어는 이 표에서 합니다.** 예전에 오른쪽 패널에 있던 별도의 `GPIO` 상자
(Enable / Pin / Value)는 없어졌습니다. 그 상자는 **Use로 선택된 분광기 한 대의 핀 하나**만
다룰 수 있었기 때문에, 분광기가 2대일 때 어느 장비의 핀인지 표에서 알 수 없었습니다.
이제는 분광기마다 자기 표가 있고, 표의 행이 곧 그 장비의 핀입니다.

| 예전 (오른쪽 GPIO 상자) | 지금 (분광기별 GPIO Channels) |
|---|---|
| Enable GPIO 체크박스 | 없음 — 핀을 바꾸는 것 자체가 명시적인 동작입니다 |
| Pin 0…N-1 스핀 | 표의 행 (드라이버가 보고한 채널마다 하나) |
| Value Low/High | `Output` 열 (그 핀이 출력일 때만 활성) |
| 대상 = Use로 선택된 분광기 | 대상 = 그 표가 속한 분광기 |

- **Measurement Mode** — 우측 패널의 `Trigger Mode` 와 **같은 설정**입니다.
  우측 패널은 Use로 선택된 분광기의 모드를 편집하고, 이 탭은 어느 분광기든
  편집합니다. 어느 쪽에서 바꾸든 서로 반영됩니다.
  모드는 이제 **분광기별 설정**입니다(적분시간·트리거 지연과 같은 취급).
- **Capability / Direction** — `getNumberOfPins()` 가 알려준 핀은 양방향을 모두
  받으므로 선택 상자가 나오고, `getTotalGPIOBits()` 에는 있지만 핀 수를 넘는 비트는
  `Unsupported` + 비활성 행으로 남습니다. GPIO 기능 자체가 없는 장비면
  표 전체가 비활성화되고 그 이유가 아래에 적힙니다.
- **Direction** — 바꾸면 그 핀을 general purpose I/O 로 돌리고 방향만 씁니다.
  **레벨은 건드리지 않습니다**: 방향을 고른 것만으로 커넥터에 신호가 나가면 안 되기
  때문입니다.
- **Output** — 그 핀이 **지금 출력일 때만** 선택할 수 있습니다. 입력 핀에 레벨을
  쓰는 것은 방향을 몰래 바꾸는 것과 같으므로 회색으로 두고
  `(set Direction to Output)` 이라고 적어 둡니다.
- **State** — 출력이면 내보내고 있는 레벨(`Output, driving HIGH`),
  입력이면 그 핀에서 **읽히는** 레벨(`Input, reads LOW`)입니다.
  입력 레벨은 바깥 회로가 정하므로, 탭을 열 때마다 드라이버에서 다시 읽습니다
  (주기적으로 폴링하지는 않습니다 — 예전 GPIO 상자에도 그런 기능은 없었습니다).
- 분광기가 연결/해제되면 이 탭은 분광기 표와 **같은 이벤트**로 다시 만들어집니다.
  별도의 장치 탐색 경로는 없습니다.

**어느 분광기의 핀인지 어떻게 보장되는가**

각 영역은 만들어질 때 그 분광기의 **serial number**(`DeviceInfo.Identity`, 시리얼이
없는 장비만 `index:N` 로 대체)를 기억하고, 그 영역에서 나가는 모든 요청에 index 와
serial 을 함께 실어 보냅니다. 적용 직전에 세션이 **그 index 에 정말 그 serial 의
장비가 있는지 다시 확인**하고, 다르면 쓰지 않고 거부합니다.

```
A 를 뽑아 B 가 index 0 이 됨
        ↓
A 용으로 큐에 남아 있던 명령
        ↓
"spectrometer AAA001 is no longer at index 0 (that index now holds BBB002)"  → 거부
```

index 만으로 장비를 고르면 위 상황에서 **B 의 핀이 바뀝니다.** serial 을 함께 싣는
이유가 이것입니다.

**설정 적용 순서 — `stop → apply → restart`**

측정 중에 모드나 GPIO 방향을 바꾸면 실행 중인 취득에 그대로 밀어 넣지 않고
다음 순서로 처리합니다.

```
측정 중이고, 그 분광기를 실제로 읽고 있는가?
   │
   ├── 예 ────► 취득 정지 ─► 설정 적용 ─► 드라이버에서 되읽어 확인 ─► 취득 재시작
   │
   ├── 아니오(다른 분광기) ─► 설정 적용 ─► 확인   (측정은 계속됩니다)
   │
   └── 애초에 정지 상태 ──► 설정 적용 ─► 확인   (자동으로 시작하지 않음)
```

취득 루프는 하나뿐이고 **Use 로 선택된 분광기**(그리고 보정이 켜져 있으면 Reference
분광기)만 읽습니다. 그래서 그 두 대에 대한 변경만 측정을 멈추게 하고, **읽지 않는
분광기의 GPIO 를 바꿀 때는 측정을 건드리지 않습니다** — 상관없는 장비를 설정하려고
데이터를 잃을 이유가 없기 때문입니다. 상태 표시줄이 어느 쪽이었는지 알려 줍니다:

```
[0] FLMS-EXAMPLE GPIO 1 direction -> Output applied - measurement restarted.
[1] OFX-EXAMPLE  GPIO 4 direction -> Output applied.
```

- **원래 멈춰 있었다면 설정을 바꿔도 측정이 시작되지 않습니다.**
- 적용은 **드라이버가 되읽어 확인한 뒤에만** 성공으로 표시합니다.
  실패하면 이전 값을 되돌리고, 화면의 선택도 실제 장비 상태로 되돌립니다.
  실패한 설정이 성공한 것처럼 남는 일은 없습니다.
- 되돌리기까지 실패하면 측정을 **재시작하지 않고** 멈춘 채로 두고 대화상자로 알립니다.
  상태를 모르는 장비로 계속 측정하는 것보다 낫기 때문입니다.
- 모드가 바뀌면 이전 모드에서 시작된 스펙트럼 1장을 `flushSpectrum()` 으로 버립니다.

**우측 패널**

- **Set parameters** — **Measurement 토글**(누르면 연속 측정, 다시 누르면 중단 —
  별도의 STOP 버튼은 없습니다), Set Min Interval (ms), Average,
  Measuring(표에서 파생된 읽기 전용 표시), Trigger Mode,
  **분광기 표**(§5), 적용된 트리거 지연 표시,
  Capture Dark / Capture White Ref. / Clear Dark/White
  (버튼 캡션에 대상 `spectrometerIndex`가 함께 표시됩니다)
- **Save datasets** — Sample / Concentration (mM) / Cell Thickness,
  Save path(폴더), Get File Path, Save Spectra, Save Timed Spectra
- **Set timed datasets** — Save timed datasets(녹화 토글, 녹화 중 초록), Clear datasets

**상태 표시줄** — 장비 정보, 진행 상태, fps·경과 시간·포화 경고, 누적 데이터 수/용량

---

## 7.3 측정 방법 파일

**파일 > 측정 방법 저장 / 불러오기** (`.uvmethod`). 같은 측정을 다음에 그대로 재현하기 위한 파일입니다.

- **담는 것**: 최소 측정 간격, 평균 횟수, 분광기별 적분시간·트리거 지연·역할(Use/Probe/Ref./Corr.),
  Timed 탭의 추적 파장, 표시 방식과 데이터 처리 설정.
- **담지 않는 것**: **Dark/White Reference**, 시료명, 저장 폴더, 창 배치.
  레퍼런스는 그 순간의 광원과 시료에만 유효하므로, 방법 파일을 불러왔다고 해서
  보정된 스펙트럼처럼 보이면 안 되기 때문입니다. 불러온 뒤에도 Dark/White는 그대로 유지됩니다.
- 분광기별 값은 **시리얼 번호**로 찾습니다. 연결 순서가 달라져도 같은 장비에 같은 값이 들어가며,
  불러올 때 그 장비의 허용 범위로 다시 클램프합니다.

## 7.4 측정 중 디스크 기록 (데이터 보호)

**시간별 데이터 기록**을 켜면 메모리에 쌓는 것과 **동시에 디스크에도 한 줄씩 기록**합니다.

- 파일은 `<시료명>_timed_live_<시각>_absorbance.csv` 와 `..._counts.csv` 입니다.
- 한 줄 쓸 때마다 즉시 내보내므로, **프로그램이 비정상 종료돼도 그때까지 측정한 데이터는 남습니다.**
  메모리 버퍼는 5000개를 넘으면 오래된 것부터 버리지만, 파일에는 전부 남습니다.
- 형식은 **Save Timed Spectra 와 동일**합니다(같은 `#` 헤더, 같은 열, 같은 숫자 표기).
- 기록을 멈추면 상태 표시줄에 기록한 행 수가 표시됩니다.

## 7.5 이미지와 보고서 내보내기

| 메뉴 | 결과 |
|---|---|
| 파일 > 그래프를 이미지로 저장 (`Ctrl+P`) | 지금 보고 있는 그래프를 PNG로 저장 |
| 파일 > 측정 보고서 내보내기 (`Ctrl+E`) | A4 한 장짜리 PDF: 제목, 시료 정보, 측정 조건, 피크 목록, 그래프 |

보고서 PDF는 외부 라이브러리 없이 직접 만듭니다. 페이지를 먼저 그림으로 그린 뒤 PDF에 넣는 방식이라
**한글 글꼴을 PDF에 포함하지 않아도 한글이 그대로 나옵니다.**

## 8. 저장 형식

파일명은 `<Sample>_<종류>_<yyyyMMdd_HHmmss>.csv`로 자동 생성됩니다.
모든 수치는 **InvariantCulture(소수점 `.`)** 로 기록됩니다.

- **Save Spectra** → `..._spectrum_....csv`
  `Wavelength (nm), Sample (counts), [Dark (counts)], [White reference (counts)], [Absorbance (OD)]`
  Dark·White 열은 저장된 것이 있을 때만 나옵니다. 저장된 값을 그대로 쓰므로
  `A = -log10((S - D) / (W - D))`를 CSV 안에서 직접 재계산해 검증할 수 있습니다.
- **Save Timed Spectra** → `..._timed_..._absorbance.csv`, `..._timed_..._counts.csv`
  행 = 1회 취득, 열 = 픽셀. 첫 두 열은 `Elapsed (s)`, `Timestamp (UTC)`.

두 형식 모두 상단에 `#` 주석으로 시료 정보와 당시 계측 설정
(측정 분광기, 빔 배정, 적분/평균/트리거 모드/적용된 트리거 지연/GPIO 상태/참조 상태)을 남깁니다.

시계열 버퍼는 기본 **5000 레코드**로 제한되며 초과 시 오래된 것부터 버립니다
(`TimedSeriesStore.Capacity`).

---

## 9. 소스 구성

`UV-VIS_measurement_code\` 아래의 구성입니다. 네임스페이스는 `UvVisMeasurement`,
어셈블리 이름은 `UV-VIS_meas`입니다(C# 네임스페이스에는 `-`를 쓸 수 없어 서로 다릅니다).

```
UV-VIS_meas.csproj             x86 고정, OmniDriver 경로 탐색, 아이콘 포함
Program.cs                     진입점, 드라이버 탐색, 예외 로깅
Driver/OmniDriverLocator.cs    OOI_HOME 탐색 + 어셈블리/네이티브 DLL 로딩
Driver/ISpectrometerSession.cs 장비 백엔드 경계 (OmniDriver / 가상 장비 / 향후 OceanDirect)
Driver/SpectrometerBackend.cs  실행 시 백엔드 선택 (--demo)
Driver/SpectrometerSession.cs  OmniDriver 구현: NETWrapper·NETGPIO·NETExternalTriggerDelay 소유 및 해제
Driver/SimulatedSpectrometerSession.cs  데모·테스트용 가상 분광기 2대
Driver/AcquisitionEngine.cs    전용 취득 스레드, 명령 큐, UI 이벤트 발행
Driver/DeviceInfo.cs           분광기 정적 정보, 트리거 지연 환산 결과
Driver/DeviceSettings.cs       분광기 표 1행: 측정 대상 여부, 빔 역할, 적분시간, 트리거 지연
Driver/MeasurementSettings.cs  공통 파라미터 + 분광기별 설정 배열, 활성/역할 파생 속성
Driver/SpectrumFrame.cs        1회 취득 결과 + 그 시점의 Dark / White Reference
Driver/HardwareCapabilities.cs 드라이버가 보고한 측정 모드·GPIO 채널, 설정 변경 요청/결과
Analysis/SpectrumMath.cs       흡광도 계산, 램프 드리프트 계수, 선형보간 리샘플링
Analysis/ReferenceStore.cs     spectrometerIndex별 Dark / White Reference
Analysis/TimedSeriesStore.cs   시계열 버퍼 + 불변 스냅샷
Analysis/SpectrumProcessing.cs 표시 방식 변환, 평활화, 기준선, 피크 찾기
Analysis/BeerLambert.cs        농도·셀 두께 단위 해석과 몰흡광계수 계산
Io/CsvExporter.cs              CSV 출력
Io/MethodFile.cs               측정 방법 파일 저장/불러오기
Io/SpectrumImport.cs           저장된 CSV 다시 읽기 (겹쳐보기)
Io/TimedSeriesRecorder.cs      측정 중 디스크 기록
Io/ReportExporter.cs           PNG 저장과 PDF 보고서 생성
Io/UserSettingsStore.cs        사용자 설정 저장/복원 (settings.xml)
Ui/MainForm.cs                 화면: 필드, 초기화, 측정 토글, 종료
Ui/MainForm.Shell.cs           메뉴·툴바·단축키·단계 표시·테마·언어 전환
Ui/MainForm.Analysis.cs        분석 막대, 겹쳐보기, 피크·커서 표시, 내보내기, 방법 파일
Ui/MainForm.DeviceTable.cs     분광기 표와 역할(Use/Probe/Ref./Corr.)
Ui/MainForm.Charts.cs          엔진 이벤트와 모든 그래프 갱신
Ui/MainForm.Hardware.cs        설정 전송과 stop→apply→restart 경로
Ui/MainForm.Actions.cs         버튼 처리: 레퍼런스·저장·시계열·트레이스
Ui/MainForm.UserSettings.cs    설정 저장/복원과 창 위치
Ui/Theme.cs                    색과 컨트롤 스타일 (한 곳에서 수정)
Ui/Icons.cs                    툴바·메뉴 아이콘 (벡터로 직접 그림)
Ui/WorkflowStrip.cs            측정 순서 단계 표시
Ui/AboutForm.cs                프로그램 정보 창
Ui/SplashForm.cs               시작 화면
Localization/L.cs              한/영 전환
Localization/Strings_ko.txt    한국어 문구 (실행 파일에 포함)
Ui/SpectrumChart.cs            갱신 중 페인트를 막는 Chart 파생 클래스 + 축 범위 조작
Ui/AxisScaling.cs              축별 auto/manual 상태(AxisScale)와 눈금 위 인라인 입력칸
Ui/HardwareSettingsView.cs     Hardware Settings 탭: 분광기별 영역 생성, 변경 요청/결과 반영
Ui/HeatmapControl.cs           2D 흡광도 맵 (시간·파장·색상 3개 스케일)
Assets/AppIcon.ico             EXE·창 아이콘
build.ps1                      x86 Release 빌드
publish.ps1                    빌드 + 더블클릭용 배포 폴더/바로가기 생성
```

### `SpectrumChart`가 필요한 이유

`System.Windows.Forms.DataVisualization`은 **자신의 `OnPaint` 안에서
`Series.Points`를 열거**합니다(`ChartArea.SetDefaultAxesValues` →
`ChartHelper.SeriesXValuesZeros`). 데이터 갱신 도중 `WM_PAINT`가 재진입하면
`InvalidOperationException: Collection was modified`가 페인트 핸들러 밖으로
튀어나와 프로세스가 종료됩니다. 모달 대화상자가 열리거나 다른 프로세스가
메시지를 보내면 메시지 루프가 펌핑되므로 실제로 발생하는 상황입니다.
`SpectrumChart`는 갱신 구간 동안 페인트를 건너뛰고, 만일을 대비해 예외도 흡수합니다.

**다만 건너뛸 때 아무것도 그리지 않으면 안 됩니다.** `Chart`는
`ControlStyles.OptimizedDoubleBuffer`로 그리는데 그 버퍼는 0으로 채워져 있어서,
그리지 않고 반환하면 **새까만 사각형이 화면에 blit**됩니다. 프레임 단위로 반복되면
차트가 검게 깜빡이는 것으로 보입니다(Dark·White를 저장해 시리즈가 3개가 되면
화면 캡처 기준 전체 프레임의 10–17%에서 발생했습니다).

그래서 완성된 렌더는 항상 **영구 오프스크린 비트맵**에 그린 뒤 화면으로 blit하고,
건너뛰어야 하는 페인트는 **그 비트맵을 다시 blit**합니다. 결과적으로 직전 스펙트럼이
그대로 남고 검은 깜빡임이 사라집니다.

`MainForm.Plot()`도 같은 목적을 공유합니다. 프레임마다 점 개수가 바뀌는 일은 거의
없으므로 `Points.Clear()` + `AddXY()` 대신 **기존 `DataPoint`를 제자리에서 덮어씁니다.**
시리즈당 수천 개 객체 할당이 사라져 페인트를 막는 구간 자체가 짧아집니다.

같은 이유로 시계열 데이터는 라이브 리스트가 아니라 `TimedSeriesSnapshot`
(불변 복사본)을 통해서만 읽습니다.

### 축 범위를 `AxisScale`이 따로 들고 있는 이유

차트에 축 범위를 직접 쓰지 않고 `Ui/AxisScaling.cs`의 `AxisScale`이 소유합니다.
축마다 **데이터가 제안한 범위**와 **사용자가 입력한 범위**를 나눠서 들고 있어서,

- 프레임마다 들어오는 제안이 사용자가 고정한 범위를 덮어쓰지 않고,
- autoscale을 다시 켜면 옛날 값이 아니라 **최신 데이터 범위**로 즉시 돌아갑니다.

**범위 계산도 라이브러리에 맡기지 않습니다.** `Axis.Minimum`에 `NaN`을 넣어 라이브러리가
스스로 정하게 하면, 라이브러리는 계산한 값을 **그 속성에 되써 넣습니다**
(실측: `NaN` 지정 후 렌더하면 `Minimum`이 `315.1063`으로 읽힘). 즉 축을 읽어도
"무엇을 요청했는지"는 알 수 없고 "무엇이 그려졌는지"만 알 수 있어, 화면과 상태가
같은 말을 하는지 확인할 방법이 없어집니다.

그래서 `SpectrumChart.AutoScaleFromData()` 가 **그려진 점들을 직접 훑어** X·Y 범위를
구하고(빈 점은 Y 계산에서 제외), 5% 여유를 준 뒤 1·2·5 단위로 바깥쪽 반올림해
**항상 명시적인 숫자**를 축에 씁니다. 덕분에

- autoscale을 켜는 즉시 현재 화면 데이터로 범위가 잡히고(프레임을 기다리지 않음),
- 데이터 갱신이 끝날 때마다(`EndDataUpdate`) 다시 계산되어 연속적으로 따라가며,
- 확대·이동이 만든 픽셀 → 값 변환도 같은 축 값을 근거로 하므로 어긋나지 않습니다.

점이 하나도 없으면 이전 범위를 그대로 둡니다. 시리즈가 비면 라이브러리가 축 자체를
그리지 않으므로, 범위를 지워 봐야 시작 범위만 잃기 때문입니다.

`Axis.Minimum`을 그대로 노출하지 않고 눈금 라벨의 `FromPosition`/`ToPosition` 중점을
쓰는 것도 같은 이유입니다. 표시된 문자열을 파싱하면 `0.###` 반올림 오차가 섞이지만,
중점은 눈금의 정확한 값이라 "이 숫자가 축의 끝인가"를 오차 없이 판정할 수 있습니다.

### Hardware Settings 탭을 열 때 만드는 이유

`HardwareSettingsView`는 분광기 목록만 받아 두고, **탭이 실제로 화면에 올라온 뒤에**
컨트롤을 만듭니다. 선택되지 않은 `TabPage`를 채우면 그 페이지가 계속 "보이는" 상태로
남아 `TabControl`이 더 이상 감추지 못하고, **이전 탭이 새 탭 위에 그대로 그려진 채**
남습니다(실측: 페이지 2개가 동시에 visible). 화면에 올라온 뒤 만들면 이 문제가 없고,
이 탭을 열지 않는 사용자에게는 비용도 들지 않습니다.

GPIO 표를 갱신할 때 행을 **다시 만들지 않고 제자리에서 고치는** 것도 같은 종류의
이유입니다. 편집 중인 셀이 있는 상태에서 `Rows.Clear()`를 하거나 셀을 다른 종류로
바꾸면 `DataGridView`가 예외를 던지고, 페인트 중 예외가 나면 표가 그때부터
**빨간 X로 그려진 채** 남습니다(실측). 방향을 고른 직후가 정확히 그 상황이므로,
갱신 전에 `EndEdit()`를 먼저 부르고 셀 값만 덮어씁니다.

`Output` 열은 방향에 따라 **선택 상자와 잠긴 텍스트 사이를 오갑니다.** 이때 셀 객체를
바꿔 끼우는데, 잠긴 셀이 있던 자리에 새 셀을 넣으면 그 셀이 **행의 read-only 상태를
물려받아** 그려지기는 해도 열리지 않았습니다(실측: 방향을 Output 으로 바꾼 뒤 레벨을
클릭해도 아무 일도 일어나지 않음). 그래서 선택 상자를 놓을 때는 `Unlock()`으로 그
셀의 read-only 를 명시적으로 풀고, 잠겨 있어야 할 나머지 셀은 다시 잠급니다.

---

## 10. 문제 해결

| 증상 | 원인 / 조치 |
|---|---|
| `BadImageFormatException` | x86으로 빌드하지 않음. `PlatformTarget=x86` 확인 |
| 빌드 시 `MSB3644` | .NET Framework 개발자 팩 없음 → 폴백이 동작하는지 확인, 또는 팩 설치 |
| "Could not find NETOmniDriver-NET40.dll" | OmniDriver 미설치이거나 비표준 경로. 대화상자에서 `OOI_HOME` 지정 |
| 프로그램을 다시 실행했는데 새 창이 안 뜸 | 정상입니다. 이미 실행 중인 창을 앞으로 가져옵니다. 분광기는 한 프로그램만 열 수 있어 한 번만 실행되도록 했습니다 |
| 설치·실행 시 "Windows의 PC 보호" | 코드 서명을 하지 않은 오픈소스 배포라서 나옵니다. **추가 정보 → 실행** |
| 분광기 0대 | 다른 측정 프로그램(OceanView 등)이 장비를 잡고 있지 않은지 확인. USB 연결 확인. OmniDriver 재설치 시 `NatUSB_32_winusb.dll` → `NatUSB.dll` 복사 필요(매뉴얼 부록 F) |
| "Waiting for trigger..." | 외부 트리거 모드에서 트리거 신호 없음. USB 타임아웃 후 자동 재시도하므로 Measurement 토글은 항상 반응함 |
| Hardware Trigger(모드 3)에서 Normal로 못 돌아감 | 매뉴얼 명시 사항. 분광기 전원을 뽑았다 다시 연결해야 함 |
| 흡광도가 노이즈만 | 광원이 없거나 White Reference를 캡처하지 않음. 측정을 시작한 뒤 `Capture Dark` → `Capture White Ref.` 순서로 진행 |
| "no spectrum is displayed yet" | Capture는 화면에 표시된 데이터를 저장합니다. Measurement 토글을 먼저 누르세요 |
| Probe/Reference 체크박스가 아예 회색 | 분광기가 1대뿐입니다. 빔을 나눌 상대가 없어 의도적으로 비활성화됩니다 |
| 다른 행의 Probe/Reference를 못 누름 | 이미 한 쌍이 배정되어 있습니다. 배정된 쪽의 체크를 풀면 두 역할이 함께 풀리고 다시 선택할 수 있습니다 |
| 다른 행의 Use를 못 누름 | Use는 1개만 선택됩니다. 현재 행의 체크를 먼저 해제하세요 |
| 측정 중 Use/Probe/Reference를 못 바꿈 | 의도된 잠금입니다. 도중에 바뀌면 저장된 Dark·White Reference가 무효가 되므로 먼저 측정을 중단하세요 |
| "the displayed spectrum is from spectrometer [x]" | Use를 막 바꿔서 화면이 아직 이전 분광기의 프레임입니다. 다음 프레임을 기다린 뒤 다시 누르세요 |
| "Tick the spectrometer to measure..." | 표에서 Use가 하나도 체크되지 않았습니다 |
| Ocean FX가 `Flame-X-F`로 표시됨 | 정상입니다. 표는 장비 펌웨어가 보고하는 모델명(`getFirmwareModel()`)을 씁니다. Ocean FX의 펌웨어 모델이 `Flame-X-F`입니다. 셀에 마우스를 올리면 `getName()` 값과 S/N·픽셀 수·파장 범위가 함께 나옵니다 |
| 차트가 검게 깜빡임 | 해결됨. 갱신 중 페인트를 건너뛸 때 0으로 채워진 더블버퍼가 그대로 보이던 문제였습니다. §9 `SpectrumChart` 참고 |
| 실시간 보정이 안 걸림 | 표 아래 문구가 이유를 그대로 알려 줍니다(§6 적용 조건 표). 대개 Reference 분광기의 Dark/White를 아직 안 찍은 경우입니다 |
| Reference 체크박스가 전부 회색 | Probe를 2대 이상 체크했습니다. 램프를 감시할 분광기가 없으므로 의도적으로 죽입니다. Probe를 1대로 줄이면 돌아옵니다 |
| 3번째 행의 Corr.를 못 누름 | 이미 2대가 짝으로 지정돼 있습니다. 한쪽을 먼저 해제하세요 |
| 축 숫자를 눌러도 입력칸이 안 뜸 | 그 축의 autoscale이 아직 켜져 있습니다. 그래프에서 오른쪽 클릭 → `Auto scale X/Y` 해제 후 다시 누르세요 (§7) |
| 가운데 눈금은 안 고쳐짐 | 의도된 동작입니다. 범위의 양 끝(첫 숫자·마지막 숫자)만 편집 대상입니다 |
| 입력했는데 범위가 안 바뀜 | 최솟값 ≥ 최댓값이 되는 값은 거부됩니다. 반대쪽 끝을 먼저 옮기세요 |
| 축 숫자가 파란색 | 그 축이 고정(autoscale 꺼짐)됐다는 표시입니다. 오른쪽 클릭으로 다시 켤 수 있습니다 |
| 측정을 다시 시작해도 축이 안 움직임 | 그 축을 고정해 두었기 때문입니다. 의도된 동작이며, autoscale을 켜면 데이터를 따라갑니다 |
| 2D 맵 일부가 빈 색 | 고정한 시간·파장 구간에 데이터가 없는 영역입니다. autoscale로 되돌리면 전체가 채워집니다 |
| 프로그램을 다시 켜니 축이 초기화됨 | 범위 설정은 저장하지 않습니다. 매 실행은 autoscale로 시작합니다 |
| 데이터가 없을 때 그래프가 검은 사각형 | 정상입니다. 시리즈에 점이 하나도 없으면 라이브러리가 축 자체를 그리지 않습니다. Measurement를 시작하면 축이 나타납니다 (Absorption 탭은 White Reference까지 캡처해야 함) |
| 측정 중 Corr./Probe/Reference를 못 바꿈 | 의도된 잠금입니다. 도중에 바뀌면 저장된 보정이 무효가 되므로 먼저 측정을 중단하세요 |
| 겹침 구간 밖에서 흡광도가 비어 있음 | 의도된 동작입니다. Reference 분광기가 그 파장을 못 보므로 외삽하지 않습니다 |
| 보정을 켜니 프레임 속도가 떨어짐 | 정상입니다. 한 사이클에 분광기 2대를 읽습니다. 필요하면 Reference 쪽 적분시간을 줄이세요 |
| 적분시간을 낮췄는데 값이 올라감 | 그 장비의 최소 적분시간으로 클램프된 것입니다. Integration 셀 툴팁에 허용 범위가 표시됩니다(Flame-S는 1 ms가 최소) |
| 같은 지연을 넣었는데 counts가 다름 | 정상입니다. µs/count가 장비마다 다릅니다(Flame-S 0.5, Ocean FX 1.0) |
| 확대했더니 autoscale이 꺼졌다 | 의도된 동작입니다. 직접 범위를 정한 것으로 보고 고정합니다. 오른쪽 클릭 → `Auto scale X/Y` 로 다시 켜면 현재 데이터로 즉시 돌아갑니다 |
| 휠을 굴려도 확대가 안 됨 | 그래프를 한 번 클릭해 포커스를 준 뒤 굴리세요. 휠 메시지는 포커스를 가진 컨트롤에만 갑니다 |
| 확대를 되돌리는 버튼이 없음 | `Auto scale X`/`Auto scale Y` 를 다시 켜는 것이 되돌리기입니다 |
| `Copy Spectrum Data` 를 눌렀는데 아무 일도 없음 | 상태 표시줄을 보세요. 그릴 데이터가 없거나, 다른 프로그램이 클립보드를 잡고 있으면 그 사실을 알려 줍니다 |
| 복사한 CSV에 확대 구간만 있을 줄 알았다 | 항상 트레이스 전체를 복사합니다. 확대는 보이는 범위만 바꿀 뿐 데이터를 자르지 않습니다 |
| 그래프 배경이 흰색으로 바뀜 | 의도된 변경입니다. 트레이스 색도 흰 배경에서 구분되는 팔레트로 함께 바꿨습니다(§7) |
| Measurement Mode 목록이 장비마다 같음 | OmniDriver가 지원 모드 목록을 제공하지 않기 때문입니다. 탭 안에 그 사실이 적혀 있습니다(§3) |
| 모드를 바꿨더니 측정이 잠깐 멈췄다 다시 시작됨 | 의도된 `stop → apply → restart` 순서입니다(§7.2) |
| 모드를 바꿨는데 측정이 시작되지 않음 | 원래 멈춰 있었기 때문입니다. 설정만 적용하고 자동으로 시작하지 않습니다 |
| 모드를 골랐는데 이전 값으로 돌아감 | 드라이버가 그 설정을 거부했습니다. 상태 표시줄에 이유가 나옵니다. 화면은 항상 장비의 실제 상태를 보여 줍니다 |
| GPIO 방향을 바꿨는데 신호가 안 나감 | 방향 선택은 레벨을 쓰지 않습니다. 같은 행의 `Output` 열에서 HIGH/LOW 를 고르세요(§7.2) |
| `Output` 열이 회색 | 그 핀이 아직 입력이거나 드라이버가 출력으로 내주지 않는 핀입니다. 먼저 `Direction` 을 Output 으로 바꾸세요 |
| 회색으로 비활성인 GPIO 행 | 드라이버가 GPIO 비트로는 세지만 핀으로는 노출하지 않는 채널입니다. 임의로 만든 행이 아닙니다 |
| 오른쪽에 있던 GPIO 상자가 없어짐 | Hardware Settings 탭의 분광기별 GPIO Channels 로 옮겼습니다(§7.2). 기능은 그대로이고 어느 장비의 핀인지가 분명해졌습니다 |
| 분광기 2대인데 어느 쪽 핀인지 헷갈림 | 각 영역 제목에 모델과 시리얼이 적혀 있고, 상태 표시줄 메시지에도 시리얼이 함께 나옵니다 |
| 다른 분광기 GPIO 를 바꿨는데 측정이 안 멈춤 | 의도된 동작입니다. 지금 읽고 있지 않은 장비이므로 측정을 끊을 이유가 없습니다(§7.2) |
| 입력 핀의 State 가 옛날 값 같음 | 입력 레벨은 탭을 열 때 다시 읽습니다. 다른 탭에 갔다 오면 갱신됩니다 |
| 예상치 못한 오류 대화상자 | `%LOCALAPPDATA%\UV-VIS_meas\error.log`에 전체 스택이 기록됨 |
