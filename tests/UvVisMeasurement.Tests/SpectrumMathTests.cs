using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Analysis;

namespace UvVisMeasurement.Tests
{
    [TestClass]
    public class SpectrumMathTests
    {
        private const double Tolerance = 1e-12;

        [TestMethod]
        public void StoredReferenceAbsorbance_FollowsBeerLambertWithDarkSubtraction()
        {
            double[] dark = { 100, 100, 100 };
            double[] white = { 1100, 2100, 600 };
            double[] sample = { 200, 2100, 100 };

            double[] a = SpectrumMath.StoredReferenceAbsorbance(sample, dark, white);

            Assert.AreEqual(1.0, a[0], Tolerance);
            Assert.AreEqual(0.0, a[1], Tolerance);
            Assert.IsTrue(double.IsNaN(a[2]), "no signal above dark must be NaN, not infinity");
        }

        [TestMethod]
        public void StoredReferenceAbsorbance_TreatsMissingOrNaNDarkAsZero()
        {
            double[] white = { 1000, 1000 };
            double[] sample = { 100, 100 };

            double[] withoutDark = SpectrumMath.StoredReferenceAbsorbance(sample, null, white);
            double[] nanDark = SpectrumMath.StoredReferenceAbsorbance(sample, new[] { double.NaN, double.NaN }, white);

            Assert.AreEqual(1.0, withoutDark[0], Tolerance);
            Assert.AreEqual(1.0, nanDark[1], Tolerance);
        }

        [TestMethod]
        public void StoredReferenceAbsorbance_ShorterReferenceGivesNaNTail()
        {
            double[] a = SpectrumMath.StoredReferenceAbsorbance(new double[] { 10, 10, 10 }, null, new double[] { 100 });

            Assert.AreEqual(1.0, a[0], Tolerance);
            Assert.IsTrue(double.IsNaN(a[1]));
            Assert.IsTrue(double.IsNaN(a[2]));
        }

        [TestMethod]
        public void DriftCorrection_WithUnitFactorEqualsStoredReference()
        {
            double[] dark = { 50, 60 };
            double[] white = { 5000, 4000 };
            double[] sample = { 700, 900 };

            double[] plain = SpectrumMath.StoredReferenceAbsorbance(sample, dark, white);
            double[] corrected = SpectrumMath.DriftCorrectedAbsorbance(sample, dark, white, new[] { 1.0, 1.0 });

            CollectionAssert.AreEqual(plain, corrected);
        }

        [TestMethod]
        public void DriftCorrection_CancelsALampThatDimmed()
        {
            double[] dark = { 0, 0 };
            double[] white = { 1000, 1000 };
            double lamp = 0.9;
            double[] sample = { 100 * lamp, 1000 * lamp };

            double[] factor = SpectrumMath.DriftFactor(new[] { 500 * lamp, 800 * lamp }, dark, new double[] { 500, 800 });
            double[] corrected = SpectrumMath.DriftCorrectedAbsorbance(sample, dark, white, factor);

            Assert.AreEqual(lamp, factor[0], Tolerance);
            Assert.AreEqual(1.0, corrected[0], Tolerance);
            Assert.AreEqual(0.0, corrected[1], Tolerance);
        }

        [TestMethod]
        public void DriftCorrection_InvalidFactorGivesNaN()
        {
            double[] a = SpectrumMath.DriftCorrectedAbsorbance(
                new double[] { 10, 10, 10 }, null, new double[] { 100, 100, 100 },
                new[] { double.NaN, 0.0, double.PositiveInfinity });

            foreach (double value in a)
                Assert.IsTrue(double.IsNaN(value));
        }

        [TestMethod]
        public void Resample_InterpolatesLinearlyAndNeverExtrapolates()
        {
            double[] sourceX = { 400, 500, 600 };
            double[] sourceY = { 1.0, 2.0, 4.0 };

            double[] y = SpectrumMath.Resample(sourceX, sourceY, new double[] { 350, 400, 450, 550, 600, 650 });

            Assert.IsTrue(double.IsNaN(y[0]));
            Assert.AreEqual(1.0, y[1], Tolerance);
            Assert.AreEqual(1.5, y[2], Tolerance);
            Assert.AreEqual(3.0, y[3], Tolerance);
            Assert.AreEqual(4.0, y[4], Tolerance);
            Assert.IsTrue(double.IsNaN(y[5]));
        }

        [TestMethod]
        public void FiniteMeanAndRange_IgnoreNaNAndInfinity()
        {
            double[] values = { 1, double.NaN, 3, double.PositiveInfinity };

            double min, max;
            Assert.IsTrue(SpectrumMath.TryGetFiniteRange(values, out min, out max));
            Assert.AreEqual(2.0, SpectrumMath.FiniteMean(values), Tolerance);
            Assert.AreEqual(1.0, min);
            Assert.AreEqual(3.0, max);
            Assert.IsTrue(double.IsNaN(SpectrumMath.FiniteMean(new[] { double.NaN })));
        }

        [TestMethod]
        public void NearestIndex_PicksClosestWavelength()
        {
            double[] wavelengths = { 400.0, 400.5, 401.0, 401.5 };

            Assert.AreEqual(0, SpectrumMath.NearestIndex(wavelengths, 100.0));
            Assert.AreEqual(2, SpectrumMath.NearestIndex(wavelengths, 401.1));
            Assert.AreEqual(3, SpectrumMath.NearestIndex(wavelengths, 900.0));
            Assert.AreEqual(-1, SpectrumMath.NearestIndex(new double[0], 500.0));
        }
    }
}
