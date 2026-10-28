using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Io;

namespace UvVisMeasurement.Tests
{
    [TestClass]
    public class IoTests
    {
        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "uvvis-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        private static SpectrumFrame Frame()
        {
            double[] dark = { 100, 100, 100 };
            double[] white = { 1100, 2100, 600 };
            double[] counts = { 200, 1100.5, 100 };

            return new SpectrumFrame
            {
                DeviceIndex = 0,
                DeviceLabel = "[0] Flame-S (SIM, 1)",
                Wavelengths = new[] { 400.123456, 500.0, 600.5 },
                Counts = counts,
                Dark = dark,
                WhiteReference = white,
                Absorbance = SpectrumMath.StoredReferenceAbsorbance(counts, dark, white),
                TimestampUtc = new DateTime(2026, 10, 28, 1, 2, 3, DateTimeKind.Utc)
            };
        }

        [TestMethod]
        public void SaveSpectrum_WritesTheDocumentedFormat()
        {
            var metadata = new ExportMetadata { Sample = "dye, batch 1", ScansToAverage = 3 };
            string path = CsvExporter.SaveSpectrum(Path.Combine(_folder, "s"), metadata, Frame(), null);

            Assert.IsTrue(path.EndsWith(".csv", StringComparison.Ordinal));

            byte[] bytes = File.ReadAllBytes(path);
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "the file must be UTF-8 without a BOM");

            string[] lines = File.ReadAllLines(path);
            StringAssert.StartsWith(lines[0], "# Ocean Optics UV-VIS Measurement - Single spectrum");
            CollectionAssert.Contains(lines, "# Sample,\"dye, batch 1\"");
            CollectionAssert.Contains(lines, "# Absorbance,A = -log10((S - D) / (W - D))");

            string[] data = lines.SkipWhile(l => l.StartsWith("#", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual("Wavelength (nm),Sample (counts),Dark (counts),White reference (counts),Absorbance (OD)", data[0]);
            Assert.AreEqual("400.1235,200,100,1100,1", data[1]);
            Assert.AreEqual("500.0000,1100.5,100,2100,0.3008129", data[2]);
            Assert.AreEqual("600.5000,100,100,600,", data[3], "NaN absorbance is an empty field");
        }

        [TestMethod]
        public void SaveSpectrum_AbsorbanceCanBeRecomputedFromTheStoredColumns()
        {
            string path = CsvExporter.SaveSpectrum(Path.Combine(_folder, "s.csv"), new ExportMetadata(), Frame(), null);

            string[] row = File.ReadAllLines(path).SkipWhile(l => l.StartsWith("#", StringComparison.Ordinal))
                               .Skip(2).First().Split(',');
            double s = double.Parse(row[1], CultureInfo.InvariantCulture);
            double d = double.Parse(row[2], CultureInfo.InvariantCulture);
            double w = double.Parse(row[3], CultureInfo.InvariantCulture);
            double a = double.Parse(row[4], CultureInfo.InvariantCulture);

            Assert.AreEqual(-Math.Log10((s - d) / (w - d)), a, 1e-7);
        }

        [TestMethod]
        public void SaveTimedSeries_WritesAbsorbanceAndCountsMatrices()
        {
            var store = new TimedSeriesStore();
            SpectrumFrame frame = Frame();
            store.Add(frame);
            frame.ElapsedSeconds = 1.5;
            store.Add(frame);

            string[] paths = CsvExporter.SaveTimedSeries(Path.Combine(_folder, "t.csv"), new ExportMetadata(), store.Snapshot());

            Assert.AreEqual(2, paths.Length);
            StringAssert.EndsWith(paths[0], "t_absorbance.csv");
            StringAssert.EndsWith(paths[1], "t_counts.csv");

            string[] data = File.ReadAllLines(paths[0]).SkipWhile(l => l.StartsWith("#", StringComparison.Ordinal)).ToArray();
            Assert.AreEqual("Elapsed (s),Timestamp (UTC),400.1235,500.0000,600.5000", data[0]);
            Assert.AreEqual(3, data.Length);
            StringAssert.StartsWith(data[2], "1.5000,");
        }

        [TestMethod]
        public void BuildTable_UsesSameNumberFormatsAndLeavesGapsEmpty()
        {
            string table = CsvExporter.BuildTable("Wavelength (nm)", new[] { "A", "B" },
                new[] { 1.0, 2.0 }, new[] { new[] { 0.5, double.NaN }, new[] { 1e-9 } });

            string[] lines = table.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual("Wavelength (nm),A,B", lines[0]);
            Assert.AreEqual("1.0000,0.5,1E-09", lines[1]);
            Assert.AreEqual("2.0000,,", lines[2]);
        }

        [TestMethod]
        public void BuildPath_SanitisesTheSampleName()
        {
            string path = CsvExporter.BuildPath(_folder, "a/b:c", "spectrum");

            StringAssert.StartsWith(Path.GetFileName(path), "a_b_c_spectrum_");
            Assert.AreEqual(_folder, Path.GetDirectoryName(path));
        }

        [TestMethod]
        public void UserSettings_RoundTrip()
        {
            string path = Path.Combine(_folder, "nested", UserSettingsStore.FileName);
            var settings = new UserSettings
            {
                SavePath = @"D:\data",
                Sample = "Rhodamine 6G",
                MinIntervalMs = 250,
                ScansToAverage = 4,
                WindowWidth = 1200,
                WindowHeight = 800,
                WindowMaximized = true,
                MainTabIndex = 2
            };
            settings.TraceWavelengths.Add(530.0);
            settings.Remember(new DeviceUserSettings { Identity = "FLMS1", IntegrationMs = 12.5, TriggerDelayUs = 40 });

            UserSettingsStore.Save(path, settings);
            UserSettingsStore.Save(path, settings);

            string error;
            UserSettings loaded = UserSettingsStore.Load(path, out error);

            Assert.IsNull(error);
            Assert.AreEqual(@"D:\data", loaded.SavePath);
            Assert.AreEqual("Rhodamine 6G", loaded.Sample);
            Assert.AreEqual(250, loaded.MinIntervalMs);
            Assert.AreEqual(4, loaded.ScansToAverage);
            Assert.IsTrue(loaded.HasWindowBounds && loaded.WindowMaximized);
            Assert.AreEqual(2, loaded.MainTabIndex);
            CollectionAssert.AreEqual(new[] { 530.0 }, loaded.TraceWavelengths);
            Assert.AreEqual(12.5, loaded.FindDevice("FLMS1").IntegrationMs);
            Assert.IsNull(loaded.FindDevice("OTHER"));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [TestMethod]
        public void UserSettings_MissingFileGivesDefaults()
        {
            string error;
            UserSettings loaded = UserSettingsStore.Load(Path.Combine(_folder, "none.xml"), out error);

            Assert.IsNull(error);
            Assert.AreEqual(-1, loaded.MinIntervalMs);
            Assert.AreEqual(-1, loaded.MainTabIndex);
            Assert.IsFalse(loaded.HasWindowBounds);
        }

        [TestMethod]
        public void UserSettings_DamagedFileIsReportedAndReset()
        {
            string path = Path.Combine(_folder, UserSettingsStore.FileName);
            File.WriteAllText(path, "<UserSettings><SavePath>trunc");

            string error;
            UserSettings loaded = UserSettingsStore.Load(path, out error);

            Assert.IsNotNull(error);
            Assert.IsNull(loaded.SavePath);
            Assert.AreEqual(-1, loaded.ScansToAverage);
        }

        [TestMethod]
        public void UserSettings_FileWithoutNewerFieldsKeepsTheirDefaults()
        {
            string path = Path.Combine(_folder, UserSettingsStore.FileName);
            File.WriteAllText(path,
                "<UserSettings xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><FormatVersion>1</FormatVersion>" +
                "<SavePath>C:\\x</SavePath></UserSettings>");

            string error;
            UserSettings loaded = UserSettingsStore.Load(path, out error);

            Assert.IsNull(error, error);
            Assert.AreEqual(@"C:\x", loaded.SavePath);
            Assert.AreEqual(-1, loaded.MinIntervalMs);
            Assert.IsNotNull(loaded.Devices);
        }

        [TestMethod]
        public void UserSettings_RememberReplacesOnlyTheSameDevice()
        {
            var settings = new UserSettings();
            settings.Remember(new DeviceUserSettings { Identity = "A", IntegrationMs = 1 });
            settings.Remember(new DeviceUserSettings { Identity = "B", IntegrationMs = 2 });
            settings.Remember(new DeviceUserSettings { Identity = "A", IntegrationMs = 3 });

            Assert.AreEqual(2, settings.Devices.Count);
            Assert.AreEqual(3.0, settings.FindDevice("A").IntegrationMs);
            Assert.AreEqual(2.0, settings.FindDevice("B").IntegrationMs);
        }
    }
}
