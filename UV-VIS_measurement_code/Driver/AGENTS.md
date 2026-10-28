# Driver/ - OmniDriver boundary + acquisition thread

## OVERVIEW
Only layer that touches OmniDriver. Earned its own file: distinct domain (vendor SDK + threading), highest reference centrality, two 700-900 line hotspots (`AcquisitionEngine.cs` 920, `SpectrometerSession.cs` 705).

## WHERE TO LOOK
| Task | File | Notes |
|------|------|-------|
| New engine command (UI -> device) | `AcquisitionEngine.cs` | add `CommandKind`, public method that `Enqueue`s, handler in `WorkerLoop` |
| New event to UI | `AcquisitionEngine.cs` | EventArgs classes live in the same file; publish via `Post`/`PostFrame` |
| New driver call | `SpectrometerSession.cs` | wrap with Safe* helpers; check `isFeatureSupported*` first |
| Trigger mode / GPIO / delay changes | `SpectrometerSession.ApplyHardwareChange`, `ApplyGpio`, `ApplyTriggerDelay` | return `HardwareApplyOutcome` |
| Capability model shown in Hardware tab | `HardwareCapabilities.cs` | `MeasurementModeOption`, `GpioChannelInfo`, `HardwareChange` |
| Per-device row settings | `DeviceSettings.cs` | measured?, beam role, integration, delay |
| Drift pair validity | `MeasurementSettings.IsInCorrectionPair`, `DescribeDriftBlocker` | |
| OOI_HOME discovery order | `OmniDriverLocator.EnumerateCandidates` | override file -> `OOI_HOME` env -> HKLM (both views) -> Program Files -> PATH -> BaseDirectory |

## THREADING CONTRACT
- `AcquisitionEngine.WorkerLoop` is the **only** thread that calls OmniDriver (`IsBackground`, MTA). `SpectrometerSession` is single-thread by design: never call it from UI code.
- UI -> worker: `ConcurrentQueue<Command>` + `AutoResetEvent`. Worker -> UI: captured `SynchronizationContext.Post`. No locks, no `Control.Invoke`.
- `PostFrame` coalesces: if a frame post is still pending (`Interlocked.CompareExchange` on `_pendingFramePosts`) the new frame is dropped. Do not "fix" this into a queue; the UI would fall behind.
- Settings cross threads only as copies (`MeasurementSettings.Clone()` / `DeviceSettings.Clone()`) inside `ApplySettings` / `StartMeasurement`.
- `Dispose` joins for 5 s then abandons a thread stuck in a USB call. Do not assume the worker exited.
- `Post`/`PostFrame` callbacks return early once `_shuttingDown` is set: posts queued before the form closed otherwise run against disposed charts, and a disposed `Chart` throws NullReferenceException, not ObjectDisposedException. Keep that check first.

## DRIVER QUIRKS (measured, see docs/README.md sec 3)
- `setExternalTriggerDelay` takes **counts**; convert with `triggerDelayCountsToMicroseconds()` per device, clamp to min/max/increment, and report the applied value.
- No API lists supported trigger modes. `ReadMeasurementModes` probes each `TriggerMode` and **must restore the previous mode**; Flame-S accepts even nonexistent mode 7.
- No API reports GPIO per-pin direction support: pins up to `getNumberOfPins()` are treated as bidirectional.
- `setMuxBit` 2nd arg is `short`; `_2`-suffixed LabVIEW names map to channel overloads `(index, 0, value)`.
- `getFeatureControllerGPIO` returns a `NETGPIO` object: cache it and `Dispose` on close.
- `ApplyGpio` writes direction **and** mux together; `ApplyGpioLevel` depends on it.
- Flame-S `getSpectrum()` returns fractional counts; Ocean FX integer counts. Treat as `double[]`.

## ANTI-PATTERNS
- Never let OmniDriver types leak into `Analysis/` or `Io/`; they take arrays + plain settings.
- Never write unconfirmed hardware state into settings: UI updates its model only from `HardwareApplied`.
- Swallowing catch blocks (`SafeInt`, `SafeString`, `SafeDispose`, `RestoreMode`) are intentional at the vendor boundary; don't add new silent catches outside it.
- `OmniDriverLocator` runs before any OmniDriver type is JIT'd. Do not add OmniDriver-typed fields/signatures to it.
- References (`HasCapturableFrame`): Capture Dark/White is rejected when the displayed frame's `DeviceIndex` != `ActiveIndex`; keep that guard.
