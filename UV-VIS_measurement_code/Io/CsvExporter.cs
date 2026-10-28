using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;

namespace UvVisMeasurement.Io
{
    /// <summary>Sample bookkeeping and instrument state written into every CSV header.</summary>
    internal sealed class ExportMetadata
    {
        public string Sample = string.Empty;
        public string Concentration = string.Empty;
        public string CellThickness = string.Empty;

        /// <summary>The spectrometer the data came from, i.e. the one ticked under "Use".</summary>
        public string ActiveDevice = string.Empty;

        public string ProbeDevice = string.Empty;
        public string ReferenceDevice = string.Empty;

        /// <summary>One line per connected spectrometer: role, integration time, trigger delay.</summary>
        public string[] DeviceLines = new string[0];

        public bool BeamsAssigned;
        public double IntegrationMs;
        public int ScansToAverage;
        public int MinIntervalMs;
        public TriggerMode TriggerMode;
        public double RequestedTriggerDelayUs;
        public string AppliedTriggerDelay = string.Empty;
        public string GpioState = string.Empty;
        public string ReferenceState = string.Empty;

        /// <summary>State of the live reference (lamp drift) correction.</summary>
        public string DriftCorrection = string.Empty;

        /// <summary>True when the absorbance in this file actually used the drift factor.</summary>
        public bool DriftCorrectionApplied;
    }

    /// <summary>Writes spectra and timed series as UTF-8 CSV with a commented header block.</summary>
    internal static class CsvExporter
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        /// <summary>Writes one spectrum: wavelength, raw counts, stored dark/reference and absorbance.</summary>
        public static string SaveSpectrum(string path, ExportMetadata metadata, SpectrumFrame frame,
                                          ReferenceSnapshot references)
        {
            if (frame == null || frame.Wavelengths == null)
                throw new InvalidOperationException("There is no spectrum to save yet. Start a measurement first.");

            path = EnsureExtension(path, ".csv");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            // The frame carries the dark and white reference that were in force when it was
            // taken; the snapshot is only a fallback for a frame captured before a change.
            double[] dark = frame.Dark ?? (references == null ? null : references.Dark);
            double[] white = frame.WhiteReference ?? (references == null ? null : references.WhiteReference);

            var builder = new StringBuilder();
            WriteHeader(builder, "Single spectrum", metadata);
            builder.Append("# Measured spectrometer,").AppendLine(Escape(frame.DeviceLabel));
            builder.Append("# Acquired (UTC),").Append(frame.TimestampUtc.ToString("o", Culture)).AppendLine();
            builder.Append("# Elapsed (s),").Append(frame.ElapsedSeconds.ToString("F3", Culture)).AppendLine();
            builder.Append("# Saturated,").Append(frame.Saturated ? "yes" : "no").AppendLine();

            bool hasDrift = frame.DriftFactor != null;
            if (hasDrift)
            {
                builder.Append("# Reference channel,").AppendLine(Escape(frame.ReferenceDeviceLabel));
                builder.Append("# Drift factor mean,").Append(Format(frame.DriftMean)).AppendLine();
                builder.Append("# Drift correction,").AppendLine(frame.DriftCorrected
                    ? "applied - f linearly interpolated onto this spectrometer's wavelengths"
                    : "not applied");
            }

            builder.AppendLine("#");

            bool hasDark = dark != null;
            bool hasWhite = white != null;
            bool hasAbsorbance = frame.Absorbance != null;

            builder.Append("Wavelength (nm),Sample (counts)");
            if (hasDark) builder.Append(",Dark (counts)");
            if (hasWhite) builder.Append(",White reference (counts)");
            if (hasDrift) builder.Append(",Drift factor");
            if (hasAbsorbance) builder.Append(",Absorbance (OD)");
            builder.AppendLine();

            for (int i = 0; i < frame.Wavelengths.Length; i++)
            {
                builder.Append(frame.Wavelengths[i].ToString("F4", Culture));
                builder.Append(',').Append(Format(Value(frame.Counts, i)));
                if (hasDark) builder.Append(',').Append(Format(Value(dark, i)));
                if (hasWhite) builder.Append(',').Append(Format(Value(white, i)));
                if (hasDrift) builder.Append(',').Append(Format(Value(frame.DriftFactor, i)));
                if (hasAbsorbance) builder.Append(',').Append(Format(Value(frame.Absorbance, i)));
                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// Writes a timed series as two matrices: absorbance and raw probe counts.
        /// Rows are acquisitions, columns are wavelengths, so the files drop straight
        /// into Origin/Excel as a 2D surface.
        /// </summary>
        public static string[] SaveTimedSeries(string path, ExportMetadata metadata, TimedSeriesSnapshot series)
        {
            if (series == null || series.IsEmpty)
                throw new InvalidOperationException("No timed datasets have been accumulated yet.");

            path = EnsureExtension(path, ".csv");
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);

            string stem = Path.Combine(directory, Path.GetFileNameWithoutExtension(path));
            string absorbancePath = stem + "_absorbance.csv";
            string countsPath = stem + "_counts.csv";

            WriteMatrix(absorbancePath, metadata, series, true);
            WriteMatrix(countsPath, metadata, series, false);

            return new[] { absorbancePath, countsPath };
        }

        private static void WriteMatrix(string path, ExportMetadata metadata, TimedSeriesSnapshot series, bool absorbance)
        {
            double[] wavelengths = series.Wavelengths;
            TimedRecord[] records = series.Records;

            var builder = new StringBuilder();
            WriteHeader(builder, absorbance ? "Timed series - absorbance (OD)" : "Timed series - raw counts", metadata);
            builder.Append("# Records,").Append(records.Length.ToString(Culture)).AppendLine();
            builder.Append("# Pixels,").Append(wavelengths.Length.ToString(Culture)).AppendLine();
            builder.AppendLine("# Row = one acquisition, column = one pixel.");
            builder.AppendLine("#");

            builder.Append("Elapsed (s),Timestamp (UTC)");
            for (int i = 0; i < wavelengths.Length; i++)
                builder.Append(',').Append(wavelengths[i].ToString("F4", Culture));
            builder.AppendLine();

            for (int r = 0; r < records.Length; r++)
            {
                TimedRecord record = records[r];
                builder.Append(record.ElapsedSeconds.ToString("F4", Culture));
                builder.Append(',').Append(record.TimestampUtc.ToString("o", Culture));

                float[] values = absorbance ? record.Absorbance : record.Counts;
                for (int i = 0; i < wavelengths.Length; i++)
                {
                    builder.Append(',');
                    if (values != null && i < values.Length)
                        builder.Append(Format(values[i]));
                }
                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// The '#' comment block every file starts with. Public so the live recorder
        /// (<see cref="TimedSeriesRecorder"/>) writes byte-for-byte the same header as a file
        /// saved afterwards.
        /// </summary>
        public static string BuildHeader(string title, ExportMetadata metadata)
        {
            var builder = new StringBuilder();
            WriteHeader(builder, title, metadata);
            return builder.ToString();
        }

        private static void WriteHeader(StringBuilder builder, string title, ExportMetadata metadata)
        {
            builder.Append("# Ocean Optics UV-VIS Measurement - ").AppendLine(title);
            builder.Append("# Saved (local),").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Culture)).AppendLine();

            if (metadata == null)
            {
                builder.AppendLine("#");
                return;
            }

            builder.Append("# Sample,").AppendLine(Escape(metadata.Sample));
            builder.Append("# Concentration (mM),").AppendLine(Escape(metadata.Concentration));
            builder.Append("# Cell thickness,").AppendLine(Escape(metadata.CellThickness));

            if (metadata.DeviceLines != null)
            {
                for (int i = 0; i < metadata.DeviceLines.Length; i++)
                    builder.Append("# Spectrometer,").AppendLine(Escape(metadata.DeviceLines[i]));
            }

            builder.Append("# Measured (Use),").AppendLine(Escape(metadata.ActiveDevice));
            if (metadata.BeamsAssigned)
            {
                builder.Append("# Probe beam,").AppendLine(Escape(metadata.ProbeDevice));
                builder.Append("# Reference beam,").AppendLine(Escape(metadata.ReferenceDevice));
            }
            if (metadata.DriftCorrectionApplied)
            {
                builder.Append("# Absorbance,")
                       .AppendLine("A = -log10((S - D) / ((W - D) * f)),  f = (Sr - Dr) / (Wr - Dr)");
            }
            else
            {
                builder.Append("# Absorbance,").AppendLine("A = -log10((S - D) / (W - D))");
            }

            if (!string.IsNullOrEmpty(metadata.DriftCorrection))
                builder.Append("# Reference correction,").AppendLine(Escape(metadata.DriftCorrection));
            builder.Append("# Integration (ms),").AppendLine(metadata.IntegrationMs.ToString("F3", Culture));
            builder.Append("# Scans to average,").AppendLine(metadata.ScansToAverage.ToString(Culture));
            builder.Append("# Min interval (ms),").AppendLine(metadata.MinIntervalMs.ToString(Culture));
            builder.Append("# Trigger mode,").AppendLine(metadata.TriggerMode + " (" + (int)metadata.TriggerMode + ")");
            builder.Append("# Trigger delay requested (us),").AppendLine(metadata.RequestedTriggerDelayUs.ToString("F1", Culture));
            builder.Append("# Trigger delay applied,").AppendLine(Escape(metadata.AppliedTriggerDelay));
            builder.Append("# GPIO,").AppendLine(Escape(metadata.GpioState));
            builder.Append("# Reference state,").AppendLine(Escape(metadata.ReferenceState));
            builder.AppendLine("#");
        }

        /// <summary>
        /// Lays out one X column and any number of value columns as CSV text, using the
        /// same number formats as the files this class writes: F4 for the X axis and G8
        /// for the values, with a missing value written as an empty field.
        ///
        /// Used for "Copy Spectrum Data", so what lands on the clipboard parses the same
        /// way in Excel, Origin, MATLAB or pandas as a saved file does.
        /// </summary>
        public static string BuildTable(string xHeader, string[] columnHeaders, double[] x, double[][] columns)
        {
            if (x == null || columnHeaders == null || columns == null)
                throw new ArgumentNullException("x");

            var builder = new StringBuilder(16 * (x.Length + 1) * (columns.Length + 1));

            builder.Append(Escape(string.IsNullOrEmpty(xHeader) ? "X" : xHeader));
            for (int c = 0; c < columnHeaders.Length; c++)
                builder.Append(',').Append(Escape(columnHeaders[c]));
            builder.AppendLine();

            for (int r = 0; r < x.Length; r++)
            {
                builder.Append(x[r].ToString("F4", Culture));

                for (int c = 0; c < columns.Length; c++)
                {
                    builder.Append(',');
                    double[] column = columns[c];
                    if (column != null && r < column.Length)
                        builder.Append(Format(column[r]));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// <summary>Builds "&lt;folder&gt;\&lt;sample&gt;_&lt;suffix&gt;_&lt;timestamp&gt;.csv".</summary>
        public static string BuildPath(string folder, string sample, string suffix)
        {
            if (string.IsNullOrWhiteSpace(folder))
                folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string name = string.IsNullOrWhiteSpace(sample) ? "spectrum" : sample.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Culture);
            return Path.Combine(folder, name + "_" + suffix + "_" + stamp + ".csv");
        }

        private static string EnsureExtension(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Choose a save path first.");

            return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
                ? path
                : path + extension;
        }

        private static double Value(IList<double> values, int index)
        {
            return values != null && index < values.Count ? values[index] : double.NaN;
        }

        private static string Format(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? string.Empty
                : value.ToString("G8", Culture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0
                ? value
                : "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
