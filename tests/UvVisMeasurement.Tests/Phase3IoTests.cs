using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Io;

namespace UvVisMeasurement.Tests
{
    [TestClass]
    public class Phase3IoTests
    {
        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "uvvis-p3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        private static SpectrumFrame Frame(double elapsed = 0.0)
        {
            double[] dark = { 100, 100, 100 };
            double[] white = { 1100, 2100, 600 };
            double[] counts = { 200, 1100.5, 100 };

            return new SpectrumFrame
            {
                DeviceIndex = 0,
                DeviceLabel = "[0] Flame-S (SIM)",
                Wavelengths = new[] { 400.0, 500.0, 600.0 },
                Counts = counts,
                Dark = dark,
                WhiteReference = white,
                Absorbance = SpectrumMath.StoredReferenceAbsorbance(counts, dark, white),
                ElapsedSeconds = elapsed,
                TimestampUtc = new DateTime(2026, 10, 28, 1, 2, 3, DateTimeKind.Utc).AddSeconds(elapsed)
            };
        }

        // ---------------------------------------------------------------- method files

        [TestMethod]
        public void MethodRoundTripsEverythingThatDefinesARun()
        {
            string path = Path.Combine(_folder, "kinetics" + Method.Extension);
            var method = new Method
            {
                Name = "효소 반응 10분",
                Note = "520 nm 추적",
                MinIntervalMs = 250,
                ScansToAverage = 4,
                Smoothing = true,
                SmoothingWindow = 11,
                ShowPeaks = true,
                PeakProminence = 0.05,
                BaselineFirstAnchor = 400,
                BaselineSecondAnchor = 800
            };
            method.Display = DisplayMode.Transmittance;
            method.Baseline = BaselineMode.Linear;
            method.TraceWavelengths.Add(520.0);
            method.Devices.Add(new MethodDevice
            {
                Identity = "SIM-FLMS-0001",
                Index = 0,
                Model = "Flame-S",
                IntegrationMs = 20,
                TriggerDelayUs = 40,
                Measured = true,
                IsProbe = true,
                InCorrectionPair = true
            });

            MethodStore.Save(path, method);

            string error;
            Method loaded = MethodStore.Load(path, out error);

            Assert.IsNull(error, error);
            Assert.AreEqual("효소 반응 10분", loaded.Name);
            Assert.AreEqual(250, loaded.MinIntervalMs);
            Assert.AreEqual(4, loaded.ScansToAverage);
            Assert.AreEqual(DisplayMode.Transmittance, loaded.Display);
            Assert.AreEqual(BaselineMode.Linear, loaded.Baseline);
            Assert.IsTrue(loaded.Smoothing && loaded.ShowPeaks);
            Assert.AreEqual(11, loaded.SmoothingWindow);
            CollectionAssert.AreEqual(new[] { 520.0 }, loaded.TraceWavelengths);
            Assert.AreNotEqual(string.Empty, loaded.SavedUtc);

            MethodDevice device = loaded.FindDevice("SIM-FLMS-0001", 7);
            Assert.IsNotNull(device, "the serial number must find it whatever the index is now");
            Assert.AreEqual(20.0, device.IntegrationMs);
            Assert.IsTrue(device.IsProbe && device.Measured && device.InCorrectionPair);
        }

        [TestMethod]
        public void MethodCarriesNoReferenceSpectraOrSampleIdentity()
        {
            string path = Path.Combine(_folder, "m" + Method.Extension);
            MethodStore.Save(path, new Method { Name = "plain" });

            string xml = File.ReadAllText(path);

            foreach (string forbidden in new[] { "Dark", "WhiteReference", "Sample", "SavePath" })
                StringAssert.DoesNotMatch(xml, new System.Text.RegularExpressions.Regex("<" + forbidden + ">"),
                    forbidden + " belongs to one run, not to a method");
        }

        [TestMethod]
        public void UnknownEnumTextFallsBackInsteadOfThrowing()
        {
            string path = Path.Combine(_folder, "odd" + Method.Extension);
            File.WriteAllText(path,
                "<Method><FormatVersion>1</FormatVersion><DisplayMode>Hologram</DisplayMode>" +
                "<BaselineMode></BaselineMode></Method>");

            string error;
            Method loaded = MethodStore.Load(path, out error);

            Assert.IsNull(error, error);
            Assert.AreEqual(DisplayMode.Absorbance, loaded.Display);
            Assert.AreEqual(BaselineMode.None, loaded.Baseline);
            Assert.AreEqual(-1, loaded.MinIntervalMs, "an absent field keeps its default");
        }

        [TestMethod]
        public void ADamagedMethodFileIsReportedNotThrown()
        {
            string path = Path.Combine(_folder, "broken" + Method.Extension);
            File.WriteAllText(path, "<Method><Name>trunc");

            string error;
            Assert.IsNull(MethodStore.Load(path, out error));
            Assert.IsNotNull(error);
        }

        // ---------------------------------------------------------------- CSV import

        [TestMethod]
        public void ASavedSpectrumReadsBackWithItsAbsorbanceColumn()
        {
            string written = CsvExporter.SaveSpectrum(Path.Combine(_folder, "run"),
                new ExportMetadata { Sample = "dye, batch 1" }, Frame(), null);

            string error;
            ImportedSpectrum imported = SpectrumImport.Load(written, out error);

            Assert.IsNull(error, error);
            Assert.IsTrue(imported.IsAbsorbance, "column: " + imported.ColumnName);
            Assert.AreEqual(3, imported.Count);
            CollectionAssert.AreEqual(new[] { 400.0, 500.0, 600.0 }, imported.Wavelengths);
            Assert.AreEqual(1.0, imported.Values[0], 1e-6);
            Assert.IsTrue(double.IsNaN(imported.Values[2]), "an empty field stays missing");
            Assert.AreEqual("run", imported.Name, "the trace is named after the file, which Save names after the sample");
        }

        [TestMethod]
        public void ACountsOnlyFileIsNotTreatedAsAbsorbance()
        {
            SpectrumFrame frame = Frame();
            frame.Dark = null;
            frame.WhiteReference = null;
            frame.Absorbance = null;

            string written = CsvExporter.SaveSpectrum(Path.Combine(_folder, "counts"), new ExportMetadata(), frame, null);

            string error;
            ImportedSpectrum imported = SpectrumImport.Load(written, out error);

            Assert.IsNull(error, error);
            Assert.IsFalse(imported.IsAbsorbance);
            Assert.AreEqual(200.0, imported.Values[0], 1e-9);
        }

        [TestMethod]
        public void PlainCsvFromOtherSoftwareIsAccepted()
        {
            string error;

            ImportedSpectrum tabbed = SpectrumImport.Parse(
                new[] { "wavelength\tA", "400\t0.10", "500\t0.25" }, "other.csv", out error);
            Assert.IsNull(error, error);
            Assert.IsTrue(tabbed.IsAbsorbance);
            Assert.AreEqual(0.25, tabbed.Values[1], 1e-9);

            ImportedSpectrum headerless = SpectrumImport.Parse(
                new[] { "400,0.10", "500,0.25", "600,0.40" }, "bare.csv", out error);
            Assert.IsNull(error, error);
            Assert.AreEqual(3, headerless.Count);
            Assert.AreEqual(0.40, headerless.Values[2], 1e-9);

            Assert.IsNull(SpectrumImport.Parse(new[] { "# only a comment" }, "x.csv", out error));
            Assert.IsNotNull(error);

            Assert.IsNull(SpectrumImport.Parse(new[] { "a,b", "not,numbers" }, "x.csv", out error));
            Assert.IsNotNull(error);
        }

        // ---------------------------------------------------------------- live recording

        [TestMethod]
        public void RecordedRowsAreOnDiskBeforeRecordingStops()
        {
            using (var recorder = new TimedSeriesRecorder())
            {
                recorder.Start(Path.Combine(_folder, "live"), new ExportMetadata { Sample = "live" },
                    new[] { 400.0, 500.0, 600.0 });

                Assert.IsTrue(recorder.Append(Frame(0.0)));
                Assert.IsTrue(recorder.Append(Frame(0.5)));

                // Read while the recorder is still open: this is what survives a crash.
                string[] rows = ReadRows(recorder.AbsorbancePath);

                Assert.AreEqual(3, rows.Length, "header row plus two acquisitions");
                StringAssert.StartsWith(rows[0], "Elapsed (s),Timestamp (UTC),400.0000,500.0000,600.0000");
                StringAssert.StartsWith(rows[1], "0.0000,");
                StringAssert.StartsWith(rows[2], "0.5000,");
                Assert.AreEqual(2, recorder.RecordCount);

                string[] paths = recorder.Stop();
                Assert.AreEqual(2, paths.Length);
                Assert.IsTrue(File.Exists(paths[1]));
            }
        }

        [TestMethod]
        public void RecordedFilesCarryTheSameHeaderAsASavedFile()
        {
            var metadata = new ExportMetadata { Sample = "dye", ScansToAverage = 4, MinIntervalMs = 250 };

            using (var recorder = new TimedSeriesRecorder())
            {
                recorder.Start(Path.Combine(_folder, "live"), metadata, new[] { 400.0, 500.0, 600.0 });
                recorder.Append(Frame());
                recorder.Stop();
            }

            string[] recorded = File.ReadAllLines(Path.Combine(_folder, "live_absorbance.csv"));
            string[] savedHeader = CsvExporter.BuildHeader("Timed series - absorbance (OD)", metadata)
                .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in savedHeader.Where(l => l.StartsWith("# Sample", StringComparison.Ordinal) ||
                                                          l.StartsWith("# Scans", StringComparison.Ordinal) ||
                                                          l.StartsWith("# Min interval", StringComparison.Ordinal)))
            {
                CollectionAssert.Contains(recorded, line);
            }
        }

        [TestMethod]
        public void AFrameFromAnotherSpectrometerIsNotAppended()
        {
            using (var recorder = new TimedSeriesRecorder())
            {
                recorder.Start(Path.Combine(_folder, "live"), new ExportMetadata(), new[] { 400.0, 500.0, 600.0 });

                SpectrumFrame other = Frame();
                other.Wavelengths = new[] { 300.0, 400.0 };
                other.Absorbance = new[] { 0.1, 0.2 };
                other.Counts = new[] { 10.0, 20.0 };

                Assert.IsFalse(recorder.Append(other));
                Assert.AreEqual(0, recorder.RecordCount);
                Assert.AreEqual(1, ReadRows(recorder.AbsorbancePath).Length, "only the column header");
            }
        }

        private static string[] ReadRows(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd()
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(l => !l.StartsWith("#", StringComparison.Ordinal))
                    .ToArray();
            }
        }

        // ---------------------------------------------------------------- PNG and PDF

        private static Bitmap Plot()
        {
            var plot = new Bitmap(320, 200);
            using (Graphics g = Graphics.FromImage(plot))
            {
                g.Clear(Color.White);
                g.DrawLine(Pens.SteelBlue, 10, 190, 310, 20);
            }
            return plot;
        }

        [TestMethod]
        public void PngIsWrittenWithThePngSignature()
        {
            using (Bitmap plot = Plot())
            {
                string path = ReportExporter.SavePng(plot, Path.Combine(_folder, "plot"));

                StringAssert.EndsWith(path, ".png");
                byte[] bytes = File.ReadAllBytes(path);
                CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray());
            }
        }

        [TestMethod]
        public void TheReportPdfIsAValidOnePageFileHoldingTheRenderedPage()
        {
            var content = new ReportContent
            {
                Title = "UV-VIS 측정 보고서",
                Subtitle = "2026-10-28",
                Footer = "UV-VIS meas"
            };
            content.Add("시료", "dye, batch 1");
            content.Add("적분시간", "20 ms");

            using (Bitmap plot = Plot())
            {
                content.Plot = plot;

                using (Bitmap page = ReportExporter.RenderPage(content, 150))
                {
                    Assert.AreEqual(1240, page.Width, 2);
                    Assert.AreEqual(1754, page.Height, 2);

                    string path = ReportExporter.SavePdf(page, Path.Combine(_folder, "report"));
                    byte[] pdf = File.ReadAllBytes(path);

                    string text = Encoding.ASCII.GetString(pdf);
                    StringAssert.StartsWith(text, "%PDF-1.4");
                    StringAssert.Contains(text, "/Type /Page");
                    StringAssert.Contains(text, "/Filter /FlateDecode");
                    StringAssert.Contains(text, "startxref");
                    StringAssert.EndsWith(text.TrimEnd('\n'), "%%EOF");

                    AssertImageStreamDecodes(pdf, page.Width, page.Height);
                }
            }
        }

        /// <summary>
        /// Pulls the image stream back out of the PDF and inflates it, so the test fails if the
        /// zlib wrapper or the pixel layout is wrong - which a viewer would show as a blank page.
        /// </summary>
        private static void AssertImageStreamDecodes(byte[] pdf, int width, int height)
        {
            string text = Encoding.ASCII.GetString(pdf);
            int header = text.IndexOf("/Subtype /Image", StringComparison.Ordinal);
            Assert.IsTrue(header > 0, "no image object");

            int streamStart = text.IndexOf("stream\n", header, StringComparison.Ordinal) + "stream\n".Length;
            int streamEnd = text.IndexOf("\nendstream", streamStart, StringComparison.Ordinal);
            Assert.IsTrue(streamEnd > streamStart, "the image stream is not terminated");

            Assert.AreEqual(0x78, pdf[streamStart], "zlib header byte 1");
            Assert.AreEqual(0x9C, pdf[streamStart + 1], "zlib header byte 2");

            using (var compressed = new MemoryStream(pdf, streamStart + 2, streamEnd - streamStart - 6))
            using (var inflate = new DeflateStream(compressed, CompressionMode.Decompress))
            using (var raw = new MemoryStream())
            {
                inflate.CopyTo(raw);
                Assert.AreEqual((long)width * height * 3, raw.Length, "one RGB triple per pixel");
            }
        }
    }
}
