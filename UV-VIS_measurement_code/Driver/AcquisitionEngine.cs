using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using UvVisMeasurement.Analysis;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// Runs every OmniDriver call on one dedicated thread, as the driver requires, and
    /// publishes results back to the UI through a captured SynchronizationContext.
    /// The UI never touches the wrapper directly; it posts commands into a queue.
    /// </summary>
    internal sealed class AcquisitionEngine : IDisposable
    {
        private enum CommandKind
        {
            OpenDevices,
            ApplySettings,
            ApplyHardware,
            Start,
            Stop,
            CaptureDark,
            CaptureReference,
            ClearReferences,
            Shutdown
        }

        private sealed class Command
        {
            public CommandKind Kind;
            public MeasurementSettings Settings;

            /// <summary>The displayed frame a capture command works from.</summary>
            public SpectrumFrame Frame;

            /// <summary>The device configuration an ApplyHardware command carries.</summary>
            public HardwareChange Hardware;
        }

        private readonly ConcurrentQueue<Command> _commands = new ConcurrentQueue<Command>();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly SynchronizationContext _sync;
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private readonly Stopwatch _sessionTimer = new Stopwatch();
        private readonly ReferenceStore _references = new ReferenceStore();

        /// <summary>Keys of status messages already shown, so they cannot repeat per frame.</summary>
        private readonly HashSet<string> _reportedOnce = new HashSet<string>(StringComparer.Ordinal);

        private readonly Func<ISpectrometerSession> _sessionFactory;
        private Thread _thread;
        private ISpectrometerSession _session;
        private MeasurementSettings _settings = new MeasurementSettings();
        private volatile bool _running;
        private volatile bool _shuttingDown;

        /// <summary>
        /// Mode last written to each spectrometer, so the flush that follows a mode change
        /// is not repeated on every settings push. Per device because the mode is.
        /// </summary>
        private readonly Dictionary<int, TriggerMode> _appliedTriggerModes = new Dictionary<int, TriggerMode>();

        private int _pendingFramePosts;

        public event EventHandler<DevicesOpenedEventArgs> DevicesOpened;
        public event EventHandler<FrameEventArgs> FrameReady;
        public event EventHandler<StatusEventArgs> StatusChanged;
        public event EventHandler<SettingsAppliedEventArgs> SettingsApplied;
        public event EventHandler<HardwareAppliedEventArgs> HardwareApplied;
        public event EventHandler<ReferenceChangedEventArgs> ReferenceChanged;
        public event EventHandler<EngineFailedEventArgs> Failed;
        public event EventHandler RunStateChanged;

        public AcquisitionEngine(Func<ISpectrometerSession> sessionFactory)
        {
            if (sessionFactory == null)
                throw new ArgumentNullException("sessionFactory");

            _sessionFactory = sessionFactory;
            _sync = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        public bool IsRunning
        {
            get { return _running; }
        }

        public void Start()
        {
            if (_thread != null)
                return;

            _thread = new Thread(WorkerLoop)
            {
                Name = "OmniDriver acquisition",
                IsBackground = true
            };
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        public void OpenDevices()
        {
            Enqueue(new Command { Kind = CommandKind.OpenDevices });
        }

        public void ApplySettings(MeasurementSettings settings)
        {
            Enqueue(new Command { Kind = CommandKind.ApplySettings, Settings = settings.Clone() });
        }

        /// <summary>
        /// Changes one device setting through the stop / apply / restart sequence. The
        /// acquisition is only restarted if it was running when the change arrived.
        /// </summary>
        public void ApplyHardware(HardwareChange change)
        {
            Enqueue(new Command { Kind = CommandKind.ApplyHardware, Hardware = change });
        }

        public void StartMeasurement(MeasurementSettings settings)
        {
            Enqueue(new Command { Kind = CommandKind.Start, Settings = settings.Clone() });
        }

        public void StopMeasurement()
        {
            Enqueue(new Command { Kind = CommandKind.Stop });
        }

        /// <summary>Stores the dark spectrum taken straight from the frame already on screen.</summary>
        public void CaptureDark(SpectrumFrame displayed)
        {
            Enqueue(new Command { Kind = CommandKind.CaptureDark, Frame = displayed });
        }

        /// <summary>Stores the white reference taken straight from the frame already on screen.</summary>
        public void CaptureWhiteReference(SpectrumFrame displayed)
        {
            Enqueue(new Command { Kind = CommandKind.CaptureReference, Frame = displayed });
        }

        /// <summary>Drops the dark and white reference of the spectrometer currently selected.</summary>
        public void ClearReferences()
        {
            Enqueue(new Command { Kind = CommandKind.ClearReferences });
        }

        private void Enqueue(Command command)
        {
            if (_shuttingDown && command.Kind != CommandKind.Shutdown)
                return;

            _commands.Enqueue(command);
            _wake.Set();
        }

        private void WorkerLoop()
        {
            try
            {
                while (true)
                {
                    Command command;
                    while (_commands.TryDequeue(out command))
                    {
                        if (command.Kind == CommandKind.Shutdown)
                            return;

                        try
                        {
                            Execute(command);
                        }
                        catch (Exception ex)
                        {
                            SetRunning(false);
                            ReportFailure(ex);
                        }
                    }

                    if (!_running)
                    {
                        _wake.WaitOne(100);
                        continue;
                    }

                    try
                    {
                        AcquireFrame();
                    }
                    catch (Exception ex)
                    {
                        SetRunning(false);
                        ReportFailure(ex);
                        continue;
                    }

                    WaitForMinimumInterval();
                }
            }
            catch (Exception ex)
            {
                ReportFailure(ex);
            }
            finally
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }
            }
        }

        private void Execute(Command command)
        {
            switch (command.Kind)
            {
                case CommandKind.OpenDevices:
                    ExecuteOpenDevices();
                    break;

                case CommandKind.ApplySettings:
                    _settings = command.Settings;
                    ExecuteApplySettings();
                    break;

                case CommandKind.ApplyHardware:
                    ExecuteApplyHardware(command.Hardware);
                    break;

                case CommandKind.Start:
                    _settings = command.Settings;
                    // A new run reports its own problems from scratch.
                    _reportedOnce.Clear();
                    ExecuteApplySettings();
                    _sessionTimer.Restart();
                    SetRunning(true);
                    Report("Measurement running.");
                    break;

                case CommandKind.Stop:
                    SetRunning(false);
                    _sessionTimer.Stop();
                    Report("Measurement stopped.");
                    break;

                case CommandKind.CaptureDark:
                    ExecuteCaptureDark(command.Frame);
                    break;

                case CommandKind.CaptureReference:
                    ExecuteCaptureReference(command.Frame);
                    break;

                case CommandKind.ClearReferences:
                    ExecuteClearReferences();
                    break;
            }
        }

        private void ExecuteOpenDevices()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }

            ISpectrometerSession session = _sessionFactory();
            int count = session.Open();
            _session = session;

            _appliedTriggerModes.Clear();

            Post(() =>
            {
                EventHandler<DevicesOpenedEventArgs> handler = DevicesOpened;
                if (handler != null)
                    handler(this, new DevicesOpenedEventArgs(session.Devices, session.DriverName, session.IsSimulated));
            });

            Report(count == 0
                ? "No spectrometer found. Check the USB connection and the OmniDriver installation."
                : count + " spectrometer(s) opened (" + session.DriverName + ").");
        }

        private void ExecuteApplySettings()
        {
            ISpectrometerSession session = RequireSession();
            MeasurementSettings settings = _settings;

            // Only the spectrometers that are actually read get configured: the selected
            // one, plus the reference channel when the drift correction needs it. Devices
            // that are never read would just cost USB round trips on every settings change.
            var applied = new List<AppliedDeviceDelay>(2);

            foreach (DeviceSettings device in DevicesInUse(settings, session.DeviceCount))
            {
                _references.For(device.Index).EnsurePixelCount(session.RequireDevice(device.Index).PixelCount);

                AppliedTriggerDelay delay = session.ApplyTriggerDelay(device.Index, device.TriggerDelayUs);

                TriggerMode lastMode;
                bool triggerModeChanged = !_appliedTriggerModes.TryGetValue(device.Index, out lastMode)
                                          || lastMode != device.TriggerMode;

                session.ApplyAcquisition(device.Index, device.IntegrationUs, settings.ScansToAverage,
                    device.TriggerMode, settings.UsbTimeoutMsFor(device.IntegrationMs), triggerModeChanged);

                applied.Add(new AppliedDeviceDelay { Index = device.Index, Delay = delay });

                _appliedTriggerModes[device.Index] = device.TriggerMode;
            }

            AppliedDeviceDelay[] delays = applied.ToArray();
            bool beamsAssigned = settings.BeamsAssigned;

            Post(() =>
            {
                EventHandler<SettingsAppliedEventArgs> handler = SettingsApplied;
                if (handler != null)
                    handler(this, new SettingsAppliedEventArgs(delays, beamsAssigned));
            });

            RaiseReferenceChanged();
        }

        /// <summary>
        /// stop -> apply -> restart, as the device configuration requires.
        ///
        /// The acquisition is only restarted when it was running to begin with: a change
        /// made while the operator had it stopped must not start it. If the setting is
        /// refused it is put back, and the run only resumes once the device is in a state
        /// that is actually known - otherwise it stays stopped and says why.
        /// </summary>
        private void ExecuteApplyHardware(HardwareChange change)
        {
            ISpectrometerSession session = RequireSession();

            if (change.DeviceIndex < 0 || change.DeviceIndex >= session.DeviceCount)
            {
                PostHardwareResult(change, HardwareApplyOutcome.Failed(
                    "spectrometer [" + change.DeviceIndex + "] is no longer connected", false), false, null);
                return;
            }

            // There is one acquisition loop, and it only reads the spectrometer under "Use"
            // plus the reference channel behind it. Changing a setting on any other
            // spectrometer cannot disturb the frames being taken, so it is applied without
            // interrupting them - stopping the run to configure a device it never touches
            // would cost the operator data for nothing.
            bool wasRunning = _running && TouchesAcquisition(session, change);
            if (wasRunning)
            {
                SetRunning(false);
                Report("Stopping the measurement to apply " + change.Describe() + "...");
            }

            HardwareApplyOutcome outcome;
            try
            {
                outcome = session.ApplyHardwareChange(change);
            }
            catch (Exception ex)
            {
                outcome = HardwareApplyOutcome.Failed(ex.Message, false);
            }

            if (outcome.Applied && change.Kind == HardwareChangeKind.MeasurementMode)
            {
                // Keep the settings path in step, so the next push does not treat this as a
                // fresh mode change and flush a spectrum for nothing.
                _appliedTriggerModes[change.DeviceIndex] = change.Mode;
            }

            // What the device reports now, not what was asked for.
            GpioChannelInfo[] channels = SafeReadGpio(session, change.DeviceIndex);

            bool restarted = wasRunning && (outcome.Applied || outcome.Restored);
            if (restarted)
                SetRunning(true);

            PostHardwareResult(change, outcome, restarted, channels);

            if (wasRunning && !restarted)
            {
                Report("The measurement is stopped: " + change.Describe() + " failed and the previous " +
                       "setting could not be restored either. Check the spectrometer before restarting.");
            }
        }

        /// <summary>
        /// True when the change is aimed at a spectrometer the running acquisition actually
        /// reads. A pure read of the pin states never counts: it writes nothing.
        /// </summary>
        private bool TouchesAcquisition(ISpectrometerSession session, HardwareChange change)
        {
            if (change.Kind == HardwareChangeKind.ReadGpio)
                return false;

            foreach (DeviceSettings device in DevicesInUse(_settings, session.DeviceCount))
            {
                if (device.Index == change.DeviceIndex)
                    return true;
            }

            return false;
        }

        private static GpioChannelInfo[] SafeReadGpio(ISpectrometerSession session, int index)
        {
            try { return session.ReadGpioChannels(index); }
            catch (Exception) { return null; }
        }

        private void PostHardwareResult(HardwareChange change, HardwareApplyOutcome outcome,
                                        bool restarted, GpioChannelInfo[] channels)
        {
            var args = new HardwareAppliedEventArgs(change, outcome, restarted, channels);

            Post(() =>
            {
                EventHandler<HardwareAppliedEventArgs> handler = HardwareApplied;
                if (handler != null)
                    handler(this, args);
            });

            Report(args.Describe());
        }

        /// <summary>
        /// The spectrometers this configuration actually touches: the one under Use, and
        /// the reference channel behind it when the drift correction is switched on.
        /// </summary>
        private static IEnumerable<DeviceSettings> DevicesInUse(MeasurementSettings settings, int deviceCount)
        {
            DeviceSettings active = settings.Active;
            if (active != null && active.Index >= 0 && active.Index < deviceCount)
                yield return active;

            if (!settings.DriftCorrectionRequested)
                yield break;

            DeviceSettings reference = settings.Reference;
            if (reference != null && reference.Index >= 0 && reference.Index < deviceCount &&
                (active == null || reference.Index != active.Index))
            {
                yield return reference;
            }
        }

        private void AcquireFrame()
        {
            ISpectrometerSession session = RequireSession();
            MeasurementSettings settings = _settings;
            _frameTimer.Restart();

            DeviceSettings active = settings.Active;
            if (active == null || active.Index < 0 || active.Index >= session.DeviceCount)
            {
                Report("No spectrometer is ticked in the Use column - nothing to acquire.");
                Thread.Sleep(200);
                return;
            }

            SpectrumReadStatus status;
            double[] counts = session.TryGetSpectrum(active.Index, out status);
            if (counts == null)
            {
                Report(status == SpectrumReadStatus.Timeout
                    ? "Waiting for trigger on spectrometer " + active.Index + "..."
                    : "Spectrometer " + active.Index + " reported an invalid spectrum; retrying.");
                return;
            }

            DeviceInfo info = session.RequireDevice(active.Index);
            DeviceReference stored = _references.For(active.Index);

            var frame = new SpectrumFrame
            {
                DeviceIndex = active.Index,
                DeviceLabel = info.ShortLabel,
                Wavelengths = info.Wavelengths,
                Counts = counts,
                Dark = stored.Dark,
                WhiteReference = stored.WhiteReference,
                Saturated = session.IsSaturated(active.Index),
                TimestampUtc = DateTime.UtcNow,
                ElapsedSeconds = _sessionTimer.Elapsed.TotalSeconds
            };

            // The reference spectrometer is read in the same cycle so the lamp drift it
            // reports belongs to the same moment as the probe spectrum.
            AttachDriftFactor(session, settings, frame);

            if (!stored.HasWhiteReference)
                frame.Absorbance = null;
            else if (frame.DriftCorrected)
                frame.Absorbance = SpectrumMath.DriftCorrectedAbsorbance(
                    counts, stored.Dark, stored.WhiteReference, frame.DriftFactor);
            else
                frame.Absorbance = SpectrumMath.StoredReferenceAbsorbance(
                    counts, stored.Dark, stored.WhiteReference);

            PostFrame(frame);
        }

        /// <summary>
        /// Reads the reference spectrometer and puts its lamp drift factor on the probe's
        /// wavelength grid.
        ///
        /// Everything needed for the correction has to be there - the roles assigned, all
        /// four spectra captured - or the frame simply falls back to the plain stored
        /// white reference. Failing soft matters because the alternative is dropping
        /// frames whenever the reference channel hiccups.
        /// </summary>
        private void AttachDriftFactor(ISpectrometerSession session, MeasurementSettings settings,
                                       SpectrumFrame frame)
        {
            if (!settings.DriftCorrectionRequested)
                return;

            DeviceSettings reference = settings.Reference;
            if (reference == null || reference.Index < 0 || reference.Index >= session.DeviceCount ||
                reference.Index == frame.DeviceIndex)
            {
                return;
            }

            DeviceReference referenceStore = _references.Find(reference.Index);
            if (referenceStore == null || !referenceStore.HasDark || !referenceStore.HasWhiteReference)
            {
                ReportOnce("drift-missing-reference",
                    "Reference channel [" + reference.Index + "] has no dark/white reference yet - " +
                    "tick it under Use, capture both, then switch back to the probe.");
                return;
            }

            SpectrumReadStatus status;
            double[] referenceCounts = session.TryGetSpectrum(reference.Index, out status);
            if (referenceCounts == null)
            {
                ReportOnce("drift-read-failed",
                    "Reference channel [" + reference.Index + "] did not return a spectrum (" +
                    status + "); this frame uses the stored white reference only.");
                return;
            }

            DeviceInfo referenceInfo = session.RequireDevice(reference.Index);
            double[] factorOnReferenceGrid = SpectrumMath.DriftFactor(
                referenceCounts, referenceStore.Dark, referenceStore.WhiteReference);

            // The two detectors never share a grid, so the factor is interpolated onto the
            // probe's wavelengths before it can be applied pixel by pixel.
            double[] factor = SpectrumMath.Resample(
                referenceInfo.Wavelengths, factorOnReferenceGrid, frame.Wavelengths);

            frame.ReferenceDeviceIndex = reference.Index;
            frame.ReferenceDeviceLabel = referenceInfo.ShortLabel;
            frame.DriftFactor = factor;
            frame.DriftMean = SpectrumMath.FiniteMean(factor);
            frame.DriftCorrected = !double.IsNaN(frame.DriftMean);

            if (frame.DriftCorrected)
                ClearOnce("drift-missing-reference", "drift-read-failed");
        }

        /// <summary>
        /// Stores the dark spectrum of the selected spectrometer, copied straight out of
        /// the frame that is on screen.
        /// </summary>
        private void ExecuteCaptureDark(SpectrumFrame frame)
        {
            if (!HasCapturableFrame(frame, "Capture Dark"))
                return;

            DeviceReference stored = _references.For(frame.DeviceIndex);

            // EnsurePixelCount wipes the entry on a geometry change, so it runs first.
            stored.EnsurePixelCount(frame.Counts.Length);
            stored.Dark = (double[])frame.Counts.Clone();

            RaiseReferenceChanged();
            Report(string.Format(CultureInfo.InvariantCulture,
                "Dark captured from the displayed spectrum of [{0}] ({1} px, mean {2:F1} counts).",
                frame.DeviceIndex, stored.Dark.Length, Mean(stored.Dark)));
        }

        /// <summary>
        /// Stores the white reference W (I0) of the selected spectrometer, copied straight
        /// out of the frame that is on screen.
        /// </summary>
        private void ExecuteCaptureReference(SpectrumFrame frame)
        {
            if (!HasCapturableFrame(frame, "Capture White Reference"))
                return;

            DeviceReference stored = _references.For(frame.DeviceIndex);

            stored.EnsurePixelCount(frame.Counts.Length);
            stored.WhiteReference = (double[])frame.Counts.Clone();

            RaiseReferenceChanged();
            Report(string.Format(CultureInfo.InvariantCulture,
                "White reference captured from the displayed spectrum of [{0}] ({1} px, mean {2:F1} counts).{3}",
                frame.DeviceIndex, stored.WhiteReference.Length, Mean(stored.WhiteReference),
                stored.HasDark ? string.Empty : "  Capture Dark as well for a proper baseline."));
        }

        private void ExecuteClearReferences()
        {
            int index = _settings.ActiveIndex;
            if (index < 0)
            {
                Report("Clear: no spectrometer is ticked in the Use column.");
                return;
            }

            _references.Clear(index);
            RaiseReferenceChanged();
            Report("Dark and white reference cleared for spectrometer [" + index + "].");
        }

        private bool HasCapturableFrame(SpectrumFrame frame, string action)
        {
            if (frame == null || !frame.HasSpectrum)
            {
                Report(action + ": no spectrum is on screen yet - start the measurement first.");
                return false;
            }

            if (frame.DeviceIndex != _settings.ActiveIndex)
            {
                // The Use selection moved after this frame was drawn; storing it would
                // file the spectrum under the wrong spectrometer.
                Report(action + ": the displayed spectrum is from spectrometer [" + frame.DeviceIndex +
                       "] but [" + _settings.ActiveIndex + "] is now selected - wait for the next frame.");
                return false;
            }

            return true;
        }

        private static double Mean(double[] values)
        {
            if (values == null || values.Length == 0)
                return 0.0;

            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i];

            return sum / values.Length;
        }

        private void WaitForMinimumInterval()
        {
            int interval = _settings.MinIntervalMs;
            if (interval <= 0)
                return;

            while (_running && !_shuttingDown && _commands.IsEmpty)
            {
                double remaining = interval - _frameTimer.Elapsed.TotalMilliseconds;
                if (remaining <= 0.0)
                    return;

                Thread.Sleep((int)Math.Max(1.0, Math.Min(remaining, 20.0)));
            }
        }

        private ISpectrometerSession RequireSession()
        {
            if (_session == null)
                throw new InvalidOperationException("No spectrometer session is open.");

            return _session;
        }

        private void SetRunning(bool running)
        {
            if (_running == running)
                return;

            _running = running;
            Post(() =>
            {
                EventHandler handler = RunStateChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            });
        }

        private void RaiseReferenceChanged()
        {
            ReferenceSnapshot snapshot = _references.Snapshot(_settings.ActiveIndex);
            Post(() =>
            {
                EventHandler<ReferenceChangedEventArgs> handler = ReferenceChanged;
                if (handler != null)
                    handler(this, new ReferenceChangedEventArgs(snapshot));
            });
        }

        /// <summary>
        /// Status message that would otherwise repeat at frame rate. The key is forgotten
        /// again by <see cref="ClearOnce"/> once the condition clears, so a problem that
        /// comes back is reported again.
        /// </summary>
        private void ReportOnce(string key, string message)
        {
            if (_reportedOnce.Add(key))
                Report(message);
        }

        private void ClearOnce(params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
                _reportedOnce.Remove(keys[i]);
        }

        private void Report(string message)
        {
            Post(() =>
            {
                EventHandler<StatusEventArgs> handler = StatusChanged;
                if (handler != null)
                    handler(this, new StatusEventArgs(message));
            });
        }

        private void ReportFailure(Exception exception)
        {
            string driverMessage = _session == null ? null : _session.LastDriverException;

            Post(() =>
            {
                EventHandler<EngineFailedEventArgs> handler = Failed;
                if (handler != null)
                    handler(this, new EngineFailedEventArgs(exception, driverMessage));
            });
        }

        /// <summary>
        /// Frames are dropped rather than queued when the UI cannot keep up, so a slow
        /// repaint can never make the post queue grow without bound.
        /// </summary>
        private void PostFrame(SpectrumFrame frame)
        {
            if (Interlocked.CompareExchange(ref _pendingFramePosts, 1, 0) != 0)
                return;

            _sync.Post(_ =>
            {
                Interlocked.Exchange(ref _pendingFramePosts, 0);
                try
                {
                    EventHandler<FrameEventArgs> handler = FrameReady;
                    if (handler != null)
                        handler(this, new FrameEventArgs(frame));
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }, null);
        }

        private void Post(Action action)
        {
            _sync.Post(_ =>
            {
                try { action(); }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            }, null);
        }

        public void Dispose()
        {
            if (_shuttingDown)
                return;

            _shuttingDown = true;
            _running = false;

            _commands.Enqueue(new Command { Kind = CommandKind.Shutdown });
            _wake.Set();

            if (_thread != null && !_thread.Join(TimeSpan.FromSeconds(5)))
            {
                // The worker is stuck inside a blocking driver call; the session is left to
                // the process teardown rather than aborting the thread mid-USB-transfer.
                _thread = null;
            }

            _wake.Close();
        }
    }

    internal sealed class DevicesOpenedEventArgs : EventArgs
    {
        public DevicesOpenedEventArgs(DeviceInfo[] devices, string driverName, bool simulated)
        {
            Devices = devices;
            DriverName = driverName;
            Simulated = simulated;
        }

        public DeviceInfo[] Devices { get; private set; }
        public string DriverName { get; private set; }
        public bool Simulated { get; private set; }
    }

    internal sealed class FrameEventArgs : EventArgs
    {
        public FrameEventArgs(SpectrumFrame frame) { Frame = frame; }
        public SpectrumFrame Frame { get; private set; }
    }

    internal sealed class StatusEventArgs : EventArgs
    {
        public StatusEventArgs(string message) { Message = message; }
        public string Message { get; private set; }
    }

    /// <summary>
    /// Result of one stop / apply / restart. <see cref="Applied"/> is what the driver
    /// confirmed, never what was requested, so the UI can revert a control that did not
    /// take rather than leaving it showing a setting the device never accepted.
    /// </summary>
    internal sealed class HardwareAppliedEventArgs : EventArgs
    {
        public HardwareAppliedEventArgs(HardwareChange change, HardwareApplyOutcome outcome,
                                        bool acquisitionRestarted, GpioChannelInfo[] channels)
        {
            Change = change;
            Applied = outcome.Applied;
            Failure = outcome.Failure;
            PreviousRestored = outcome.Restored;
            AcquisitionRestarted = acquisitionRestarted;
            GpioChannels = channels;
        }

        public HardwareChange Change { get; private set; }
        public bool Applied { get; private set; }
        public string Failure { get; private set; }
        public bool PreviousRestored { get; private set; }
        public bool AcquisitionRestarted { get; private set; }

        /// <summary>GPIO state read back afterwards, or null when it could not be read.</summary>
        public GpioChannelInfo[] GpioChannels { get; private set; }

        public string Describe()
        {
            if (Applied)
            {
                return Change.Describe() + " applied" +
                       (AcquisitionRestarted ? " - measurement restarted." : ".");
            }

            return Change.Describe() + " failed: " + Failure +
                   (PreviousRestored ? "  The previous setting was restored." : "  It could not be restored.");
        }
    }

    internal sealed class ReferenceChangedEventArgs : EventArgs
    {
        public ReferenceChangedEventArgs(ReferenceSnapshot snapshot) { Snapshot = snapshot; }
        public ReferenceSnapshot Snapshot { get; private set; }
    }

    internal sealed class EngineFailedEventArgs : EventArgs
    {
        public EngineFailedEventArgs(Exception exception, string driverMessage)
        {
            Exception = exception;
            DriverMessage = driverMessage;
        }

        public Exception Exception { get; private set; }
        public string DriverMessage { get; private set; }
    }

    /// <summary>The trigger delay the driver actually accepted for one spectrometer.</summary>
    internal struct AppliedDeviceDelay
    {
        public int Index;
        public AppliedTriggerDelay Delay;

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0}] {1}", Index, Delay);
        }
    }

    internal sealed class SettingsAppliedEventArgs : EventArgs
    {
        public SettingsAppliedEventArgs(AppliedDeviceDelay[] triggerDelays, bool beamsAssigned)
        {
            TriggerDelays = triggerDelays ?? new AppliedDeviceDelay[0];
            BeamsAssigned = beamsAssigned;
        }

        public AppliedDeviceDelay[] TriggerDelays { get; private set; }
        public bool BeamsAssigned { get; private set; }

        public string DescribeTriggerDelays()
        {
            if (TriggerDelays.Length == 0)
                return "no spectrometer selected";

            var builder = new StringBuilder();
            for (int i = 0; i < TriggerDelays.Length; i++)
            {
                if (i > 0)
                    builder.Append("   ");
                builder.Append(TriggerDelays[i]);
            }
            return builder.ToString();
        }
    }
}
