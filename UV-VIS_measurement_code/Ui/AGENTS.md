# Ui/ - WinForms screen + custom-drawn plots

## OVERVIEW
Largest code mass (~6.2k lines in 6 files). Earned its own file: distinct domain with GDI+/DataVisualization workarounds that look removable but are not (rationale: docs/README.md sec 9).

## WHERE TO LOOK
| Task | File | Notes |
|------|------|-------|
| Any MainForm feature | `MainForm.cs` | banner sections: setup ~177, device table ~504, engine events ~1192, charts ~1258, settings plumbing ~1465, hardware config ~1481, run toggle ~1663, button handlers ~1762, teardown ~2080 |
| Control layout / fields | `MainForm.Designer.cs` | designer-generated; `panelHardware` is empty here and filled at runtime |
| Spectrum chart zoom/pan/paint | `SpectrumChart.cs` (1110) | `Chart` subclass, `BeginDataUpdate`/`EndDataUpdate`, `AutoScaleFromData` |
| Axis auto/manual ranges, inline edit box | `AxisScaling.cs` | `AxisScale`, `PlotAxis`, `InlineNumberBox` |
| 2D absorbance map | `HeatmapControl.cs` (741) | unsafe `LockBits` raster into 32bppPArgb cache, viridis |
| Hardware Settings tab | `HardwareSettingsView.cs` (924) | per-device GroupBox, mode combo, GPIO grid |

## DATA FLOW
- Engine events -> handlers in the engine-events section (already on UI thread via SynchronizationContext).
- Settings edits -> `QueueSettingsPush` (300 ms debounce) -> `PushSettings` -> `_engine.ApplySettings(BuildSettings())`. `BuildSettings` returns the shared `_settings`; the engine clones it.
- Hardware edits -> `RequestHardwareChange` -> `_engine.ApplyHardware`; model updated **only** in `OnHardwareApplied`.
- Timed chart + heatmap refresh on `_slowRefresh` (250 ms), visible tab only, gated by `_timedDirty`; read from `TimedSeriesSnapshot`, never the live store.
- Capture Dark/White use `_lastFrame` (the displayed spectrum), not a fresh read.

## CONVENTIONS
- `_camelCase` fields, `OnXxx` handlers, `RequestXxx` = hand-off to engine, `Describe*` = text builders.
- Chart updates: overwrite existing `DataPoint`s in place (`MainForm.Plot`); NaN/Inf -> empty point (line break).
- Axis ranges are always explicit numbers owned by `AxisScale`; never set `Axis.Minimum = NaN` (library writes back computed values, hiding the request).
- `_suppressHeatSync` guards feedback loops between heat-scale inputs and `HeatmapControl` (heatmap is source of truth).

## ANTI-PATTERNS
- Do not remove `SpectrumChart`'s paint guard or persistent offscreen bitmap: Chart enumerates `Series.Points` in `OnPaint` -> "Collection was modified" crash; skipping paint without re-blitting shows black flicker.
- Do not replace in-place `DataPoint` updates with `Points.Clear()` + `AddXY()`.
- Do not build `HardwareSettingsView` controls before the tab is visible (unselected TabPage stays visible -> tabs overdraw).
- GPIO grid: call `EndEdit()` and update cells in place; never `Rows.Clear()` while editing (red-X grid). When swapping an Output cell to a combo, call `Unlock()` (inherits row read-only).
- Do not hand-edit `InitializeComponent` in ways the designer cannot round-trip; put runtime layout in `ConfigureInputs` / `CaptureLayoutBaseline`.
- Do not show modal dialogs during chart data updates (pumps messages into a re-entrant paint).
