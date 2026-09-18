using System;

namespace UvVisMeasurement.Driver
{
    /// <summary>Trigger modes accepted by NETWrapper.setExternalTriggerMode().</summary>
    internal enum TriggerMode
    {
        Normal = 0,
        ExternalSoftware = 1,
        ExternalSynchronization = 2,
        Hardware = 3,
        SingleShot = 4
    }

    /// <summary>
    /// The candidate list and its display names, in one place so the trigger combo box
    /// on the parameter panel and the per-device list on the Hardware Settings tab can
    /// never drift apart.
    ///
    /// These are the mode numbers the driver accepts, not a claim about any particular
    /// spectrometer: which of them a device really supports is decided per device by
    /// <see cref="SpectrometerSession.ReadMeasurementModes"/>.
    /// </summary>
    internal static class TriggerModes
    {
        public static readonly TriggerMode[] All =
        {
            TriggerMode.Normal,
            TriggerMode.ExternalSoftware,
            TriggerMode.ExternalSynchronization,
            TriggerMode.Hardware,
            TriggerMode.SingleShot
        };

        public static string Label(TriggerMode mode)
        {
            switch (mode)
            {
                case TriggerMode.Normal: return "Normal Mode";
                case TriggerMode.ExternalSoftware: return "External Software Trigger Mode";
                case TriggerMode.ExternalSynchronization: return "External Synchronization Trigger Mode";
                case TriggerMode.Hardware: return "Hardware Trigger Mode";
                case TriggerMode.SingleShot: return "Single-Shot Trigger Mode";
                default: return "Mode " + (int)mode;
            }
        }
    }

    /// <summary>
    /// Everything the acquisition thread needs to configure the hardware.
    /// Instances are deep-copied before being handed across the thread boundary.
    /// </summary>
    internal sealed class MeasurementSettings
    {
        /// <summary>One entry per connected spectrometer, in index order.</summary>
        public DeviceSettings[] Devices = new DeviceSettings[0];

        public int ScansToAverage = 1;

        /// <summary>Minimum period between the start of two acquisitions.</summary>
        public int MinIntervalMs = 100;

        /// <summary>
        /// Measurement mode of the spectrometer being read. It lives on the device row
        /// (like integration time and trigger delay) because the Hardware Settings tab
        /// configures each spectrometer separately; this is the one that governs the
        /// acquisition currently running.
        /// </summary>
        public TriggerMode TriggerMode
        {
            get
            {
                DeviceSettings active = Active;
                return active == null ? TriggerMode.Normal : active.TriggerMode;
            }
        }

        // GPIO is not part of the per-frame settings: pins are configured per spectrometer
        // from the Hardware Settings tab and written once, when the operator changes one.

        /// <summary>
        /// The one spectrometer the "Use" column selects. Exactly one device is measured
        /// at a time, so this is what the acquisition loop reads and what Capture Dark /
        /// Capture White Reference store their spectra against.
        /// </summary>
        public DeviceSettings Active
        {
            get
            {
                for (int i = 0; i < Devices.Length; i++)
                {
                    if (Devices[i] != null && Devices[i].Enabled)
                        return Devices[i];
                }
                return null;
            }
        }

        public int ActiveIndex
        {
            get { DeviceSettings device = Active; return device == null ? -1 : device.Index; }
        }

        public DeviceSettings Probe
        {
            get
            {
                for (int i = 0; i < Devices.Length; i++)
                {
                    if (Devices[i] != null && Devices[i].IsProbe)
                        return Devices[i];
                }
                return null;
            }
        }

        public DeviceSettings Reference
        {
            get
            {
                for (int i = 0; i < Devices.Length; i++)
                {
                    if (Devices[i] != null && Devices[i].IsReference)
                        return Devices[i];
                }
                return null;
            }
        }

        public int ProbeIndex
        {
            get { DeviceSettings device = Probe; return device == null ? -1 : device.Index; }
        }

        public int ReferenceIndex
        {
            get { DeviceSettings device = Reference; return device == null ? -1 : device.Index; }
        }

        /// <summary>True when the probe and reference beams sit on two different spectrometers.</summary>
        public bool BeamsAssigned
        {
            get
            {
                int probe = ProbeIndex;
                int reference = ReferenceIndex;
                return probe >= 0 && reference >= 0 && probe != reference;
            }
        }

        /// <summary>How many spectrometers are marked as a probe.</summary>
        public int ProbeCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Devices.Length; i++)
                    if (Devices[i] != null && Devices[i].IsProbe)
                        count++;
                return count;
            }
        }

        /// <summary>Spectrometers ticked under "Corr.", i.e. the live-correction pair.</summary>
        public int CorrectionPairCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Devices.Length; i++)
                    if (Devices[i] != null && Devices[i].InCorrectionPair)
                        count++;
                return count;
            }
        }

        public bool IsInCorrectionPair(int index)
        {
            DeviceSettings device = For(index);
            return device != null && device.InCorrectionPair;
        }

        /// <summary>
        /// True when the live reference correction should run. There is no switch for it:
        /// it follows from the table alone.
        ///
        /// It needs exactly one probe (two probes means two independent samples and leaves
        /// nothing watching the lamp), one reference on a different spectrometer, both of
        /// them forming the correction pair, and the spectrometer under Use has to be that
        /// probe - the correction rescales the probe's white reference, so it is
        /// meaningless while a different device is being read. Whether all four spectra
        /// exist is decided by the engine, which owns the reference store.
        /// </summary>
        public bool DriftCorrectionRequested
        {
            get
            {
                DeviceSettings active = Active;
                DeviceSettings reference = Reference;

                return active != null
                    && reference != null
                    && active.IsProbe
                    && ProbeCount == 1
                    && active.Index != reference.Index
                    && CorrectionPairCount == 2
                    && active.InCorrectionPair
                    && reference.InCorrectionPair;
            }
        }

        /// <summary>
        /// Why <see cref="DriftCorrectionRequested"/> is false, phrased for the operator.
        /// Returns null when the correction is in fact requested.
        /// </summary>
        public string DescribeDriftBlocker(int deviceCount)
        {
            if (deviceCount < 2)
                return "needs a second spectrometer";

            DeviceSettings active = Active;
            if (active == null)
                return "tick a spectrometer under Use";

            int probes = ProbeCount;
            if (probes == 0)
                return "assign a probe";
            if (probes > 1)
                return probes + " probes - each uses its own dark/white";

            DeviceSettings reference = Reference;
            if (reference == null)
                return "assign a reference";
            if (reference.Index == active.Index)
                return "probe and reference must differ";
            if (!active.IsProbe)
                return "tick the probe [" + ProbeIndex + "] under Use";

            if (CorrectionPairCount != 2)
                return "tick exactly 2 under Corr.";
            if (!active.InCorrectionPair || !reference.InCorrectionPair)
                return "Corr. must be [" + active.Index + "] and [" + reference.Index + "]";

            return null;
        }

        public DeviceSettings For(int index)
        {
            for (int i = 0; i < Devices.Length; i++)
            {
                if (Devices[i] != null && Devices[i].Index == index)
                    return Devices[i];
            }
            return null;
        }

        public MeasurementSettings Clone()
        {
            var copy = (MeasurementSettings)MemberwiseClone();

            // The array is shared by MemberwiseClone; the acquisition thread must never
            // see the UI mutating rows underneath it.
            copy.Devices = new DeviceSettings[Devices.Length];
            for (int i = 0; i < Devices.Length; i++)
                copy.Devices[i] = Devices[i] == null ? null : Devices[i].Clone();

            return copy;
        }

        /// <summary>
        /// USB read timeout. Normal mode only has to outlast one averaged acquisition;
        /// the external trigger modes block until a trigger arrives, so a short timeout
        /// is used there and the acquisition loop simply retries. That keeps the run
        /// toggle responsive when no trigger ever comes.
        /// </summary>
        public int UsbTimeoutMsFor(double integrationMs)
        {
            if (TriggerMode == TriggerMode.Normal)
            {
                double estimate = integrationMs * Math.Max(1, ScansToAverage) * 3.0 + 2000.0;
                return (int)Math.Min(int.MaxValue, Math.Max(2000.0, estimate));
            }

            return 1500;
        }
    }
}
