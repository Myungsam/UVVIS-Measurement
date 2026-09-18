using System;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// One row of the spectrometer table: whether the device takes part in an acquisition,
    /// which role it plays, and its own integration time and external trigger delay.
    ///
    /// Integration time and trigger delay are per device because two spectrometers looking
    /// at the probe and the reference beam almost never want the same exposure.
    /// </summary>
    internal sealed class DeviceSettings
    {
        public int Index;

        /// <summary>
        /// "Use" check box. Exactly one spectrometer is measured at a time, so this also
        /// selects which device Capture Dark / Capture White Reference apply to.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// Probe beam. Only meaningful once two spectrometers are connected. More than one
        /// device may be a probe: that means several samples are measured in turn, each
        /// against its own dark and white reference, and no reference beam exists.
        /// </summary>
        public bool IsProbe;

        /// <summary>
        /// Reference beam. At most one device, and only while exactly one probe exists -
        /// with two probes there is nothing left to monitor the lamp with.
        /// </summary>
        public bool IsReference;

        /// <summary>
        /// "Corr." check box: this device is one of the two that form the live reference
        /// correction pair. With three or more spectrometers the operator picks the pair;
        /// with two or fewer the column is not editable and every device belongs to it.
        /// </summary>
        public bool InCorrectionPair;

        public double IntegrationMs = 1.0;
        public double TriggerDelayUs = 50.0;

        /// <summary>
        /// Measurement (external trigger) mode of this spectrometer. Per device for the
        /// same reason the integration time is: the probe and the reference channel do
        /// not have to be triggered the same way.
        /// </summary>
        public TriggerMode TriggerMode = TriggerMode.Normal;

        public bool HasRole
        {
            get { return IsProbe || IsReference; }
        }

        public int IntegrationUs
        {
            get
            {
                double us = IntegrationMs * 1000.0;
                if (us < 1.0)
                    us = 1.0;
                if (us > int.MaxValue)
                    us = int.MaxValue;

                return (int)Math.Round(us, MidpointRounding.AwayFromZero);
            }
        }

        public string DescribeRole()
        {
            string role;
            if (IsProbe && IsReference)
                role = "probe + reference";
            else if (IsProbe)
                role = "probe";
            else if (IsReference)
                role = "reference";
            else
                role = "unassigned";

            return InCorrectionPair ? role + " (correction pair)" : role;
        }

        public DeviceSettings Clone()
        {
            return (DeviceSettings)MemberwiseClone();
        }
    }
}
