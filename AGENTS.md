# PROJECT KNOWLEDGE BASE

**Generated:** 2026-10-28
**Commit:** 4162c70
**Branch:** main

## OVERVIEW
C# WinForms (.NET Framework 4.8, C# 7.3, **x86 only**) UV-VIS measurement app for Ocean Optics / Ocean Insight spectrometers via the vendor OmniDriver .NET assembly (`NETOmniDriver-NET40.dll`). Spectrum capture, dark/white-reference absorbance, dual-spectrometer lamp-drift correction, timed series + heatmap, CSV export.

## STRUCTURE
```
UV-VIS_measurement/
├── docs/README.md              # Korean operator manual (780 lines) - also the design-rationale doc (sec 3, 8, 9)
└── UV-VIS_measurement_code/    # the single project (UV-VIS_meas.csproj, no .sln)
    ├── Program.cs              # entry: OmniDriverLocator BEFORE MainForm is JIT'd, crash logging
    ├── Driver/                 # OmniDriver wrapper, acquisition thread -> see Driver/AGENTS.md
    ├── Analysis/               # pure math + stores (no driver, no UI types)
    ├── Io/CsvExporter.cs       # CSV writer
    ├── Ui/                     # MainForm + custom-drawn charts -> see Ui/AGENTS.md
    ├── build.ps1 / publish.ps1 # MSBuild x86 build; publish stages ../UV-VIS_meas/ + .lnk shortcuts
    └── Assets/AppIcon.ico
```
Namespace root `UvVisMeasurement.*`; assembly name `UV-VIS_meas` (differs on purpose: `-` illegal in namespaces).

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Absorbance / drift formula | `Analysis/SpectrumMath.cs` | `StoredReferenceAbsorbance`, `DriftFactor`, `DriftCorrectedAbsorbance`, `Resample` |
| Dark / white reference storage | `Analysis/ReferenceStore.cs` | keyed by spectrometer index; `EnsurePixelCount` wipes on geometry change |
| Timed buffer | `Analysis/TimedSeriesStore.cs` | `Capacity` = 5000, drops oldest; UI reads only `TimedSeriesSnapshot` |
| CSV output | `Io/CsvExporter.cs` | format spec in docs/README.md sec 8 |
| Driver load failure | `Driver/OmniDriverLocator.cs`, `Program.cs` | |
| New UI control / chart behavior | `Ui/` | |
| Build path probing | `UV-VIS_meas.csproj` | OmniDriverHome property chain |

## CODE MAP
| Symbol | Type | Location | Role |
|--------|------|----------|------|
| `OmniDriverLocator` | static class | Driver/OmniDriverLocator.cs | find OOI_HOME, hook AssemblyResolve, SetDllDirectory |
| `AcquisitionEngine` | class | Driver/AcquisitionEngine.cs | sole worker thread, command queue, UI events |
| `SpectrometerSession` | class | Driver/SpectrometerSession.cs | owns NETWrapper / NETGPIO / trigger-delay controllers |
| `MeasurementSettings` | class | Driver/MeasurementSettings.cs | shared params + per-device `DeviceSettings[]`, `Clone()` |
| `SpectrumFrame` | class | Driver/SpectrumFrame.cs | one acquisition + references valid at that moment |
| `SpectrumMath` | static class | Analysis/SpectrumMath.cs | all numeric formulas |
| `CsvExporter` | static class | Io/CsvExporter.cs | `SaveSpectrum`, `SaveTimedSeries` |
| `MainForm` | partial Form | Ui/MainForm.cs (2095 lines) | wires engine events to charts/tables |

## CONVENTIONS
- **x86 pinned everywhere** (`PlatformTarget`, build.ps1 `/p:Platform=x86`). AnyCPU/x64 = `BadImageFormatException` (mixed-mode C++/CLI vendor DLL).
- OmniDriver reference is `Private=False`: loaded from OOI_HOME at runtime so native `OmniDriver32.dll` resolves. Never copy it next to the EXE.
- No targeting pack required: csproj falls back to `%windir%\Microsoft.NET\Framework\v4.0.30319` when Reference Assemblies are absent.
- Every new `.cs` file must be added to the explicit `<Compile Include>` list in the csproj (old-style project, no globbing).
- Units live in identifiers: `IntegrationUs`, `TriggerDelayUs`, `MinIntervalMs`. Trigger delay crosses to the driver as **counts**, not µs.
- Report what the driver **confirmed**, not what was requested (outcome structs, CSV headers, UI).
- CSV: `InvariantCulture`, X as `F4`, values as `G8`, `UTF8Encoding(false)` (no BOM), `#` comment header with settings snapshot, filename `<Sample>_<kind>_<yyyyMMdd_HHmmss>.csv`.
- User-facing UI strings: English. Operator docs / README / publish `사용법.txt`: Korean.
- Long explanatory XML/inline comments explain *why* (often citing measured behavior); preserve them when editing.

## ANTI-PATTERNS (THIS PROJECT)
- Do not hardcode per-model capability tables (trigger modes, delay scale, pixel counts). Read from the driver at runtime; docs sec 3 explains why (the driver echoes any trigger mode, Ocean FX 1 count = 1 µs vs Flame-S 0.5 µs).
- Do not use `setCorrectForElectricalDark`: dark correction = subtract the captured Dark spectrum (Ocean FX has 0 dark pixels).
- Do not reference OmniDriver types from `Program.Main` or anything JIT'd before `OmniDriverLocator.Initialize` (hence `[NoInlining] RunApplication`).
- Do not change CSV column order / formats casually; files are meant to be re-verifiable as `A = -log10((S-D)/(W-D))` from stored columns.
- Do not commit binaries, `ooi_home.txt`, `error.log`, the vendor PDF, or `/UV-VIS_meas/` (all gitignored).

## COMMANDS
```powershell
cd UV-VIS_measurement_code
.\build.ps1                                   # Release x86 -> bin\Release\UV-VIS_meas.exe
.\build.ps1 -Configuration Debug
.\build.ps1 -OmniDriverHome "C:\path\to\OOI_HOME"
.\publish.ps1 [-NoDesktopShortcut]            # build + stage ..\UV-VIS_meas\ + shortcuts
```
No test project exists. Verification = build succeeds + manual run with hardware.

## NOTES
- Build fails at `VerifyOmniDriverHome` if OmniDriver is not installed; you cannot compile on a machine without it (or without `/p:OmniDriverHome`).
- Runtime state: `%LOCALAPPDATA%\UV-VIS_meas\error.log` (crash log), `ooi_home.txt` (manually picked OOI_HOME).
- `App.config` redirects all `NETOmniDriver-NET40` versions to 2.45.0.0.
- `AllowUnsafeBlocks` exists only for `Ui/HeatmapControl.cs` (BitmapData.Scan0 raster).
- Hardware-integration code: behavior is verified on real Ocean FX / Flame-S units; cite docs/README.md before "simplifying" odd-looking driver workarounds.
