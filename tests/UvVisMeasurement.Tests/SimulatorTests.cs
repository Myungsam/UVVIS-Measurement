using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;

namespace UvVisMeasurement.Tests
{
    [TestClass]
    public class SimulatorTests
    {
        private const int Probe = 0;
        private const int Reference = 1;

        private double _now;

        private SimulatedSpectrometerSession Open()
        {
            var session = new SimulatedSpectrometerSession(1234, false) { Clock = () => _now };
            Assert.AreEqual(2, session.Open());

            session.ApplyAcquisition(Probe, 20000, 16, TriggerMode.Normal, 5000, false);
            session.ApplyAcquisition(Reference, 20000, 16, TriggerMode.Normal, 5000, false);
            return session;
        }

        private static double[] Read(ISpectrometerSession session, int index)
        {
            SpectrumReadStatus status;
            double[] counts = session.TryGetSpectrum(index, out status);
            Assert.AreEqual(SpectrumReadStatus.Ok, status);
            return counts;
        }

        private static void SetShutter(ISpectrometerSession session, int index, bool closed)
        {
            HardwareApplyOutcome outcome = session.ApplyHardwareChange(new HardwareChange
            {
                DeviceIndex = index,
                DeviceIdentity = session.RequireDevice(index).Identity,
                Kind = HardwareChangeKind.GpioLevel,
                Pin = SimulatedSpectrometerSession.ShutterPin,
                High = closed
            });
            Assert.IsTrue(outcome.Applied, outcome.Failure);
        }

        private static double MeanAround(double[] wavelengths, double[] values, double centre, double halfWidth)
        {
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < wavelengths.Length; i++)
            {
                if (Math.Abs(wavelengths[i] - centre) > halfWidth || double.IsNaN(values[i]))
                    continue;
                sum += values[i];
                count++;
            }
            return sum / count;
        }

        [TestMethod]
        public void Open_ReportsTwoDistinctDevicesWithDriverLikeCapabilities()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                DeviceInfo probe = session.RequireDevice(Probe);
                DeviceInfo reference = session.RequireDevice(Reference);

                Assert.IsTrue(session.IsSimulated);
                Assert.AreNotEqual(probe.Identity, reference.Identity);
                Assert.AreEqual(2048, probe.Wavelengths.Length);
                Assert.AreEqual(0.5, probe.MicrosecondsPerCount);
                Assert.AreEqual(10, probe.GpioChannels.Length);
                Assert.IsFalse(probe.GpioChannels[9].Usable, "bits past the pin count are listed but unusable");
                Assert.IsTrue(probe.MeasurementModesUnverified);
                Assert.IsFalse(reference.MeasurementModesUnverified);
                Assert.IsFalse(Array.Find(reference.MeasurementModes, m => m.Mode == TriggerMode.SingleShot).Supported);
            }
        }

        [TestMethod]
        public void ClosedShutter_LeavesOnlyTheDarkLevel()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                double[] wavelengths = session.RequireDevice(Probe).Wavelengths;
                double lit = MeanAround(wavelengths, Read(session, Probe), 600, 50);

                SetShutter(session, Probe, true);
                double dark = MeanAround(wavelengths, Read(session, Probe), 600, 50);

                Assert.AreEqual(1000.0, dark, 10.0);
                Assert.IsTrue(lit > dark + 5000, "lit " + lit + " vs dark " + dark);
            }
        }

        [TestMethod]
        public void DriftCorrectedAbsorbance_RecoversTheSimulatedSampleChange()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                double[] probeGrid = session.RequireDevice(Probe).Wavelengths;
                double[] referenceGrid = session.RequireDevice(Reference).Wavelengths;

                SetShutter(session, Probe, true);
                SetShutter(session, Reference, true);
                double[] probeDark = Read(session, Probe);
                double[] referenceDark = Read(session, Reference);
                SetShutter(session, Probe, false);
                SetShutter(session, Reference, false);

                _now = 0.0;
                double[] probeWhite = Read(session, Probe);
                double[] referenceWhite = Read(session, Reference);

                // 12 s in, the simulated lamp is about 2.5 % brighter than at the white reference.
                _now = 12.0;
                double[] sample = Read(session, Probe);
                double[] factor = SpectrumMath.Resample(referenceGrid,
                    SpectrumMath.DriftFactor(Read(session, Reference), referenceDark, referenceWhite), probeGrid);

                double[] corrected = SpectrumMath.DriftCorrectedAbsorbance(sample, probeDark, probeWhite, factor);
                double[] plain = SpectrumMath.StoredReferenceAbsorbance(sample, probeDark, probeWhite);

                double expected = 0.8 * (SimulatedSpectrometerSession.SampleStrength(12.0) -
                                         SimulatedSpectrometerSession.SampleStrength(0.0));
                double measured = MeanAround(probeGrid, corrected, 520, 3);
                double baseline = MeanAround(probeGrid, corrected, 750, 20);
                double plainBaseline = MeanAround(probeGrid, plain, 750, 20);

                Assert.AreEqual(expected, measured, 0.02);
                Assert.AreEqual(0.0, baseline, 0.01, "the lamp drift must be corrected away");
                Assert.IsTrue(Math.Abs(plainBaseline) > 0.005, "without correction the lamp drift shows: " + plainBaseline);
            }
        }

        [TestMethod]
        public void ExternalTriggerModes_TimeOutLikeHardwareWithNoTrigger()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                session.ApplyAcquisition(Probe, 20000, 1, TriggerMode.Hardware, 10, true);

                SpectrumReadStatus status;
                Assert.IsNull(session.TryGetSpectrum(Probe, out status));
                Assert.AreEqual(SpectrumReadStatus.Timeout, status);
            }
        }

        [TestMethod]
        public void LongIntegration_Saturates()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                session.ApplyAcquisition(Probe, 2000000, 1, TriggerMode.Normal, 100000, false);

                double[] counts = Read(session, Probe);

                Assert.IsTrue(session.IsSaturated(Probe));
                Assert.AreEqual(65535.0, Max(counts));
            }
        }

        [TestMethod]
        public void RejectedMode_FailsAndLeavesTheDeviceUntouched()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                HardwareApplyOutcome outcome = session.ApplyHardwareChange(new HardwareChange
                {
                    DeviceIndex = Reference,
                    DeviceIdentity = session.RequireDevice(Reference).Identity,
                    Kind = HardwareChangeKind.MeasurementMode,
                    Mode = TriggerMode.SingleShot
                });

                Assert.IsFalse(outcome.Applied);
                Assert.IsTrue(outcome.Restored);
                Read(session, Reference);
            }
        }

        [TestMethod]
        public void ChangeForAnotherSerial_IsRefused()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                HardwareApplyOutcome outcome = session.ApplyHardwareChange(new HardwareChange
                {
                    DeviceIndex = Probe,
                    DeviceIdentity = "SOMEONE-ELSE",
                    Kind = HardwareChangeKind.GpioLevel,
                    Pin = 1,
                    High = true
                });

                Assert.IsFalse(outcome.Applied);
                Assert.AreEqual(0, session.ReadGpioChannels(Probe)[1].Value);
            }
        }

        [TestMethod]
        public void TriggerDelay_UsesTheDeviceScale()
        {
            using (SimulatedSpectrometerSession session = Open())
            {
                AppliedTriggerDelay flame = session.ApplyTriggerDelay(Probe, 50.0);
                AppliedTriggerDelay oceanFx = session.ApplyTriggerDelay(Reference, 50.0);

                Assert.AreEqual(100, flame.Counts);
                Assert.AreEqual(50, oceanFx.Counts);
            }
        }

        private static double Max(double[] values)
        {
            double max = double.MinValue;
            foreach (double value in values)
                max = Math.Max(max, value);
            return max;
        }
    }

    [TestClass]
    public class AcquisitionEngineTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

        [TestMethod]
        public void SimulatedRun_DeliversFramesAndAbsorbanceAfterWhiteReference()
        {
            using (var opened = new ManualResetEventSlim())
            using (var firstFrame = new ManualResetEventSlim())
            using (var whiteStored = new ManualResetEventSlim())
            using (var absorbanceFrame = new ManualResetEventSlim())
            using (var engine = new AcquisitionEngine(() => new SimulatedSpectrometerSession(7, true)))
            {
                DeviceInfo[] devices = null;
                SpectrumFrame frame = null;
                SpectrumFrame withAbsorbance = null;
                Exception failure = null;

                engine.Failed += (s, e) => failure = e.Exception;
                engine.DevicesOpened += (s, e) => { devices = e.Devices; opened.Set(); };
                engine.ReferenceChanged += (s, e) => { if (e.Snapshot.HasWhiteReference) whiteStored.Set(); };
                engine.FrameReady += (s, e) =>
                {
                    if (frame == null)
                    {
                        frame = e.Frame;
                        firstFrame.Set();
                    }
                    if (e.Frame.HasAbsorbance && withAbsorbance == null)
                    {
                        withAbsorbance = e.Frame;
                        absorbanceFrame.Set();
                    }
                };

                engine.Start();
                engine.OpenDevices();
                Assert.IsTrue(opened.Wait(Timeout), "devices were not opened");
                Assert.AreEqual(2, devices.Length);

                var settings = new MeasurementSettings
                {
                    MinIntervalMs = 0,
                    Devices = new[]
                    {
                        new DeviceSettings { Index = 0, Enabled = true, IntegrationMs = 5 },
                        new DeviceSettings { Index = 1, IntegrationMs = 5 }
                    }
                };

                engine.StartMeasurement(settings);
                Assert.IsTrue(firstFrame.Wait(Timeout), "no frame arrived: " + failure);
                Assert.IsTrue(frame.HasSpectrum);
                Assert.IsFalse(frame.HasAbsorbance, "no white reference has been captured yet");

                engine.CaptureWhiteReference(frame);
                Assert.IsTrue(whiteStored.Wait(Timeout), "white reference was not stored: " + failure);
                Assert.IsTrue(absorbanceFrame.Wait(Timeout), "no absorbance frame arrived: " + failure);
                Assert.AreEqual(2048, withAbsorbance.Absorbance.Length);
                Assert.IsNull(failure);
            }
        }
    }
}
