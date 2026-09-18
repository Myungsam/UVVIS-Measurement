using System;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// One acquisition from the spectrometer currently selected in the table, together
    /// with the dark and white reference stored for that same spectrometer.
    ///
    /// Capture Dark and Capture White Reference work from this object rather than
    /// triggering their own read, so what gets stored is exactly the spectrum the
    /// operator was watching when the button was pressed.
    /// </summary>
    internal sealed class SpectrumFrame
    {
        public int DeviceIndex = -1;
        public string DeviceLabel = string.Empty;

        /// <summary>Wavelength grid of that spectrometer. Shared with DeviceInfo, never modified.</summary>
        public double[] Wavelengths;

        /// <summary>Raw counts as returned by getSpectrum().</summary>
        public double[] Counts;

        /// <summary>Stored dark D for this spectrometer, or null when none was captured.</summary>
        public double[] Dark;

        /// <summary>Stored white reference W (I0) for this spectrometer, or null.</summary>
        public double[] WhiteReference;

        /// <summary>Absorbance in OD, or null when no white reference exists. May contain NaN.</summary>
        public double[] Absorbance;

        // ---- reference channel, only filled when the drift correction ran ----

        /// <summary>Spectrometer that monitored the lamp for this frame, or -1.</summary>
        public int ReferenceDeviceIndex = -1;

        public string ReferenceDeviceLabel = string.Empty;

        /// <summary>
        /// Lamp drift factor already interpolated onto <see cref="Wavelengths"/>. 1.0 means
        /// the lamp is exactly as bright as when the white references were captured. NaN
        /// outside the wavelength range the reference spectrometer can see.
        /// </summary>
        public double[] DriftFactor;

        /// <summary>True when <see cref="Absorbance"/> was computed with the drift factor.</summary>
        public bool DriftCorrected;

        /// <summary>Mean of the finite drift factors, for the status readout.</summary>
        public double DriftMean = double.NaN;

        public bool Saturated;

        public DateTime TimestampUtc;

        /// <summary>Seconds since the measurement was started.</summary>
        public double ElapsedSeconds;

        public bool HasAbsorbance
        {
            get { return Absorbance != null; }
        }

        public bool HasSpectrum
        {
            get { return Wavelengths != null && Counts != null && Counts.Length > 0; }
        }

        public bool HasDriftFactor
        {
            get { return DriftFactor != null; }
        }
    }
}
