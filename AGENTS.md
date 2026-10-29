# PROJECT KNOWLEDGE BASE

**Generated:** 2026-10-28 (commit 4162c70) - **Updated:** 2026-10-29 (latest release v2.3.1, tag on 7f579d4; user guide added in 3391867)
**Branch:** main

## CURRENT STATE (read first when resuming)
- Latest release **v2.3.1** on GitHub (MIT, unsigned installer + SHA256SUMS). `main` is clean; every feature branch is merged.
- Open work is listed in `docs/TODO.md` (standard-sample absorbance, real fluorescence, real 150% display, other models, signing, OmniDriver EOL). Nothing is half-done in code.
- Docs: operators read `README.md` (user-facing: install, quick start) and `docs/USER_GUIDE.md`; `docs/README.md` is the reference manual (F1); developers read `docs/DEVELOPMENT.md`. README and USER_GUIDE also exist as `.en.md`, `.ja.md`, `.zh-CN.md` (Korean is the source; ja/zh quote the English UI labels because the app has no ja/zh UI). Update every language when behaviour or UI changes.
- Local only (git-excluded): `실행파일\` holds `v2.0.0_구버전`, `v2.3.1_신버전`, `설치파일\`; the desktop shortcut points at the v2.3.1 copy. Refresh it after a release with `publish.ps1 -TargetDir ..\실행파일\v<ver>_신버전 -NoShortcuts`.
- Workflow the user expects: plan + approval before multi-step changes, a branch per change cut from `origin/main`, PR into `main`; the user usually merges (the agent merges only when asked/approved for that step).

## OVERVIEW
C# WinForms (.NET Framework 4.8, C# 7.3, **x86 only**) UV-VIS measurement app for Ocean Optics / Ocean Insight spectrometers via the vendor OmniDriver .NET assembly (`NETOmniDriver-NET40.dll`). Spectrum capture, dark/white-reference absorbance, dual-spectrometer lamp-drift correction, timed series + heatmap, CSV export.

## STRUCTURE
```
UV-VIS_measurement/
├── docs/USER_GUIDE.md          # Korean step-by-step guide (install, absorbance, fluorescence, timed) with demo-mode screenshots in docs/images/guide/
├── docs/README.md              # Korean reference manual (F1 opens it) - also the design-rationale doc (sec 3, 8, 9)
├── docs/TODO.md                # deferred checks (lamp-on test, real 150%, other models, signing, OmniDriver EOL)
├── LICENSE                     # MIT; publish.ps1 ships it as LICENSE.txt
├── CHANGELOG.md                # Korean, Keep a Changelog; one entry per GitHub release
├── tests/UvVisMeasurement.Tests/  # MSTest, net48, no OmniDriver needed
├── installer/                  # Inno Setup script, build+sign script, Korean reference translation of the licence
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
| Single running instance | `Program.cs` (`SingleInstanceMutexName`) | a second launch activates the first window and exits; OmniDriver lets one process own a device |
| New UI control / chart behavior | `Ui/` | |
| Build path probing | `UV-VIS_meas.csproj` | OmniDriverHome property chain |
| Installer, signing, licence page | `installer/` | `build-installer.ps1` reads the version out of the built EXE |
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
- Do not reintroduce a proprietary EULA or bundle OmniDriver: the program is MIT open source and OmniDriver stays a separate install under Ocean Optics' own terms.
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
.\build-installer.ps1 -CertificateThumbprint <sha1>   # optional; releases ship unsigned (README explains SmartScreen)
```
New code-signing certificates (since 2023-06) live on a token / cloud HSM, so `-CertificateFile` (.pfx) only fits old certificates.
Inno Setup 6 is required for the installer (`winget install -e --id JRSoftware.InnoSetup`);
it may land in `%LOCALAPPDATA%\Programs\Inno Setup 6`, which the build script already probes.
```powershell
dotnet test tests\UvVisMeasurement.Tests    # 68 tests, no OmniDriver needed
```
Demo mode (`UV-VIS_meas.exe --demo`) runs two simulated spectrometers, so the UI can be checked without hardware.

## RELEASE PROCEDURE
1. Work on a branch cut from `origin/main`, PR straight into `main`. **Never stack PRs on feature branches**: PR #3, #5 and #6 were merged into their parent branches and `main` silently missed whole phases (fixed by PR #4 and #7).
2. Bump `AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion` together in `Properties/AssemblyInfo.cs`; add a `CHANGELOG.md` entry.
3. After merge, on `main`: `dotnet test`, then `installer\build-installer.ps1` (version is read from the EXE).
4. Silent install check: `Setup.exe /VERYSILENT /SP- /NOICONS /DIR=%TEMP%\x`, inspect files, `unins000.exe /VERYSILENT`.
5. `SHA256SUMS.txt` next to the setup in `dist\`; `git tag -a vX.Y.Z`; `gh release create vX.Y.Z <setup> <SHA256SUMS.txt> --notes-file ...` (Korean notes: OmniDriver first, SmartScreen, checksum). Re-download and compare the hash.

## VERIFICATION RECIPES (what worked on this project)
- **UI without hardware**: `bin\Release\UV-VIS_meas.exe --demo`. For scripted checks, load the EXE in-process from **32-bit** PowerShell (`C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe -STA`), set `SpectrometerBackend.Current` to `Simulator` (enum value 1) by reflection, `Activator.CreateInstance(MainForm, true)`, `Show()`, pump `Application.DoEvents()`, drive fields (`chkRun`, `btnCaptureDark`, `btnCaptureReference`, `tabMain.SelectedIndex`) by reflection, capture with `DrawToBitmap`.
- **Real hardware in-process**: call `OmniDriverLocator.Initialize(out err)` first, then the same as above with the default backend.
- **150% scaling emulation**: before creating the form set `System.Windows.Forms.DpiHelper` static fields `deviceDpi=144`, `logicalToDeviceUnitsScalingFactor=1.5`, `isInitialized=true`. This found the startup crash and the clipped analysis bar fixed in c12af6f. It is emulation only; a real 150% display is still unverified.
- **UI Automation limits**: WinForms TabPages are not exposed as TabItems (switch tabs in-process); Inno Setup buttons are Panes, match exact names like `다음(N)`; `DrawToBitmap` paints ComboBoxes empty (artifact - read `.Text` instead).
- **Before any run**: make sure no `UV-VIS_meas.exe` is alive (an orphan holds the spectrometer and the new instance now just exits); back up `%LOCALAPPDATA%\UV-VIS_meas\settings.xml` and restore it afterwards; compare `error.log` size before/after.
- **CSV re-check**: rows with a value must satisfy `A = -log10((S-D)/(W-D))` within ~1e-4 (G8 rounding); empty A cells are expected where the ratio is <= 0. Parse empty as NaN, not 0.
- Verified hardware so far: Flame-S S/N FLMS18996 (`getName` USB2000+), 2048 px, 179.9-870.7 nm, OmniDriver 2.56, 50 us -> 100 counts. 2026-10-29 with the lamp on (emits 430-680 nm only): 10 ms / 10 averages gives ~40k counts, blank absorbance 0 +/- 0.0013 OD, SATURATED flag works, live recording file intact. No standard sample measured yet.
- **Close-while-measuring race**: reproduce with the Sample/Dark/White tab open (`MainTabIndex` 1), start measurement, `CloseMainWindow` after 1.5-6 s, repeat ~12 times, count new `error.log` entries. Fixed by dropping engine posts once `_shuttingDown` is set.
- UIA: the timed-record button is renamed while recording ("Recording - click to stop"), so a second `ByName 'Record timed datasets'` does not stop it.

## DOCS AND SCREENSHOTS
- `docs/USER_GUIDE.md` images live in `docs/images/guide/` (`01`-`13` app, `i01`-`i06` installer); the English UI set used by the en/ja/zh guides is `docs/images/guide-en/` (same names, `L.SetLanguage('en')`, setup `/LANG=english`). Retake both when the UI changes; the guides quote the cursor read-out numbers from `08_analysis.png`.
- App screenshots: load the EXE in-process from 32-bit PowerShell with the Simulator backend and `L.SetLanguage('ko')`; after `form.Show()` toggle `form.Visible` off/on (a hidden parent process hides the first window); capture with `PrintWindow(form.Handle, dc, 2)` only - `CopyFromScreen` once captured the user's desktop instead.
- Realistic demo absorbance: Dark with the simulated shutter closed (GPIO 0 Output + HIGH via `RequestHardwareChange`), White when the simulator's sample strength is minimal (session private `Now`, t mod 90 = 67.5 s), sample at maximum (22.5 s) -> ~0.70 OD at 520 nm.
- Installer screenshots: run setup with `/LANG=korean /DIR=<neutral path>`, drive with exact Inno button names, uninstall afterwards. Never show paths containing the Windows user name in public images.
- The demo cannot produce real fluorescence; the guide says so. Replace `10_fluorescence.png` once a real emission spectrum is measured.

## OPEN ITEMS (deferred by the user 2026-10-28; details and checklists in `docs/TODO.md` - GitHub issues are disabled on the repo)
- Standard-sample measurement (lamp-on blank done 2026-10-29; known absorbance / molar absorptivity still unchecked).
- Fluorescence on real hardware (never measured; the guide's fluorescence screenshot is demo data).
- A real 150% display.
- Spectrometer models other than Flame-S (Ocean FX in particular).
- Code signing, if users ask (Certum open-source or OV on a token); OmniDriver availability/terms from Ocean Optics; OceanDirect migration (OmniDriver is discontinued).

## NOTES
- Build fails at `VerifyOmniDriverHome` if OmniDriver is not installed; you cannot compile on a machine without it (or without `/p:OmniDriverHome`).
- Runtime state: `%LOCALAPPDATA%\UV-VIS_meas\error.log` (crash log), `ooi_home.txt` (manually picked OOI_HOME).
- `App.config` redirects all `NETOmniDriver-NET40` versions to 2.45.0.0.
- `AllowUnsafeBlocks` exists only for `Ui/HeatmapControl.cs` (BitmapData.Scan0 raster).
- Hardware-integration code: behavior is verified on real Ocean FX / Flame-S units; cite docs/README.md before "simplifying" odd-looking driver workarounds.
