using System;
using System.Globalization;
using System.IO;
using System.Text;
using UvVisMeasurement.Driver;

namespace UvVisMeasurement.Io
{
    /// <summary>
    /// Writes a timed series to disk as it is measured, instead of holding it in memory until
    /// the operator presses Save.
    ///
    /// A long run is exactly the one that must not be lost: the in-memory buffer keeps only
    /// the last <see cref="Analysis.TimedSeriesStore.Capacity"/> records and a crash or a
    /// power cut takes everything with it. Each row is flushed as it is written, so whatever
    /// was measured before the failure is already a readable CSV.
    ///
    /// The layout is the one <see cref="CsvExporter.SaveTimedSeries"/> produces - same header,
    /// same two files, same number formats - so a recorded run and a saved one are read by the
    /// same scripts.
    /// </summary>
    internal sealed class TimedSeriesRecorder : IDisposable
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        private StreamWriter _absorbance;
        private StreamWriter _counts;
        private double[] _wavelengths;

        public string AbsorbancePath { get; private set; }
        public string CountsPath { get; private set; }
        public int RecordCount { get; private set; }

        public bool IsRecording
        {
            get { return _absorbance != null; }
        }

        /// <summary>
        /// Opens the two files and writes their headers. The wavelength grid is fixed from
        /// here on: a frame from a different spectrometer cannot be appended to a matrix whose
        /// columns are these wavelengths.
        /// </summary>
        public void Start(string stem, ExportMetadata metadata, double[] wavelengths)
        {
            if (IsRecording)
                throw new InvalidOperationException("Recording is already running.");
            if (wavelengths == null || wavelengths.Length == 0)
                throw new ArgumentException("A wavelength grid is needed before recording.", "wavelengths");

            string directory = Path.GetDirectoryName(Path.GetFullPath(stem));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string withoutExtension = Path.Combine(
                directory ?? string.Empty,
                Path.GetFileNameWithoutExtension(stem));

            AbsorbancePath = withoutExtension + "_absorbance.csv";
            CountsPath = withoutExtension + "_counts.csv";
            _wavelengths = (double[])wavelengths.Clone();
            RecordCount = 0;

            _absorbance = Create(AbsorbancePath);
            _counts = Create(CountsPath);

            WriteHeader(_absorbance, metadata, "Timed series - absorbance (OD), recorded live");
            WriteHeader(_counts, metadata, "Timed series - raw counts, recorded live");
        }

        private static StreamWriter Create(string path)
        {
            return new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        }

        private void WriteHeader(StreamWriter writer, ExportMetadata metadata, string title)
        {
            writer.Write(CsvExporter.BuildHeader(title, metadata));
            writer.WriteLine("# Rows are appended while the measurement runs.");
            writer.WriteLine("#");

            var line = new StringBuilder("Elapsed (s),Timestamp (UTC)");
            for (int i = 0; i < _wavelengths.Length; i++)
                line.Append(',').Append(_wavelengths[i].ToString("F4", Culture));

            writer.WriteLine(line.ToString());
        }

        /// <summary>
        /// Appends one acquisition. A frame whose grid does not match the one the recording
        /// started with is refused rather than written into the wrong columns.
        /// </summary>
        public bool Append(SpectrumFrame frame)
        {
            if (!IsRecording || frame == null || frame.Wavelengths == null)
                return false;

            if (frame.Wavelengths.Length != _wavelengths.Length)
                return false;

            WriteRow(_absorbance, frame, frame.Absorbance);
            WriteRow(_counts, frame, frame.Counts);
            RecordCount++;
            return true;
        }

        private void WriteRow(StreamWriter writer, SpectrumFrame frame, double[] values)
        {
            var line = new StringBuilder(16 * (_wavelengths.Length + 2));
            line.Append(frame.ElapsedSeconds.ToString("F4", Culture));
            line.Append(',').Append(frame.TimestampUtc.ToString("o", Culture));

            for (int i = 0; i < _wavelengths.Length; i++)
            {
                line.Append(',');
                if (values == null || i >= values.Length)
                    continue;

                double value = values[i];
                if (!double.IsNaN(value) && !double.IsInfinity(value))
                    line.Append(value.ToString("G8", Culture));
            }

            writer.WriteLine(line.ToString());
        }

        /// <summary>Closes both files and returns their paths, or an empty array if none were open.</summary>
        public string[] Stop()
        {
            if (!IsRecording)
                return new string[0];

            string[] written = { AbsorbancePath, CountsPath };

            Close(ref _absorbance);
            Close(ref _counts);
            _wavelengths = null;

            return written;
        }

        private static void Close(ref StreamWriter writer)
        {
            if (writer == null)
                return;

            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch (IOException)
            {
                // Nothing useful can be done while closing; the rows already on disk stay.
            }

            writer = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
