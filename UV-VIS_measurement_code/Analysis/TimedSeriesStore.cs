using System;
using System.Collections.Generic;
using UvVisMeasurement.Driver;

namespace UvVisMeasurement.Analysis
{
    /// <summary>One accumulated spectrum in a timed series.</summary>
    internal sealed class TimedRecord
    {
        public double ElapsedSeconds;
        public DateTime TimestampUtc;

        /// <summary>Absorbance in OD, or null when the frame had no reference. Stored as float to halve memory.</summary>
        public float[] Absorbance;

        /// <summary>Raw counts of the measured spectrometer.</summary>
        public float[] Counts;
    }

    /// <summary>
    /// Immutable view of the series handed to the chart, the 2D map and the CSV exporter.
    ///
    /// Readers must never walk the store's live list: a frame arriving mid-render (the
    /// acquisition callback is posted onto the UI thread and runs whenever a nested
    /// message loop pumps, e.g. while a modal dialog is open) would invalidate the
    /// enumerator. Copying the reference array once is cheap and removes that hazard.
    /// </summary>
    internal sealed class TimedSeriesSnapshot
    {
        private static readonly TimedRecord[] NoRecords = new TimedRecord[0];

        public static readonly TimedSeriesSnapshot Empty = new TimedSeriesSnapshot(null, NoRecords);

        public TimedSeriesSnapshot(double[] wavelengths, TimedRecord[] records)
        {
            Wavelengths = wavelengths;
            Records = records ?? NoRecords;
        }

        public double[] Wavelengths { get; private set; }
        public TimedRecord[] Records { get; private set; }

        public int Count
        {
            get { return Records.Length; }
        }

        public bool IsEmpty
        {
            get { return Records.Length == 0 || Wavelengths == null || Wavelengths.Length == 0; }
        }

        public int PixelCount
        {
            get { return Wavelengths == null ? 0 : Wavelengths.Length; }
        }

        /// <summary>Absorbance over time at one pixel. NaN where the frame had no absorbance.</summary>
        public void FillTrace(int pixelIndex, List<double> times, List<double> values)
        {
            times.Clear();
            values.Clear();

            for (int i = 0; i < Records.Length; i++)
            {
                TimedRecord record = Records[i];
                times.Add(record.ElapsedSeconds);

                float[] absorbance = record.Absorbance;
                values.Add(absorbance != null && pixelIndex >= 0 && pixelIndex < absorbance.Length
                    ? absorbance[pixelIndex]
                    : double.NaN);
            }
        }

        /// <summary>Min/max absorbance across the whole series, ignoring NaN.</summary>
        public bool TryGetAbsorbanceRange(out double min, out double max)
        {
            min = 0.0;
            max = 0.0;
            bool any = false;

            for (int i = 0; i < Records.Length; i++)
            {
                float[] values = Records[i].Absorbance;
                if (values == null)
                    continue;

                for (int j = 0; j < values.Length; j++)
                {
                    float value = values[j];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        continue;

                    if (!any)
                    {
                        min = max = value;
                        any = true;
                    }
                    else
                    {
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }

            return any;
        }
    }

    /// <summary>
    /// Rolling buffer of spectra for the "Timed Absorption Series" tabs.
    /// A 2136 pixel Ocean FX frame costs ~17 kB here, so the capacity is bounded.
    /// Mutated only from the UI thread; readers go through <see cref="Snapshot"/>.
    /// </summary>
    internal sealed class TimedSeriesStore
    {
        private readonly List<TimedRecord> _records = new List<TimedRecord>();
        private double[] _wavelengths;

        /// <summary>Maximum number of retained records; the oldest are discarded first.</summary>
        public int Capacity { get; set; }

        public TimedSeriesStore()
        {
            Capacity = 5000;
        }

        public int Count
        {
            get { return _records.Count; }
        }

        public bool IsEmpty
        {
            get { return _records.Count == 0; }
        }

        public double[] Wavelengths
        {
            get { return _wavelengths; }
        }

        /// <summary>Approximate retained size in bytes, for the status bar.</summary>
        public long ApproximateBytes
        {
            get
            {
                int pixels = _wavelengths == null ? 0 : _wavelengths.Length;
                return (long)_records.Count * pixels * sizeof(float) * 2L;
            }
        }

        /// <summary>
        /// Appends a frame. Changing the wavelength grid (a different spectrometer, or a
        /// binning change) resets the series, because the matrix must stay rectangular.
        /// </summary>
        public void Add(SpectrumFrame frame)
        {
            if (frame == null || frame.Wavelengths == null)
                return;

            if (!SameGrid(_wavelengths, frame.Wavelengths))
            {
                _records.Clear();
                _wavelengths = (double[])frame.Wavelengths.Clone();
            }

            _records.Add(new TimedRecord
            {
                ElapsedSeconds = frame.ElapsedSeconds,
                TimestampUtc = frame.TimestampUtc,
                Absorbance = ToFloat(frame.Absorbance),
                Counts = ToFloat(frame.Counts)
            });

            int overflow = _records.Count - Math.Max(1, Capacity);
            if (overflow > 0)
                _records.RemoveRange(0, overflow);
        }

        public void Clear()
        {
            _records.Clear();
            _wavelengths = null;
        }

        /// <summary>Stable copy for rendering and export.</summary>
        public TimedSeriesSnapshot Snapshot()
        {
            return _records.Count == 0 || _wavelengths == null
                ? TimedSeriesSnapshot.Empty
                : new TimedSeriesSnapshot(_wavelengths, _records.ToArray());
        }

        private static bool SameGrid(double[] a, double[] b)
        {
            if (a == null || b == null)
                return false;
            if (ReferenceEquals(a, b))
                return true;
            if (a.Length != b.Length || a.Length == 0)
                return false;

            return Math.Abs(a[0] - b[0]) < 1e-9
                && Math.Abs(a[a.Length - 1] - b[b.Length - 1]) < 1e-9;
        }

        private static float[] ToFloat(double[] source)
        {
            if (source == null)
                return null;

            var result = new float[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = (float)source[i];

            return result;
        }
    }
}
