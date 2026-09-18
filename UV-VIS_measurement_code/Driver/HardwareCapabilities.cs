using System;
using System.Globalization;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// One measurement (external trigger) mode as it stands for one spectrometer.
    ///
    /// OmniDriver has no call that enumerates the modes a given spectrometer supports:
    /// there is only setExternalTriggerMode()/getExternalTriggerMode(). A mode is
    /// therefore offered to the driver once when the device is opened and kept as
    /// supported only if the driver accepts it and reports it back - see
    /// <see cref="SpectrometerSession.ReadMeasurementModes"/> for what that can and
    /// cannot detect.
    /// </summary>
    internal sealed class MeasurementModeOption
    {
        public TriggerMode Mode;
        public string Label = string.Empty;

        /// <summary>False when the driver refused the mode or reported a different one back.</summary>
        public bool Supported = true;

        /// <summary>What the driver said when it refused. Null while <see cref="Supported"/>.</summary>
        public string Reason;

        public override string ToString()
        {
            return Supported ? Label : Label + " (not available: " + Reason + ")";
        }
    }

    /// <summary>
    /// One GPIO channel of one spectrometer, as reported by NETGPIO.
    ///
    /// The driver exposes getNumberOfPins() and getTotalGPIOBits(). Bits at or above
    /// the pin count are known to the driver but are not usable pins, so they are kept
    /// in the list and marked unusable rather than being hidden or invented.
    ///
    /// There is no per-pin input/output capability call in the API. Every pin the
    /// driver does expose accepts setDirectionBit(pin, 0|1), so both directions are
    /// offered for a usable pin and neither for one that is not.
    /// </summary>
    internal sealed class GpioChannelInfo
    {
        public int Index;

        /// <summary>True for indices below getNumberOfPins().</summary>
        public bool Usable;

        public bool InputSupported;
        public bool OutputSupported;

        /// <summary>0 = input, 1 = output, -1 = the driver did not report it.</summary>
        public int Direction = -1;

        /// <summary>0 = general purpose I/O, 1 = the pin's alternate function, -1 = unknown.</summary>
        public int Mux = -1;

        /// <summary>Current level, or -1 when the driver did not report it.</summary>
        public int Value = -1;

        /// <summary>Why the channel cannot be used, when it cannot.</summary>
        public string Note;

        public bool BothDirections
        {
            get { return InputSupported && OutputSupported; }
        }

        public string DescribeCapability()
        {
            if (!Usable)
                return "Unsupported";
            if (BothDirections)
                return "Input / Output";

            return InputSupported ? "Input only" : (OutputSupported ? "Output only" : "Unsupported");
        }

        public string DescribeDirection()
        {
            if (Direction == 0) return "Input";
            if (Direction == 1) return "Output";
            return "unknown";
        }

        /// <summary>True while the pin is an output that can be driven.</summary>
        public bool IsOutput
        {
            get { return Usable && OutputSupported && Direction == 1; }
        }

        public string DescribeLevel()
        {
            if (Value == 0) return "LOW";
            if (Value == 1) return "HIGH";
            return "-";
        }

        /// <summary>
        /// What the pin is doing right now. For an input this is the level being read off
        /// the connector; for an output it is the level being driven onto it.
        /// </summary>
        public string DescribeStatus()
        {
            if (!Usable)
                return string.IsNullOrEmpty(Note) ? "Disabled" : "Disabled - " + Note;

            if (Mux == 1)
                return "Alternate function (not routed to GPIO)";

            if (Value < 0)
                return Direction == 1 ? "Output" : "Input";

            return Direction == 1
                ? "Output, driving " + DescribeLevel()
                : "Input, reads " + DescribeLevel();
        }
    }

    /// <summary>Which piece of device configuration a <see cref="HardwareChange"/> carries.</summary>
    internal enum HardwareChangeKind
    {
        MeasurementMode,
        GpioDirection,

        /// <summary>Drive an output pin low or high.</summary>
        GpioLevel,

        /// <summary>Change nothing; just report what the pins currently read.</summary>
        ReadGpio
    }

    /// <summary>
    /// One hardware setting the operator changed. It is applied through the acquisition
    /// thread with the stop / apply / restart sequence, because changing the trigger mode
    /// or a pin direction underneath a running read leaves the device and the driver
    /// disagreeing about what the next spectrum is.
    /// </summary>
    internal sealed class HardwareChange
    {
        public int DeviceIndex;

        /// <summary>
        /// <see cref="DeviceInfo.Identity"/> of the spectrometer this is meant for. The
        /// index alone is not enough: a command can be sitting in the queue while a device
        /// is unplugged, and acting on it afterwards would drive a pin on whichever
        /// spectrometer inherited that index. The session checks this before it writes.
        /// </summary>
        public string DeviceIdentity = string.Empty;

        public HardwareChangeKind Kind;

        public TriggerMode Mode;

        public int Pin;

        /// <summary>True to make the pin an output, false to make it an input.</summary>
        public bool Output;

        /// <summary>Level to drive on an output pin.</summary>
        public bool High;

        public string Describe()
        {
            string device = string.Format(CultureInfo.InvariantCulture, "[{0}] {1}",
                DeviceIndex, DeviceIdentity);

            switch (Kind)
            {
                case HardwareChangeKind.MeasurementMode:
                    return device + " measurement mode -> " + TriggerModes.Label(Mode);

                case HardwareChangeKind.GpioLevel:
                    return device + " GPIO " + Pin + " -> " + (High ? "HIGH" : "LOW");

                case HardwareChangeKind.ReadGpio:
                    return device + " GPIO state";

                default:
                    return device + " GPIO " + Pin + " direction -> " + (Output ? "Output" : "Input");
            }
        }
    }

    /// <summary>
    /// What the driver actually did with a <see cref="HardwareChange"/>. A change is only
    /// reported as applied once the driver has read the new value back.
    /// </summary>
    internal struct HardwareApplyOutcome
    {
        public bool Applied;

        /// <summary>Why it failed, phrased for the operator. Null when it succeeded.</summary>
        public string Failure;

        /// <summary>True when the previous setting was put back after a failure.</summary>
        public bool Restored;

        public static HardwareApplyOutcome Success
        {
            get { return new HardwareApplyOutcome { Applied = true }; }
        }

        public static HardwareApplyOutcome Failed(string reason, bool restored)
        {
            return new HardwareApplyOutcome { Applied = false, Failure = reason, Restored = restored };
        }
    }
}
