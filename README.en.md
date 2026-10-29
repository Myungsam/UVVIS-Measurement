# UV-VIS meas

[한국어](README.md) | **English** | [日本語](README.ja.md) | [简体中文](README.zh-CN.md)

[![Release](https://img.shields.io/github/v/release/Myungsam/UVVIS-Measurement)](https://github.com/Myungsam/UVVIS-Measurement/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A Windows program for **measuring UV-VIS absorbance and fluorescence spectra** with Ocean Optics / Ocean Insight spectrometers.
Capture a dark and a white reference, insert the sample, and the absorbance spectrum appears at once; measurements are saved as CSV.
It is free, open-source software (MIT).

![Absorbance measurement](docs/images/guide-en/08_analysis.png)

## Features

- **Absorbance, transmittance and fluorescence (raw intensity)**, with a step guide for the dark / white references
- Smoothing, baseline correction, peak finding, cursor read-out and **Beer-Lambert molar absorptivity**
- **Absorbance over time** with a 2D map; rows are written to disk as they are measured, so a crash does not lose data
- Real-time lamp-drift correction with two spectrometers (dual beam)
- CSV export with the measurement conditions, overlay of saved spectra, plot as PNG, one-page A4 PDF report
- Method files to repeat a measurement under the same conditions
- Korean / English user interface, and a **demo mode** that needs no hardware

## Installation

### Requirements

| Item | Details |
|---|---|
| PC | Windows 10 / 11 (.NET Framework 4.8 is part of Windows) |
| Driver | **Ocean Optics OmniDriver**. It is not distributed with this program; install it first |
| Spectrometer | An Ocean Optics spectrometer that OmniDriver recognises, connected by USB |

### Steps

1. **Install OmniDriver** and connect the spectrometer by USB.
2. Download `UV-VIS_meas-Setup-<version>.exe` from the [latest release](https://github.com/Myungsam/UVVIS-Measurement/releases/latest).
3. Run it. If **"Windows protected your PC"** appears, click **More info → Run anyway**.
   The warning appears because this open-source build is not code-signed.
4. Choose **Install for me only** to install without administrator rights, accept the licence and click **Next** until it finishes.
5. Start **UV-VIS meas** from the Start menu.

To check the download, compare the output of this PowerShell command with the SHA-256 value on the release page:

```powershell
Get-FileHash .\UV-VIS_meas-Setup-2.3.1.exe -Algorithm SHA256
```

The [user guide, chapter 2](docs/USER_GUIDE.en.md#2-installation) shows every installer screen.

### Updating and uninstalling

- **Update**: run the new setup file; it installs over the old version and keeps your settings.
- **Uninstall**: Windows Settings → Apps, or **Uninstall UV-VIS meas** in the Start menu. Your measurement data (CSV) is not deleted.

## Quick start — absorbance

1. In the spectrometer table, tick **Use** for the spectrometer to measure, and switch the lamp on.
2. Click **Start measurement** (`F5`).
3. Close the lamp shutter or block the light path, then **Capture Dark** (`Ctrl+D`).
4. Insert a cuvette with the solvent only, then **Capture White** (`Ctrl+W`).
5. Insert the sample: the **Absorption** plot appears. **Save spectrum** (`Ctrl+S`) writes a CSV file.

Fluorescence, timed measurements, analysis tools and the file formats are explained step by step in the [user guide](docs/USER_GUIDE.en.md).

## Try it without hardware (demo mode)

Two simulated spectrometers let you practise every function, even without OmniDriver.

```powershell
& "$env:LOCALAPPDATA\Programs\UV-VIS meas\UV-VIS_meas.exe" --demo
```

Answering **No** in the "OmniDriver not found" dialog also starts the demo mode.

## Documentation

| Document | Contents |
|---|---|
| [User guide](docs/USER_GUIDE.en.md) | Installation, absorbance / fluorescence / timed measurements, saving and troubleshooting, step by step with screenshots |
| [Reference manual](docs/README.md) | Formulas, hardware settings, file formats, detailed troubleshooting (opens with `F1`; Korean only) |
| [Changelog](CHANGELOG.md) | Changes per version (Korean) |
| [Third-party notices](THIRD-PARTY-NOTICES.md) | Components used by the program, such as OmniDriver |

## Verified environment

| Item | Details |
|---|---|
| Spectrometer | Flame-S (USB2000+ family) with OmniDriver 2.56 — checked on real hardware |
| OS | Windows 11, x86 program |
| Not yet verified | Absorbance against a standard sample, real fluorescence, models other than Flame-S, a real 150 % display ([open items](docs/TODO.md), Korean) |

Other Ocean Optics models should work if OmniDriver recognises them; each device's capabilities and limits are read from the driver at run time.

## Where data and settings are stored

| What | Where |
|---|---|
| Measurement data (CSV), reports | The **Save path (folder)** chosen in the program |
| Program settings | `%LOCALAPPDATA%\UV-VIS_meas\settings.xml` |
| Error log | `%LOCALAPPDATA%\UV-VIS_meas\error.log` (please attach it when you report a problem) |

## Notice

The accuracy of a measurement depends on the spectrometer, the lamp, the dark / white references and the measurement conditions.
This program is intended for laboratory measurement and is not designed for medical diagnosis.

## Licence

Released under the [MIT License](LICENSE). Ocean Optics OmniDriver is not covered by this licence and is not distributed with the program
(see [Third-party notices](THIRD-PARTY-NOTICES.md)).

## For developers

Building from source, tests, building the installer and the source layout are described in the [developer guide](docs/DEVELOPMENT.md) (Korean).
