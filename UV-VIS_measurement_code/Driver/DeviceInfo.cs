using System;
using System.Globalization;

namespace UvVisMeasurement.Driver
{
    /// <summary>Static description of one spectrometer, read once when the device is opened.</summary>
    internal sealed class DeviceInfo
    {
        public int Index;
        public string Name = string.Empty;
        public string SerialNumber = string.Empty;
        public string FirmwareModel = string.Empty;
        public string FirmwareVersion = string.Empty;

        public int PixelCount;

        /// <summary>
        /// Optically masked pixels, from getNumberOfDarkPixels(). Zero on an Ocean FX and
        /// 16 on a Flame-S, so whether setCorrectForElectricalDark() is usable at all is a
        /// per-model fact. Dark correction here is done by subtracting a captured dark
        /// spectrum instead, which works on every model.
        /// </summary>
        public int DarkPixelCount;

        /// <summary>Wavelength of every pixel, from getWavelengths(index, channel).</summary>
        public double[] Wavelengths = new double[0];

        public int MaximumIntensity;
        public int MinimumIntegrationTimeUs;
        public int MaximumIntegrationTimeUs;

        public bool GpioSupported;
        public int GpioPinCount;

        /// <summary>
        /// getTotalGPIOBits(). Bits beyond <see cref="GpioPinCount"/> are known to the
        /// driver but are not usable pins; they are listed as unusable channels rather
        /// than hidden, so the operator can tell "not there" from "there but off limits".
        /// </summary>
        public int GpioTotalBits;

        /// <summary>
        /// One entry per GPIO channel the driver reports, read when the device was opened.
        /// Empty when the spectrometer has no GPIO feature at all.
        /// </summary>
        public GpioChannelInfo[] GpioChannels = new GpioChannelInfo[0];

        /// <summary>
        /// The measurement modes this spectrometer accepted when it was opened. Never
        /// assume the list is the same on two devices - that is the whole point of it.
        /// </summary>
        public MeasurementModeOption[] MeasurementModes = new MeasurementModeOption[0];

        /// <summary>
        /// True when every candidate mode was accepted, which on most Ocean Optics
        /// devices means the driver simply echoes whatever it is given rather than that
        /// the hardware really implements all of them. The Hardware Settings tab says so
        /// instead of presenting an unverified list as a capability.
        /// </summary>
        public bool MeasurementModesUnverified;

        public bool TriggerDelaySupported;
        public int TriggerDelayMinimumCounts;
        public int TriggerDelayMaximumCounts;
        public int TriggerDelayIncrementCounts = 1;

        /// <summary>
        /// Microseconds represented by a single trigger-delay count, derived from
        /// ExternalTriggerDelay.triggerDelayCountsToMicroseconds(). Ocean FX reports 1.0.
        /// </summary>
        public double MicrosecondsPerCount = 1.0;

        public double MinWavelength
        {
            get { return Wavelengths.Length == 0 ? 0.0 : Wavelengths[0]; }
        }

        public double MaxWavelength
        {
            get { return Wavelengths.Length == 0 ? 0.0 : Wavelengths[Wavelengths.Length - 1]; }
        }

        /// <summary>
        /// One model name per spectrometer.
        ///
        /// getFirmwareModel() is read out of the device itself and is preferred, because
        /// getName() reports the USB product family instead of the model on the housing:
        /// a Flame-S enumerates as "USB2000+" since it shares that product ID. getName()
        /// is only the fallback for devices whose firmware does not report a model.
        /// <see cref="DetailLabel"/> keeps both strings for the tool tip.
        /// </summary>
        public string ModelLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(FirmwareModel))
                    return FirmwareModel;

                return string.IsNullOrEmpty(Name) ? "unknown" : Name;
            }
        }

        /// <summary>Both driver strings, for places that can afford the width.</summary>
        public string DetailLabel
        {
            get
            {
                if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(FirmwareModel) ||
                    string.Equals(Name, FirmwareModel, StringComparison.OrdinalIgnoreCase))
                {
                    return ModelLabel;
                }

                return FirmwareModel + " (getName: " + Name + ")";
            }
        }

        public string ShortLabel
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "[{0}] {1} ({2})",
                    Index, ModelLabel, SerialNumber);
            }
        }

        /// <summary>
        /// Which physical spectrometer this is, independent of the driver's ordering.
        ///
        /// The index only says where a device sits in the list openAllSpectrometers()
        /// happened to return, and unplugging one renumbers the rest. Anything that ends
        /// up driving a pin has to name the device by something that does not move, so
        /// the serial number is used and the index is only a fallback for a device whose
        /// firmware does not report one.
        /// </summary>
        public string Identity
        {
            get
            {
                return string.IsNullOrEmpty(SerialNumber)
                    ? "index:" + Index.ToString(CultureInfo.InvariantCulture)
                    : SerialNumber;
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "[{0}] {1}  S/N {2}  {3} px ({4} dark)  {5:F1}-{6:F1} nm  int {7}-{8} us  full scale {9}{10}{11}",
                Index, DetailLabel, SerialNumber, PixelCount, DarkPixelCount,
                MinWavelength, MaxWavelength,
                MinimumIntegrationTimeUs, MaximumIntegrationTimeUs, MaximumIntensity,
                GpioSupported ? "  GPIO:" + GpioPinCount + "pin" : "  GPIO:n/a",
                TriggerDelaySupported
                    ? string.Format(CultureInfo.InvariantCulture, "  TrigDelay:<={0:F1}us", TriggerDelayMaximumCounts * MicrosecondsPerCount)
                    : "  TrigDelay:n/a");
        }
    }

    /// <summary>Result of pushing a trigger delay to the device.</summary>
    internal struct AppliedTriggerDelay
    {
        public bool Supported;
        public int Counts;
        public double Microseconds;

        public static AppliedTriggerDelay Unsupported
        {
            get { return new AppliedTriggerDelay { Supported = false }; }
        }

        public override string ToString()
        {
            if (!Supported)
                return "trigger delay not supported";

            return string.Format(CultureInfo.InvariantCulture,
                "applied: {0:F1} us ({1} counts)", Microseconds, Counts);
        }
    }
}
