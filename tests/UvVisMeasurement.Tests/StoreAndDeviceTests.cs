using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;

namespace UvVisMeasurement.Tests
{
    [TestClass]
    public class StoreAndDeviceTests
    {
        private static DeviceInfo FlameS()
        {
            return new DeviceInfo
            {
                MinimumIntegrationTimeUs = 1000,
                MaximumIntegrationTimeUs = 65535000,
                TriggerDelaySupported = true,
                TriggerDelayMinimumCounts = 0,
                TriggerDelayMaximumCounts = 65535,
                TriggerDelayIncrementCounts = 1,
                MicrosecondsPerCount = 0.5
            };
        }

        [TestMethod]
        public void TriggerDelay_IsConvertedWithTheDeviceScaleAndClamped()
        {
            DeviceInfo device = FlameS();

            Assert.AreEqual(100, device.TriggerDelayCountsFor(50.0));
            Assert.AreEqual(50.0, device.TriggerDelayMicrosecondsFor(100), 1e-12);
            Assert.AreEqual(0, device.TriggerDelayCountsFor(-5.0));
            Assert.AreEqual(65535, device.TriggerDelayCountsFor(1e9));
        }

        [TestMethod]
        public void TriggerDelay_IsAlignedDownToTheIncrement()
        {
            DeviceInfo device = FlameS();
            device.TriggerDelayMinimumCounts = 10;
            device.TriggerDelayIncrementCounts = 8;
            device.MicrosecondsPerCount = 1.0;

            Assert.AreEqual(10, device.TriggerDelayCountsFor(0.0));
            Assert.AreEqual(26, device.TriggerDelayCountsFor(33.0));
        }

        [TestMethod]
        public void IntegrationTime_IsClampedToTheDeviceRange()
        {
            DeviceInfo device = FlameS();

            Assert.AreEqual(1000, device.ClampIntegrationUs(10));
            Assert.AreEqual(20000, device.ClampIntegrationUs(20000));
            Assert.AreEqual(65535000, device.ClampIntegrationUs(int.MaxValue));
        }

        [TestMethod]
        public void ReferenceStore_GeometryChangeDropsStoredSpectra()
        {
            var store = new ReferenceStore();
            DeviceReference reference = store.For(0);
            reference.EnsurePixelCount(3);
            reference.Dark = new double[3];
            reference.WhiteReference = new double[3];

            reference.EnsurePixelCount(3);
            Assert.IsTrue(reference.HasDark && reference.HasWhiteReference);

            reference.EnsurePixelCount(4);
            Assert.IsFalse(reference.HasDark || reference.HasWhiteReference);
        }

        [TestMethod]
        public void ReferenceStore_SnapshotIsACopy()
        {
            var store = new ReferenceStore();
            store.For(1).WhiteReference = new double[] { 5 };

            ReferenceSnapshot snapshot = store.Snapshot(1);
            store.For(1).WhiteReference[0] = 99;

            Assert.AreEqual(5.0, snapshot.WhiteReference[0]);
            Assert.IsNull(store.Snapshot(0).WhiteReference);
        }

        private static SpectrumFrame Frame(double[] wavelengths, double seconds)
        {
            return new SpectrumFrame
            {
                Wavelengths = wavelengths,
                Counts = new double[wavelengths.Length],
                Absorbance = new double[wavelengths.Length],
                ElapsedSeconds = seconds,
                TimestampUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds)
            };
        }

        [TestMethod]
        public void TimedSeries_DropsOldestBeyondCapacity()
        {
            var store = new TimedSeriesStore { Capacity = 3 };
            double[] grid = { 400, 500 };

            for (int i = 0; i < 5; i++)
                store.Add(Frame(grid, i));

            TimedSeriesSnapshot snapshot = store.Snapshot();
            Assert.AreEqual(3, snapshot.Count);
            Assert.AreEqual(2.0, snapshot.Records[0].ElapsedSeconds);
            Assert.AreEqual(4.0, snapshot.Records[2].ElapsedSeconds);
        }

        [TestMethod]
        public void TimedSeries_ADifferentWavelengthGridRestartsTheSeries()
        {
            var store = new TimedSeriesStore();
            store.Add(Frame(new double[] { 400, 500 }, 0));
            store.Add(Frame(new double[] { 400, 500 }, 1));
            store.Add(Frame(new double[] { 300, 600, 900 }, 2));

            Assert.AreEqual(1, store.Count);
            Assert.AreEqual(3, store.Snapshot().PixelCount);
        }
    }
}
