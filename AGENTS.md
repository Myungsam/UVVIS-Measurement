# PROJECT KNOWLEDGE BASE

**Generated:** 2026-10-28
**Commit:** 4162c70
**Branch:** main

## OVERVIEW
C# WinForms (.NET Framework 4.8, C# 7.3, **x86 only**) UV-VIS measurement app for Ocean Optics / Ocean Insight spectrometers via the vendor OmniDriver .NET assembly (`NETOmniDriver-NET40.dll`). Spectrum capture, dark/white-reference absorbance, dual-spectrometer lamp-drift correction, timed series + heatmap, CSV export.

## STRUCTURE
```
UV-VIS_measurement/
├── docs/README.md              # Korean operator manual - also the design-rationale doc (sec 3, 8, 9)
├── installer/                  # Inno Setup script, build+sign script, EULA (ko/en)
├── THIRD-PARTY-NOTICES.md      # shipped next to the EXE; the About box points at it
└── UV-VIS_measurement_code/    # the single project (UV-VIS_meas.csproj, no .sln)
    ├── Program.cs              # entry: OmniDriverLocator BEFORE MainForm is JIT'd, crash logging
    ├── Driver/                 # OmniDriver wrapper, acquisition thread -> see Driver/AGENTS.md
    ├── Analysis/               # pure math + stores (no driver, no UI types)
    ├── Io/                     # CSV, method files, live recording, PNG/PDF export
    ├── Localization/           # L.cs + embedded Strings_ko.txt (Korean UI text)
    ├── Ui/                     # MainForm + custom-drawn charts -> see Ui/AGENTS.md
    ├── build.ps1 / publish.ps1 # MSBuild x86 build; publish stages ../UV-VIS_meas/ + .lnk shortcuts
    └── Assets/AppIcon.ico
```
Namespace root `UvVisMeasurement.*`; assembly name `UV-VIS_meas` (differs on purpose: `-` illegal in namespaces).

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Absorbance / drift formula | `Analysis/SpectrumMath.cs` | `StoredReferenceAbsorbance`, `DriftFactor`, `DriftCorrectedAbsorbance`, `Resample` |
| Smoothing / baseline / peaks / %T | `Analysis/SpectrumProcessing.cs` | display only; never reaches stored data or CSV |
| Molar absorptivity, unit parsing | `Analysis/BeerLambert.cs` | a bare number means mM / mm, as the field labels say |
| Method files (`.uvmethod`) | `Io/MethodFile.cs` | carries no dark/white reference, by design |
| Reading a saved CSV back | `Io/SpectrumImport.cs` | overlay traces |
| Crash-safe timed recording | `Io/TimedSeriesRecorder.cs` | same format as `CsvExporter.SaveTimedSeries` |
| PNG / PDF report | `Io/ReportExporter.cs` | one-page PDF written here, no third-party library |
| Korean / English text | `Localization/L.cs` + `Strings_ko.txt` | `L.T(key, english)`; a test checks every key |
| Menu, toolbar, theme, step guide | `Ui/MainForm.Shell.cs`, `Ui/Theme.cs`, `Ui/Icons.cs` | |
| Dark / white reference storage | `Analysis/ReferenceStore.cs` | keyed by spectrometer index; `EnsurePixelCount` wipes on geometry change |
| Timed buffer | `Analysis/TimedSeriesStore.cs` | `Capacity` = 5000, drops oldest; UI reads only `TimedSeriesSnapshot` |
| CSV output | `Io/CsvExporter.cs` | format spec in docs/README.md sec 8 |
| Driver load failure | `Driver/OmniDriverLocator.cs`, `Program.cs` | |
| New UI control / chart behavior | `Ui/` | |
| Build path probing | `UV-VIS_meas.csproj` | OmniDriverHome property chain |
| Installer, signing, EULA | `installer/` | `build-installer.ps1` reads the version out of the built EXE |
| Company / copyright | `Properties/AssemblyInfo.cs` | one source; About box and installer read it, never hardcode it |

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
| `MainForm` | partial Form | Ui/MainForm*.cs | split by concern: Shell, DeviceTable, Charts, Hardware, Actions, Analysis, UserSettings |
| `SpectrumProcessing` | static class | Analysis/SpectrumProcessing.cs | Savitzky-Golay, baselines, peak prominence |
| `L` | static class | Localization/L.cs | `L.T(key, english)`, language switch at run time |

## CONVENTIONS
- **x86 pinned everywhere** (`PlatformTarget`, build.ps1 `/p:Platform=x86`). AnyCPU/x64 = `BadImageFormatException` (mixed-mode C++/CLI vendor DLL).
- OmniDriver reference is `Private=False`: loaded from OOI_HOME at runtime so native `OmniDriver32.dll` resolves. Never copy it next to the EXE.
- No targeting pack required: csproj falls back to `%windir%\Microsoft.NET\Framework\v4.0.30319` when Reference Assemblies are absent.
- Every new `.cs` file must be added to the explicit `<Compile Include>` list in the csproj (old-style project, no globbing).
- Units live in identifiers: `IntegrationUs`, `TriggerDelayUs`, `MinIntervalMs`. Trigger delay crosses to the driver as **counts**, not µs.
- Report what the driver **confirmed**, not what was requested (outcome structs, CSV headers, UI).
- CSV: `InvariantCulture`, X as `F4`, values as `G8`, `UTF8Encoding(false)` (no BOM), `#` comment header with settings snapshot, filename `<Sample>_<kind>_<yyyyMMdd_HHmmss>.csv`.
- User-facing UI strings go through `L.T("key", "English")`; the Korean is in `Localization/Strings_ko.txt`. A test fails on a missing or unused key.
- CSV column names, `#` header keys, "Copy Spectrum Data" headers and driver terms (Input/Output/LOW/HIGH, trigger-mode names) stay English in both languages: they are machine-read or must match the vendor documentation.
- Operator docs / README / publish `사용법.txt`: Korean.
- Long explanatory XML/inline comments explain *why* (often citing measured behavior); preserve them when editing.

## ANTI-PATTERNS (THIS PROJECT)
- Do not hardcode per-model capability tables (trigger modes, delay scale, pixel counts). Read from the driver at runtime; docs sec 3 explains why (the driver echoes any trigger mode, Ocean FX 1 count = 1 µs vs Flame-S 0.5 µs).
- Do not use `setCorrectForElectricalDark`: dark correction = subtract the captured Dark spectrum (Ocean FX has 0 dark pixels).
- Do not reference OmniDriver types from `Program.Main` or anything JIT'd before `OmniDriverLocator.Initialize` (hence `[NoInlining] RunApplication`).
- Do not change CSV column order / formats casually; files are meant to be re-verifiable as `A = -log10((S-D)/(W-D))` from stored columns.
- Do not let display processing (smoothing, baseline, %T) reach the stored spectra or the exported CSV: a processed number must never be mistaken for a measured one.
- Do not put dark/white references into a method file.
- Do not write a constant like `{app}` inside an Inno Setup `{ ... }` Pascal comment: the first closing brace ends the comment and the rest is parsed as code. Use `//`.
- Do not hardcode the version in the installer: `build-installer.ps1` reads it from the EXE and passes `/DAppVersion`.
- Do not construct `AcquisitionEngine` before `InitializeComponent`: it captures `SynchronizationContext.Current`, and earlier there is none, so its events would run off the UI thread.
- Do not commit binaries, `ooi_home.txt`, `error.log`, the vendor PDF, or `/UV-VIS_meas/` (all gitignored).

## COMMANDS
```powershell
cd UV-VIS_measurement_code
.\build.ps1                                   # Release x86 -> bin\Release\UV-VIS_meas.exe
.\build.ps1 -Configuration Debug
.\build.ps1 -OmniDriverHome "C:\path\to\OOI_HOME"
.\publish.ps1 [-NoDesktopShortcut] [-NoShortcuts]   # build + stage ..\UV-VIS_meas\ + shortcuts

cd ..\installer
.\build-installer.ps1                         # -> dist\UV-VIS_meas-Setup-<version>.exe
.\build-installer.ps1 -CertificateFile signing.pfx -CertificatePassword (Read-Host -AsSecureString)
```
Inno Setup 6 is required for the installer (`winget install -e --id JRSoftware.InnoSetup`);
it may land in `%LOCALAPPDATA%\Programs\Inno Setup 6`, which the build script already probes.
```powershell
dotnet test tests\UvVisMeasurement.Tests    # 68 tests, no OmniDriver needed
```
Demo mode (`UV-VIS_meas.exe --demo`) runs two simulated spectrometers, so the UI can be checked without hardware.

## NOTES
- Build fails at `VerifyOmniDriverHome` if OmniDriver is not installed; you cannot compile on a machine without it (or without `/p:OmniDriverHome`).
- Runtime state: `%LOCALAPPDATA%\UV-VIS_meas\error.log` (crash log), `ooi_home.txt` (manually picked OOI_HOME).
- `App.config` redirects all `NETOmniDriver-NET40` versions to 2.45.0.0.
- `AllowUnsafeBlocks` exists only for `Ui/HeatmapControl.cs` (BitmapData.Scan0 raster).
- Hardware-integration code: behavior is verified on real Ocean FX / Flame-S units; cite docs/README.md before "simplifying" odd-looking driver workarounds.
