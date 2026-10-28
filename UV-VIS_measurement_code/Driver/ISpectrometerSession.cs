using System;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// Everything the acquisition thread asks of a spectrometer backend.
    ///
    /// <see cref="SpectrometerSession"/> is the OmniDriver implementation and
    /// <see cref="SimulatedSpectrometerSession"/> the demo/test one. Only the session
    /// implementations may touch a vendor SDK; the engine, the UI, analysis and export
    /// code see this interface and the plain data types around it. That boundary is
    /// also what a future OceanDirect backend plugs into.
    ///
    /// Same threading contract as the driver: every member is called from the single
    /// acquisition thread.
    /// </summary>
    internal interface ISpectrometerSession : IDisposable
    {
        DeviceInfo[] Devices { get; }

        int DeviceCount { get; }

        /// <summary>Backend name and version for the status bar, e.g. "OmniDriver 2.56".</summary>
        string DriverName { get; }

        bool IsSimulated { get; }

        int Open();

        GpioChannelInfo[] ReadGpioChannels(int index);

        /// <summary>Applies one setting and only reports success once it has been read back.</summary>
        HardwareApplyOutcome ApplyHardwareChange(HardwareChange change);

        AppliedTriggerDelay ApplyTriggerDelay(int index, double microseconds);

        void ApplyAcquisition(int index, int integrationUs, int scansToAverage, TriggerMode triggerMode,
                              int usbTimeoutMs, bool triggerModeChanged);

        /// <summary>One spectrum, or null on a timeout / invalid read (see <paramref name="status"/>).</summary>
        double[] TryGetSpectrum(int index, out SpectrumReadStatus status);

        bool IsSaturated(int index);

        string LastDriverException { get; }

        DeviceInfo RequireDevice(int index);
    }

    internal enum SpectrumReadStatus
    {
        Ok,
        Timeout,
        Invalid
    }
}
