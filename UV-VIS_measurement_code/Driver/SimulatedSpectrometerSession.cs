using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// Two synthetic spectrometers for demos, training and tests on a PC without hardware.
    ///
    /// [0] "Flame-S (simulated)" looks through a sample whose two absorption bands
    /// (420 nm, 520 nm) rise and fall with a 90 s period, so the timed series and the 2D
    /// map have something to show. [1] "Ocean FX (simulated)" sees the bare lamp and works
    /// as the reference beam. Both share one lamp that drifts by a few percent, which the
    /// live reference correction removes.
    ///
    /// GPIO pin 0 is the lamp shutter: set it to Output / HIGH on the Hardware Settings
    /// tab to block the light for Capture Dark. Any trigger mode other than Normal never
    /// receives a trigger and times out, as real hardware with nothing wired would.
    /// </summary>
    internal sealed class SimulatedSpectrometerSession : ISpectrometerSession
    {
        public const int ShutterPin = 0;

        private const int GpioPins = 8;
        private const int GpioBits = 10;
        private const double SamplePeriodSeconds = 90.0;

        private sealed class Device
        {
            public DeviceInfo Info;
            public bool SeesSample;
            public double CountsPerMsAtPeak;
            public TriggerMode RejectedMode = (TriggerMode)(-1);

            public double[] LampShape;
            public double[] DarkLevel;
            public double[] SampleBands;

            public int IntegrationUs;
            public int ScansToAverage = 1;
            public TriggerMode Mode = TriggerMode.Normal;
            public int UsbTimeoutMs = 2000;
            public int TriggerDelayCounts;

            public readonly int[] Direction = new int[GpioPins];
            public readonly int[] Mux = new int[GpioPins];
            public readonly int[] Value = new int[GpioPins];

            public bool Saturated;
        }

        private readonly Random _random;
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly bool _realTime;
        private Device[] _simulated = new Device[0];
        private DeviceInfo[] _devices = new DeviceInfo[0];
        private string _lastError;

        public SimulatedSpectrometerSession() : this(Environment.TickCount, true)
        {
        }

        /// <param name="seed">Noise seed, fixed in tests so results repeat.</param>
        /// <param name="realTime">False skips the exposure wait so tests run fast.</param>
        public SimulatedSpectrometerSession(int seed, bool realTime)
        {
            _random = new Random(seed);
            _realTime = realTime;
        }

        public DeviceInfo[] Devices
        {
            get { return _devices; }
        }

        public int DeviceCount
        {
            get { return _devices.Length; }
        }

        public string DriverName
        {
            get { return "Simulator (demo mode)"; }
        }

        public bool IsSimulated
        {
            get { return true; }
        }

        public string LastDriverException
        {
            get { return _lastError; }
        }

        /// <summary>Seconds of simulated time; the sample and the lamp follow this clock.</summary>
        public Func<double> Clock { get; set; }

        private double Now
        {
            get { return Clock != null ? Clock() : _clock.Elapsed.TotalSeconds; }
        }

        public int Open()
        {
            _clock.Restart();

            _simulated = new[]
            {
                CreateDevice(0, "Flame-S (simulated)", "USB2000+", "SIM-FLMS-0001", 2048, 16, 178.8, 891.2,
                    1000, 65535000, 65535, 0.5, 65535, true, 1800.0, 1000.0, (TriggerMode)(-1)),
                CreateDevice(1, "Ocean FX (simulated)", "Flame-X-F", "SIM-OFX-0002", 2136, 0, 315.5, 1031.2,
                    10, 10000000, 1000000, 1.0, 65535, false, 900.0, 1600.0, TriggerMode.SingleShot)
            };

            var infos = new List<DeviceInfo>(_simulated.Length);
            foreach (Device device in _simulated)
            {
                device.Info.MeasurementModes = ReadMeasurementModes(device);
                device.Info.MeasurementModesUnverified = device.RejectedMode < 0;
                device.Info.GpioChannels = ReadGpioChannels(device);
                infos.Add(device.Info);
            }

            _devices = infos.ToArray();
            return _devices.Length;
        }

        private Device CreateDevice(int index, string firmwareModel, string name, string serial, int pixels,
                                    int darkPixels, double firstNm, double lastNm, int minIntegrationUs,
                                    int maxIntegrationUs, int delayMaxCounts, double usPerCount,
                                    int fullScale, bool seesSample, double countsPerMs, double darkLevel,
                                    TriggerMode rejectedMode)
        {
            var wavelengths = new double[pixels];
            for (int i = 0; i < pixels; i++)
            {
                double x = (double)i / (pixels - 1);
                // Real calibrations are slightly non-linear in pixel index.
                wavelengths[i] = firstNm + (lastNm - firstNm) * (0.97 * x + 0.03 * x * x);
            }

            var info = new DeviceInfo
            {
                Index = index,
                Name = name,
                FirmwareModel = firmwareModel,
                FirmwareVersion = "sim-1.0",
                SerialNumber = serial,
                PixelCount = pixels,
                DarkPixelCount = darkPixels,
                Wavelengths = wavelengths,
                MaximumIntensity = fullScale,
                MinimumIntegrationTimeUs = minIntegrationUs,
                MaximumIntegrationTimeUs = maxIntegrationUs,
                GpioSupported = true,
                GpioPinCount = GpioPins,
                GpioTotalBits = GpioBits,
                TriggerDelaySupported = true,
                TriggerDelayMinimumCounts = 0,
                TriggerDelayMaximumCounts = delayMaxCounts,
                TriggerDelayIncrementCounts = 1,
                MicrosecondsPerCount = usPerCount
            };

            var device = new Device
            {
                Info = info,
                SeesSample = seesSample,
                CountsPerMsAtPeak = countsPerMs,
                RejectedMode = rejectedMode,
                IntegrationUs = Math.Max(minIntegrationUs, 1000),
                LampShape = new double[pixels],
                DarkLevel = new double[pixels],
                SampleBands = new double[pixels]
            };

            double peak = 0.0;
            for (int i = 0; i < pixels; i++)
            {
                device.LampShape[i] = LampAndDetector(wavelengths[i]);
                peak = Math.Max(peak, device.LampShape[i]);

                // A fixed per-pixel pattern on top of the offset, like a real CCD.
                device.DarkLevel[i] = darkLevel + 6.0 * Math.Sin(i * 0.37) + 3.0 * Math.Sin(i * 1.91);

                device.SampleBands[i] = 0.8 * Gaussian(wavelengths[i], 520.0, 35.0) +
                                        0.4 * Gaussian(wavelengths[i], 420.0, 25.0);
            }

            for (int i = 0; i < pixels; i++)
                device.LampShape[i] /= peak;

            return device;
        }

        /// <summary>Deuterium + halogen lamp seen through a silicon detector's response.</summary>
        private static double LampAndDetector(double nm)
        {
            const double h = 6.62607015e-34, c = 2.99792458e8, k = 1.380649e-23, temperature = 3000.0;
            double m = nm * 1e-9;
            double planck = 1.0 / (Math.Pow(m, 5) * (Math.Exp(h * c / (m * k * temperature)) - 1.0));
            double halogen = planck / 1.0e28;
            double deuterium = 0.35 * Gaussian(nm, 240.0, 45.0);
            double response = Gaussian(nm, 600.0, 260.0);
            return (halogen + deuterium) * response + 1e-4;
        }

        private static double Gaussian(double x, double centre, double sigma)
        {
            double z = (x - centre) / sigma;
            return Math.Exp(-0.5 * z * z);
        }

        private static double LampDrift(double seconds)
        {
            return 1.0 + 0.03 * Math.Sin(2.0 * Math.PI * seconds / 47.0) + 0.01 * Math.Sin(2.0 * Math.PI * seconds / 13.0);
        }

        /// <summary>Scale of the sample bands; the absorbance at 520 nm is 0.8 times this.</summary>
        public static double SampleStrength(double seconds)
        {
            return 0.5 + 0.45 * Math.Sin(2.0 * Math.PI * seconds / SamplePeriodSeconds);
        }

        // ---------------------------------------------------------------- capabilities

        private static MeasurementModeOption[] ReadMeasurementModes(Device device)
        {
            var options = new MeasurementModeOption[TriggerModes.All.Length];
            for (int i = 0; i < options.Length; i++)
            {
                TriggerMode mode = TriggerModes.All[i];
                bool rejected = mode == device.RejectedMode;
                options[i] = new MeasurementModeOption
                {
                    Mode = mode,
                    Label = TriggerModes.Label(mode),
                    Supported = !rejected,
                    Reason = rejected ? "not supported by this simulated model" : null
                };
            }
            return options;
        }

        public GpioChannelInfo[] ReadGpioChannels(int index)
        {
            return ReadGpioChannels(Find(index));
        }

        private static GpioChannelInfo[] ReadGpioChannels(Device device)
        {
            var channels = new GpioChannelInfo[GpioBits];
            for (int pin = 0; pin < GpioBits; pin++)
            {
                bool usable = pin < GpioPins;
                channels[pin] = new GpioChannelInfo
                {
                    Index = pin,
                    Usable = usable,
                    InputSupported = usable,
                    OutputSupported = usable,
                    Direction = usable ? device.Direction[pin] : -1,
                    Mux = usable ? device.Mux[pin] : -1,
                    Value = usable ? device.Value[pin] : -1,
                    Note = usable
                        ? (pin == ShutterPin ? "simulated lamp shutter: Output + HIGH blocks the light" : null)
                        : "the simulator counts " + GpioBits + " GPIO bits but exposes only " + GpioPins + " pins"
                };
            }
            return channels;
        }

        public HardwareApplyOutcome ApplyHardwareChange(HardwareChange change)
        {
            if (change.DeviceIndex < 0 || change.DeviceIndex >= _simulated.Length)
                return HardwareApplyOutcome.Failed("spectrometer [" + change.DeviceIndex + "] is no longer connected", false);

            Device device = _simulated[change.DeviceIndex];
            if (!string.IsNullOrEmpty(change.DeviceIdentity) &&
                !string.Equals(device.Info.Identity, change.DeviceIdentity, StringComparison.Ordinal))
            {
                return HardwareApplyOutcome.Failed("spectrometer " + change.DeviceIdentity + " is no longer at index " +
                                                   change.DeviceIndex, false);
            }

            switch (change.Kind)
            {
                case HardwareChangeKind.MeasurementMode:
                    if (change.Mode == device.RejectedMode)
                    {
                        _lastError = TriggerModes.Label(change.Mode) + " is not supported by this simulated model";
                        return HardwareApplyOutcome.Failed(_lastError, true);
                    }
                    device.Mode = change.Mode;
                    return HardwareApplyOutcome.Success;

                case HardwareChangeKind.ReadGpio:
                    return HardwareApplyOutcome.Success;

                case HardwareChangeKind.GpioDirection:
                    if (!ValidPin(change.Pin))
                        return InvalidPin(change.Pin);
                    device.Mux[change.Pin] = 0;
                    device.Direction[change.Pin] = change.Output ? 1 : 0;
                    return HardwareApplyOutcome.Success;

                default:
                    if (!ValidPin(change.Pin))
                        return InvalidPin(change.Pin);
                    device.Mux[change.Pin] = 0;
                    device.Direction[change.Pin] = 1;
                    device.Value[change.Pin] = change.High ? 1 : 0;
                    return HardwareApplyOutcome.Success;
            }
        }

        private static bool ValidPin(int pin)
        {
            return pin >= 0 && pin < GpioPins;
        }

        private static HardwareApplyOutcome InvalidPin(int pin)
        {
            return HardwareApplyOutcome.Failed("GPIO " + pin + " is not one of the " + GpioPins + " pins the driver exposes", false);
        }

        // ---------------------------------------------------------------- acquisition

        public AppliedTriggerDelay ApplyTriggerDelay(int index, double microseconds)
        {
            Device device = Find(index);
            device.TriggerDelayCounts = device.Info.TriggerDelayCountsFor(microseconds);

            return new AppliedTriggerDelay
            {
                Supported = true,
                Counts = device.TriggerDelayCounts,
                Microseconds = device.Info.TriggerDelayMicrosecondsFor(device.TriggerDelayCounts)
            };
        }

        public void ApplyAcquisition(int index, int integrationUs, int scansToAverage, TriggerMode triggerMode,
                                     int usbTimeoutMs, bool triggerModeChanged)
        {
            Device device = Find(index);
            device.IntegrationUs = device.Info.ClampIntegrationUs(integrationUs);
            device.ScansToAverage = Math.Max(1, scansToAverage);
            device.Mode = triggerMode;
            device.UsbTimeoutMs = Math.Max(1, usbTimeoutMs);
        }

        public double[] TryGetSpectrum(int index, out SpectrumReadStatus status)
        {
            Device device = Find(index);

            if (device.Mode != TriggerMode.Normal)
            {
                Wait(device.UsbTimeoutMs);
                status = SpectrumReadStatus.Timeout;
                return null;
            }

            double integrationMs = device.IntegrationUs / 1000.0;
            double exposureMs = integrationMs * device.ScansToAverage;
            if (exposureMs > device.UsbTimeoutMs)
            {
                Wait(device.UsbTimeoutMs);
                status = SpectrumReadStatus.Timeout;
                return null;
            }

            Wait(exposureMs);

            double t = Now;
            bool shutterClosed = device.Direction[ShutterPin] == 1 && device.Value[ShutterPin] == 1;
            double lamp = shutterClosed ? 0.0 : LampDrift(t);
            double strength = SampleStrength(t);
            double fullScale = device.Info.MaximumIntensity;
            double noiseScale = 1.0 / Math.Sqrt(device.ScansToAverage);

            var counts = new double[device.Info.PixelCount];
            bool saturated = false;

            for (int i = 0; i < counts.Length; i++)
            {
                double transmission = device.SeesSample ? Math.Pow(10.0, -strength * device.SampleBands[i]) : 1.0;
                double signal = device.LampShape[i] * device.CountsPerMsAtPeak * integrationMs * lamp * transmission;
                double noise = Math.Sqrt(Math.Max(signal, 0.0) + 16.0) * noiseScale;
                double value = device.DarkLevel[i] + signal + noise * NextGaussian();

                if (value >= fullScale)
                {
                    value = fullScale;
                    saturated = true;
                }

                counts[i] = Math.Max(0.0, value);
            }

            device.Saturated = saturated;
            status = SpectrumReadStatus.Ok;
            return counts;
        }

        public bool IsSaturated(int index)
        {
            return Find(index).Saturated;
        }

        public DeviceInfo RequireDevice(int index)
        {
            return Find(index).Info;
        }

        private Device Find(int index)
        {
            if (index < 0 || index >= _simulated.Length)
                throw new ArgumentOutOfRangeException("index",
                    "Spectrometer index " + index + " is not available (" + _simulated.Length + " connected).");

            return _simulated[index];
        }

        private void Wait(double milliseconds)
        {
            if (_realTime && milliseconds >= 1.0)
                Thread.Sleep((int)Math.Min(milliseconds, int.MaxValue));
        }

        private double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        public void Dispose()
        {
            _simulated = new Device[0];
            _devices = new DeviceInfo[0];
        }
    }
}
