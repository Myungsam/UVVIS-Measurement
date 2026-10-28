using System;
using System.Collections.Generic;
using OmniDriver;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// Owns the OmniDriver wrapper and the per-spectrometer feature controllers.
    ///
    /// OmniDriver requires that a given spectrometer is only ever touched from a single
    /// thread (see "Using OmniDriver with Multithreaded Applications" in the programming
    /// manual), so every method here is expected to be called from the acquisition thread.
    /// </summary>
    internal sealed class SpectrometerSession : ISpectrometerSession
    {
        /// <summary>Ocean FX exposes a single channel; the manual's channel overloads are used throughout.</summary>
        public const int ChannelIndex = 0;

        private NETWrapper _wrapper;
        private NETWrapperExtensions _extensions;
        private readonly Dictionary<int, NETGPIO> _gpioControllers = new Dictionary<int, NETGPIO>();
        private readonly Dictionary<int, NETExternalTriggerDelay> _triggerDelayControllers =
            new Dictionary<int, NETExternalTriggerDelay>();

        private DeviceInfo[] _devices = new DeviceInfo[0];
        private bool _disposed;

        public DeviceInfo[] Devices
        {
            get { return _devices; }
        }

        public int DeviceCount
        {
            get { return _devices.Length; }
        }

        public string ApiVersion { get; private set; }

        public string DriverName
        {
            get { return "OmniDriver " + ApiVersion; }
        }

        public bool IsSimulated
        {
            get { return false; }
        }

        /// <summary>CreateWrapper + openAllSpectrometers, then reads the static description of each device.</summary>
        public int Open()
        {
            if (_wrapper != null)
                throw new InvalidOperationException("The session is already open.");

            _wrapper = new NETWrapper();
            _wrapper.CreateWrapper();
            ApiVersion = _wrapper.getApiVersion();

            int count = _wrapper.openAllSpectrometers();
            if (count < 0)
                count = 0;

            _extensions = _wrapper.getWrapperExtensions();

            var devices = new List<DeviceInfo>(count);
            for (int index = 0; index < count; index++)
                devices.Add(ReadDeviceInfo(index));

            _devices = devices.ToArray();
            return count;
        }

        private DeviceInfo ReadDeviceInfo(int index)
        {
            var info = new DeviceInfo
            {
                Index = index,
                Name = _wrapper.getName(index) ?? string.Empty,
                SerialNumber = _wrapper.getSerialNumber(index) ?? string.Empty,
                FirmwareModel = SafeString(() => _wrapper.getFirmwareModel(index)),
                FirmwareVersion = SafeString(() => _wrapper.getFirmwareVersion(index)),
                PixelCount = _wrapper.getNumberOfPixels(index, ChannelIndex),
                DarkPixelCount = SafeInt(() => _wrapper.getNumberOfDarkPixels(index)),
                MaximumIntensity = _wrapper.getMaximumIntensity(index),
                MinimumIntegrationTimeUs = _wrapper.getMinimumIntegrationTime(index),
                MaximumIntegrationTimeUs = _wrapper.getMaximumIntegrationTime(index),
                Wavelengths = _wrapper.getWavelengths(index, ChannelIndex) ?? new double[0],
                GpioSupported = _wrapper.isFeatureSupportedGPIO(index) != 0,
                TriggerDelaySupported = _wrapper.isFeatureSupportedExternalTriggerDelay(index) != 0
            };

            // getFeatureControllerXxx() always hands back an object even when the feature is
            // absent, so the isFeatureSupported check above has to gate the access.
            if (info.GpioSupported)
            {
                NETGPIO gpio = GetGpioController(index);
                info.GpioPinCount = gpio.getNumberOfPins();
                info.GpioTotalBits = SafeInt(() => gpio.getTotalGPIOBits());
                info.GpioChannels = ReadGpioChannels(index, info);
            }

            info.MeasurementModes = ReadMeasurementModes(index);
            info.MeasurementModesUnverified = AllAccepted(info.MeasurementModes);

            if (info.TriggerDelaySupported)
            {
                NETExternalTriggerDelay delay = GetTriggerDelayController(index);
                info.TriggerDelayMinimumCounts = delay.getExternalTriggerDelayMinimum();
                info.TriggerDelayMaximumCounts = delay.getExternalTriggerDelayMaximum();
                info.TriggerDelayIncrementCounts = Math.Max(1, delay.getExternalTriggerDelayIncrement());
                info.MicrosecondsPerCount = ReadMicrosecondsPerCount(delay, info);
            }

            return info;
        }

        /// <summary>
        /// setExternalTriggerDelay() takes device counts, not microseconds. The scale is read
        /// back from triggerDelayCountsToMicroseconds() so the UI can work in microseconds.
        /// </summary>
        private static double ReadMicrosecondsPerCount(NETExternalTriggerDelay delay, DeviceInfo info)
        {
            int probeCounts = Math.Min(1000, Math.Max(1, info.TriggerDelayMaximumCounts));
            try
            {
                double microseconds = delay.triggerDelayCountsToMicroseconds(probeCounts);
                if (microseconds > 0.0)
                    return microseconds / probeCounts;

                microseconds = delay.triggerDelayCountsToMicroseconds(1);
                if (microseconds > 0.0)
                    return microseconds;
            }
            catch (Exception)
            {
                // Fall through to the 1:1 default below.
            }

            return 1.0;
        }

        // ------------------------------------------------------- capability queries

        /// <summary>
        /// Which measurement (external trigger) modes this spectrometer accepts.
        ///
        /// The API has no enumeration call, so the only question that can be put to the
        /// driver is "will you take this mode?": each candidate is written with
        /// setExternalTriggerMode(), then getExternalTriggerMode() and getLastException()
        /// are used to see whether it stuck. The mode in force beforehand is restored, so
        /// the device is left exactly as it was found.
        ///
        /// Measured limit: on a Flame-S (OmniDriver 2.56) the driver echoes back any value
        /// written, including numbers that are not modes at all, and reports "no
        /// exception". On such a device this cannot narrow the list down, which is what
        /// <see cref="DeviceInfo.MeasurementModesUnverified"/> tells the operator.
        /// </summary>
        public MeasurementModeOption[] ReadMeasurementModes(int index)
        {
            var options = new List<MeasurementModeOption>(TriggerModes.All.Length);

            int previous = SafeInt(() => _wrapper.getExternalTriggerMode(index, ChannelIndex));
            try
            {
                foreach (TriggerMode mode in TriggerModes.All)
                    options.Add(OfferMode(index, mode));
            }
            finally
            {
                try { _wrapper.setExternalTriggerMode(index, ChannelIndex, previous); }
                catch (Exception) { /* nothing better to do; the next apply rewrites it */ }
            }

            return options.ToArray();
        }

        private MeasurementModeOption OfferMode(int index, TriggerMode mode)
        {
            var option = new MeasurementModeOption { Mode = mode, Label = TriggerModes.Label(mode) };

            string before = DriverExceptionText();
            try
            {
                _wrapper.setExternalTriggerMode(index, ChannelIndex, (int)mode);

                string after = DriverExceptionText();
                if (after != null && !string.Equals(after, before, StringComparison.Ordinal))
                {
                    option.Supported = false;
                    option.Reason = after;
                }
                else if (_wrapper.getExternalTriggerMode(index, ChannelIndex) != (int)mode)
                {
                    option.Supported = false;
                    option.Reason = "the driver reports a different mode back";
                }
            }
            catch (Exception ex)
            {
                option.Supported = false;
                option.Reason = ex.Message;
            }

            return option;
        }

        private static bool AllAccepted(MeasurementModeOption[] options)
        {
            for (int i = 0; i < options.Length; i++)
                if (!options[i].Supported)
                    return false;

            return true;
        }

        /// <summary>
        /// The GPIO channels the driver reports for one spectrometer, with the direction,
        /// mux routing and level it currently holds. Nothing is written.
        /// </summary>
        public GpioChannelInfo[] ReadGpioChannels(int index)
        {
            return ReadGpioChannels(index, RequireDevice(index));
        }

        private GpioChannelInfo[] ReadGpioChannels(int index, DeviceInfo info)
        {
            if (!info.GpioSupported)
                return new GpioChannelInfo[0];

            NETGPIO gpio = GetGpioController(index);
            int pins = Math.Max(0, SafeInt(() => gpio.getNumberOfPins()));
            int bits = Math.Max(0, SafeInt(() => gpio.getTotalGPIOBits()));

            NETBitSet directions = SafeBits(() => gpio.getDirectionBits());
            NETBitSet mux = SafeBits(() => gpio.getMuxBits());
            NETBitSet values = SafeBits(() => gpio.getValueBits());

            var channels = new List<GpioChannelInfo>(Math.Max(pins, bits));
            for (int pin = 0; pin < Math.Max(pins, bits); pin++)
            {
                bool usable = pin < pins;
                channels.Add(new GpioChannelInfo
                {
                    Index = pin,
                    Usable = usable,

                    // No per-pin capability call exists. Every pin the driver exposes takes
                    // setDirectionBit(pin, 0|1), so a usable pin can be either; a bit past
                    // the pin count can be neither.
                    InputSupported = usable,
                    OutputSupported = usable,

                    Direction = usable ? Bit(directions, pin) : -1,
                    Mux = usable ? Bit(mux, pin) : -1,
                    Value = usable ? Bit(values, pin) : -1,
                    Note = usable
                        ? null
                        : "the driver counts " + bits + " GPIO bits but exposes only " + pins + " pins"
                });
            }

            return channels.ToArray();
        }

        /// <summary>
        /// Applies one hardware setting to the spectrometer the change names, and only
        /// reports success once the driver has read the new value back. On a refusal the
        /// previous value is written again so the device is never left in a state nobody
        /// asked for.
        /// </summary>
        public HardwareApplyOutcome ApplyHardwareChange(HardwareChange change)
        {
            DeviceInfo info;
            string wrongDevice = TryResolve(change, out info);
            if (wrongDevice != null)
                return HardwareApplyOutcome.Failed(wrongDevice, false);

            switch (change.Kind)
            {
                case HardwareChangeKind.MeasurementMode:
                    return ApplyMeasurementMode(info, change.Mode);

                case HardwareChangeKind.GpioLevel:
                    return ApplyGpioLevel(info, change.Pin, change.High);

                case HardwareChangeKind.ReadGpio:
                    return HardwareApplyOutcome.Success;   // the caller re-reads afterwards

                default:
                    return ApplyGpioDirection(info, change.Pin, change.Output);
            }
        }

        /// <summary>
        /// Finds the spectrometer a change is for and refuses if the device sitting at that
        /// index is not the one it was raised against. Returns null when it matches, or the
        /// reason to report when it does not.
        /// </summary>
        private string TryResolve(HardwareChange change, out DeviceInfo info)
        {
            info = null;

            if (change.DeviceIndex < 0 || change.DeviceIndex >= _devices.Length)
                return "spectrometer [" + change.DeviceIndex + "] is no longer connected";

            DeviceInfo candidate = _devices[change.DeviceIndex];
            if (!string.IsNullOrEmpty(change.DeviceIdentity) &&
                !string.Equals(candidate.Identity, change.DeviceIdentity, StringComparison.Ordinal))
            {
                // The device list moved under this command - refusing beats writing to
                // whichever spectrometer inherited the index.
                return "spectrometer " + change.DeviceIdentity + " is no longer at index " +
                       change.DeviceIndex + " (that index now holds " + candidate.Identity + ")";
            }

            info = candidate;
            return null;
        }

        private HardwareApplyOutcome ApplyMeasurementMode(DeviceInfo info, TriggerMode mode)
        {
            int previous = SafeInt(() => _wrapper.getExternalTriggerMode(info.Index, ChannelIndex));
            string before = DriverExceptionText();

            try
            {
                _wrapper.setExternalTriggerMode(info.Index, ChannelIndex, (int)mode);
            }
            catch (Exception ex)
            {
                return HardwareApplyOutcome.Failed(ex.Message, false);
            }

            string after = DriverExceptionText();
            if (after != null && !string.Equals(after, before, StringComparison.Ordinal))
                return HardwareApplyOutcome.Failed(after, RestoreMode(info.Index, previous));

            int readback = SafeInt(() => _wrapper.getExternalTriggerMode(info.Index, ChannelIndex));
            if (readback != (int)mode)
            {
                return HardwareApplyOutcome.Failed(
                    "the driver reports mode " + readback + " after being asked for " + (int)mode,
                    RestoreMode(info.Index, previous));
            }

            // A spectrum that was started under the previous mode says nothing about the
            // new one, so it is dropped rather than shown as the first frame after the
            // restart.
            try { _wrapper.flushSpectrum(info.Index); }
            catch (Exception) { /* not fatal - at worst one stale frame */ }

            return HardwareApplyOutcome.Success;
        }

        private bool RestoreMode(int index, int previous)
        {
            try
            {
                _wrapper.setExternalTriggerMode(index, ChannelIndex, previous);
                return _wrapper.getExternalTriggerMode(index, ChannelIndex) == previous;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Points one pin at general purpose I/O and sets its direction, then reads the
        /// direction bits back. The pin's level is deliberately not written: choosing a
        /// direction must not put a level on the connector by itself.
        /// </summary>
        private HardwareApplyOutcome ApplyGpioDirection(DeviceInfo info, int pin, bool output)
        {
            if (!info.GpioSupported)
                return HardwareApplyOutcome.Failed("this spectrometer does not expose GPIO", false);

            if (pin < 0 || pin >= info.GpioPinCount)
            {
                return HardwareApplyOutcome.Failed(
                    "GPIO " + pin + " is not one of the " + info.GpioPinCount + " pins the driver exposes", false);
            }

            NETGPIO gpio = GetGpioController(info.Index);
            short wanted = (short)(output ? 1 : 0);
            int previous = Bit(SafeBits(() => gpio.getDirectionBits()), pin);

            try
            {
                gpio.setMuxBit(pin, 0);            // 0 = general purpose I/O
                gpio.setDirectionBit(pin, wanted);
            }
            catch (Exception ex)
            {
                return HardwareApplyOutcome.Failed(ex.Message, false);
            }

            if (Bit(SafeBits(() => gpio.getDirectionBits()), pin) == wanted)
                return HardwareApplyOutcome.Success;

            bool restored = false;
            if (previous >= 0)
            {
                try
                {
                    gpio.setDirectionBit(pin, (short)previous);
                    restored = Bit(SafeBits(() => gpio.getDirectionBits()), pin) == previous;
                }
                catch (Exception) { restored = false; }
            }

            return HardwareApplyOutcome.Failed("the driver did not report the new direction back", restored);
        }

        /// <summary>
        /// Drives one pin low or high, through the same <see cref="ApplyGpio"/> the GPIO box
        /// on the parameter panel used to call: route to general purpose I/O, make it an
        /// output, then write the level. Success is only reported once the driver reads the
        /// level back.
        /// </summary>
        private HardwareApplyOutcome ApplyGpioLevel(DeviceInfo info, int pin, bool high)
        {
            if (!info.GpioSupported)
                return HardwareApplyOutcome.Failed("this spectrometer does not expose GPIO", false);

            if (pin < 0 || pin >= info.GpioPinCount)
            {
                return HardwareApplyOutcome.Failed(
                    "GPIO " + pin + " is not one of the " + info.GpioPinCount + " pins the driver exposes", false);
            }

            NETGPIO gpio = GetGpioController(info.Index);
            int previous = SafeInt(() => gpio.getValueBit(pin));

            try
            {
                ApplyGpio(info.Index, pin, high);
            }
            catch (Exception ex)
            {
                return HardwareApplyOutcome.Failed(ex.Message, false);
            }

            int wanted = high ? 1 : 0;
            if (SafeInt(() => gpio.getValueBit(pin)) == wanted)
                return HardwareApplyOutcome.Success;

            bool restored = false;
            if (previous == 0 || previous == 1)
            {
                try
                {
                    gpio.setValueBit(pin, (short)previous);
                    restored = SafeInt(() => gpio.getValueBit(pin)) == previous;
                }
                catch (Exception) { restored = false; }
            }

            return HardwareApplyOutcome.Failed("the driver did not report the new level back", restored);
        }

        private static int Bit(NETBitSet bits, int index)
        {
            if (bits == null)
                return -1;

            try { return bits.get(index); }
            catch (Exception) { return -1; }
        }

        private static NETBitSet SafeBits(Func<NETBitSet> read)
        {
            try { return read(); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// getLastException() reports the driver's most recent problem and stays there, so
        /// a call is only judged to have failed when the text changed. "no exception" is
        /// the driver's way of saying there is nothing to report.
        /// </summary>
        private string DriverExceptionText()
        {
            string text = SafeString(() => _wrapper.getLastException());
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return text.IndexOf("no exception", StringComparison.OrdinalIgnoreCase) >= 0 ? null : text;
        }

        /// <summary>
        /// GPIO setup in the order required by the driver: route the pin to general purpose
        /// I/O, make it an output, then drive the requested level. The manual notes these
        /// calls can each cost up to 200 ms, so they are only issued when the operator
        /// actually changes a pin - never per frame.
        ///
        /// This is the original GPIO backend and is still the only place a level is
        /// written; <see cref="ApplyGpioLevel"/> wraps it with the read-back check.
        /// </summary>
        public bool ApplyGpio(int index, int pin, bool high)
        {
            DeviceInfo info = RequireDevice(index);
            if (!info.GpioSupported)
                return false;

            if (pin < 0 || pin >= info.GpioPinCount)
                throw new ArgumentOutOfRangeException("pin",
                    "GPIO pin must be between 0 and " + (info.GpioPinCount - 1) + " on " + info.Name + ".");

            NETGPIO gpio = GetGpioController(index);
            gpio.setMuxBit(pin, 0);                        // 0 = general purpose I/O
            gpio.setDirectionBit(pin, 1);                  // 1 = output
            gpio.setValueBit(pin, (short)(high ? 1 : 0));  // drive the pin
            return true;
        }

        /// <summary>Converts microseconds to counts, aligns to the device increment and applies it.</summary>
        public AppliedTriggerDelay ApplyTriggerDelay(int index, double microseconds)
        {
            DeviceInfo info = RequireDevice(index);
            if (!info.TriggerDelaySupported)
                return AppliedTriggerDelay.Unsupported;

            int counts = info.TriggerDelayCountsFor(microseconds);

            NETExternalTriggerDelay delay = GetTriggerDelayController(index);
            delay.setExternalTriggerDelay(counts);

            return new AppliedTriggerDelay
            {
                Supported = true,
                Counts = counts,
                Microseconds = info.TriggerDelayMicrosecondsFor(counts)
            };
        }

        /// <summary>Integration time, averaging and trigger mode for one spectrometer.</summary>
        public void ApplyAcquisition(int index, int integrationUs, int scansToAverage, TriggerMode triggerMode,
                                     int usbTimeoutMs, bool triggerModeChanged)
        {
            int clamped = RequireDevice(index).ClampIntegrationUs(integrationUs);

            _wrapper.setIntegrationTime(index, ChannelIndex, clamped);
            _wrapper.setScansToAverage(index, ChannelIndex, Math.Max(1, scansToAverage));
            _wrapper.setExternalTriggerMode(index, ChannelIndex, (int)triggerMode);
            _wrapper.setUSBTimeout(index, usbTimeoutMs);

            // Switching away from Normal mode leaves a spectrum in flight that was not
            // produced by an external trigger; drop it so the first frame is meaningful.
            if (triggerModeChanged)
            {
                try { _wrapper.flushSpectrum(index); }
                catch (Exception) { /* not fatal - the stale frame is simply kept */ }
            }
        }

        /// <summary>
        /// Reads one spectrum. Returns null when the read timed out (waiting for a trigger)
        /// or the driver reported the spectrum as invalid.
        /// </summary>
        public double[] TryGetSpectrum(int index, out SpectrumReadStatus status)
        {
            double[] spectrum = _wrapper.getSpectrum(index, ChannelIndex);

            if (_wrapper.isUSBTimeout(index, ChannelIndex) != 0)
            {
                status = SpectrumReadStatus.Timeout;
                return null;
            }

            if (spectrum == null || spectrum.Length == 0)
            {
                status = SpectrumReadStatus.Invalid;
                return null;
            }

            if (_extensions != null && _extensions.isSpectrumValid(index, ChannelIndex) == 0)
            {
                status = SpectrumReadStatus.Invalid;
                return null;
            }

            status = SpectrumReadStatus.Ok;
            return spectrum;
        }

        public bool IsSaturated(int index)
        {
            try { return _wrapper.isSaturated(index, ChannelIndex) != 0; }
            catch (Exception) { return false; }
        }

        public string LastDriverException
        {
            get
            {
                try { return _wrapper == null ? null : _wrapper.getLastException(); }
                catch (Exception) { return null; }
            }
        }

        public DeviceInfo RequireDevice(int index)
        {
            if (index < 0 || index >= _devices.Length)
                throw new ArgumentOutOfRangeException("index",
                    "Spectrometer index " + index + " is not available (" + _devices.Length + " connected).");

            return _devices[index];
        }

        private NETGPIO GetGpioController(int index)
        {
            NETGPIO controller;
            if (!_gpioControllers.TryGetValue(index, out controller))
            {
                controller = _wrapper.getFeatureControllerGPIO(index);
                _gpioControllers[index] = controller;
            }
            return controller;
        }

        private NETExternalTriggerDelay GetTriggerDelayController(int index)
        {
            NETExternalTriggerDelay controller;
            if (!_triggerDelayControllers.TryGetValue(index, out controller))
            {
                controller = _wrapper.getFeatureControllerExternalTriggerDelay(index);
                _triggerDelayControllers[index] = controller;
            }
            return controller;
        }

        private static string SafeString(Func<string> read)
        {
            try { return read() ?? string.Empty; }
            catch (Exception) { return string.Empty; }
        }

        private static int SafeInt(Func<int> read)
        {
            try { return read(); }
            catch (Exception) { return 0; }
        }

        /// <summary>
        /// Releases every OmniDriver object: the GPIO and ExternalTriggerDelay feature
        /// controllers first, then closeAllSpectrometers() and the wrapper itself.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (NETGPIO gpio in _gpioControllers.Values)
                SafeDispose(gpio);
            _gpioControllers.Clear();

            foreach (NETExternalTriggerDelay delay in _triggerDelayControllers.Values)
                SafeDispose(delay);
            _triggerDelayControllers.Clear();

            SafeDispose(_extensions);
            _extensions = null;

            if (_wrapper != null)
            {
                try { _wrapper.closeAllSpectrometers(); }
                catch (Exception) { }

                SafeDispose(_wrapper);
                _wrapper = null;
            }

            _devices = new DeviceInfo[0];
        }

        private static void SafeDispose(IDisposable disposable)
        {
            if (disposable == null)
                return;

            try { disposable.Dispose(); }
            catch (Exception) { }
        }
    }
}
