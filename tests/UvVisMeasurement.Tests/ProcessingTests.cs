using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UvVisMeasurement.Analysis;

namespace UvVisMeasurement.Tests
{
    [TestClass]
    public class ProcessingTests
    {
        private static double[] Grid(int count, double from, double to)
        {
            var x = new double[count];
            for (int i = 0; i < count; i++)
                x[i] = from + (to - from) * i / (count - 1);

            return x;
        }

        private static double[] Map(double[] x, Func<double, double> f)
        {
            var y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
                y[i] = f(x[i]);

            return y;
        }

        private static double Gaussian(double x, double centre, double width, double height)
        {
            double z = (x - centre) / width;
            return height * Math.Exp(-0.5 * z * z);
        }

        // ---------------------------------------------------------------- display modes

        [TestMethod]
        public void TransmittanceFollowsTheAbsorbanceDefinition()
        {
            double[] t = SpectrumProcessing.ToTransmittance(new[] { 0.0, 1.0, 2.0, double.NaN });

            Assert.AreEqual(100.0, t[0], 1e-9);
            Assert.AreEqual(10.0, t[1], 1e-9);
            Assert.AreEqual(1.0, t[2], 1e-9);
            Assert.IsTrue(double.IsNaN(t[3]));
            Assert.IsNull(SpectrumProcessing.ToTransmittance(null));
        }

        // ---------------------------------------------------------------- smoothing

        [TestMethod]
        public void SmoothingReproducesAPolynomialItCanFit()
        {
            double[] x = Grid(60, 0, 59);
            double[] y = Map(x, v => 3.0 + 0.5 * v - 0.01 * v * v);

            double[] smoothed = SpectrumProcessing.SavitzkyGolay(y, 11, 2);

            for (int i = 0; i < y.Length; i++)
                Assert.AreEqual(y[i], smoothed[i], 1e-6, "point " + i + " (including the ends)");
        }

        [TestMethod]
        public void SmoothingKeepsPeakHeightWhileRemovingNoise()
        {
            double[] x = Grid(401, 300, 700);
            double[] clean = Map(x, v => Gaussian(v, 500, 20, 1.0));

            var random = new Random(4242);
            var noisy = new double[clean.Length];
            for (int i = 0; i < clean.Length; i++)
                noisy[i] = clean[i] + (random.NextDouble() - 0.5) * 0.08;

            double[] smoothed = SpectrumProcessing.SavitzkyGolay(noisy, 15, 2);

            double noisyError = 0.0, smoothedError = 0.0;
            for (int i = 0; i < clean.Length; i++)
            {
                noisyError += Math.Abs(noisy[i] - clean[i]);
                smoothedError += Math.Abs(smoothed[i] - clean[i]);
            }

            Assert.IsTrue(smoothedError < noisyError / 2.0,
                "smoothing should at least halve the deviation: " + smoothedError + " vs " + noisyError);

            int peak = SpectrumMath.NearestIndex(x, 500.0);
            Assert.AreEqual(1.0, smoothed[peak], 0.02, "the peak must keep its height");
        }

        [TestMethod]
        public void SmoothingLeavesShortSpectraAndBadNumbersAlone()
        {
            double[] tooShort = { 1.0, 2.0, 3.0 };
            CollectionAssert.AreEqual(tooShort, SpectrumProcessing.SavitzkyGolay(tooShort, 11, 2));

            var withHole = new double[30];
            for (int i = 0; i < withHole.Length; i++)
                withHole[i] = i;
            withHole[10] = double.NaN;

            double[] smoothed = SpectrumProcessing.SavitzkyGolay(withHole, 7, 2);

            Assert.IsTrue(double.IsNaN(smoothed[10]), "a gap stays a gap");
            Assert.AreEqual(25.0, smoothed[25], 1e-6, "points away from the gap are still smoothed");
        }

        // ---------------------------------------------------------------- baseline

        [TestMethod]
        public void OffsetBaselineZeroesTheChosenWavelength()
        {
            double[] x = Grid(101, 400, 500);
            double[] y = Map(x, v => 0.2 + Gaussian(v, 450, 10, 1.0));

            double[] baseline = SpectrumProcessing.Baseline(x, y, BaselineMode.Offset, 400.0, 0.0);
            double[] corrected = SpectrumProcessing.Subtract(y, baseline);

            Assert.AreEqual(0.0, corrected[0], 1e-9, "the anchor itself must read exactly zero");

            // Not exactly 1.0: the anchor at 400 nm still carries the Gaussian's own tail,
            // which the offset removes along with the background.
            Assert.AreEqual(1.0, corrected[SpectrumMath.NearestIndex(x, 450.0)], 1e-4);
        }

        [TestMethod]
        public void LinearBaselineRemovesASlopeBetweenTwoAnchors()
        {
            double[] x = Grid(201, 300, 500);
            double[] y = Map(x, v => 0.5 - 0.001 * (v - 300.0) + Gaussian(v, 400, 8, 0.8));

            double[] baseline = SpectrumProcessing.Baseline(x, y, BaselineMode.Linear, 300.0, 500.0);
            double[] corrected = SpectrumProcessing.Subtract(y, baseline);

            Assert.AreEqual(0.0, corrected[0], 1e-9);
            Assert.AreEqual(0.0, corrected[corrected.Length - 1], 1e-9);
            Assert.AreEqual(0.8, corrected[SpectrumMath.NearestIndex(x, 400.0)], 1e-6);
        }

        [TestMethod]
        public void LinearBaselineIsRefusedWhenBothAnchorsAreTheSamePixel()
        {
            double[] x = Grid(50, 400, 500);
            Assert.IsNull(SpectrumProcessing.Baseline(x, Map(x, v => 1.0), BaselineMode.Linear, 450.0, 450.0));
            Assert.IsNull(SpectrumProcessing.Baseline(x, Map(x, v => 1.0), BaselineMode.None, 400.0, 500.0));
        }

        [TestMethod]
        public void RubberbandFollowsACurvedBackgroundWithoutEatingThePeak()
        {
            double[] x = Grid(401, 300, 700);
            double[] background = Map(x, v => 0.3 + 0.4 * Math.Exp(-(v - 300.0) / 150.0));
            var y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
                y[i] = background[i] + Gaussian(x[i], 500, 15, 1.0);

            double[] baseline = SpectrumProcessing.Baseline(x, y, BaselineMode.Rubberband, 0, 0);
            double[] corrected = SpectrumProcessing.Subtract(y, baseline);

            double minimum = double.MaxValue;
            for (int i = 0; i < corrected.Length; i++)
                minimum = Math.Min(minimum, corrected[i]);

            Assert.IsTrue(minimum >= -1e-9, "the rubber band must stay under the curve, lowest point " + minimum);
            Assert.AreEqual(1.0, corrected[SpectrumMath.NearestIndex(x, 500.0)], 0.05);

            int edge = SpectrumMath.NearestIndex(x, 680.0);
            Assert.AreEqual(0.0, corrected[edge], 0.02, "away from the band the background should be gone");
        }

        // ---------------------------------------------------------------- peaks

        [TestMethod]
        public void PeaksAreFoundSortedByWavelengthAndRankedByProminence()
        {
            double[] x = Grid(601, 300, 900);
            var y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                y[i] = Gaussian(x[i], 420, 12, 0.4) +
                       Gaussian(x[i], 520, 18, 1.0) +
                       Gaussian(x[i], 700, 10, 0.02);
            }

            Peak[] peaks = SpectrumProcessing.FindPeaks(x, y, 0.05, 10);

            Assert.AreEqual(2, peaks.Length, "the 0.02 ripple is below the prominence threshold");
            Assert.AreEqual(420.0, peaks[0].Wavelength, 1.5);
            Assert.AreEqual(520.0, peaks[1].Wavelength, 1.5);
            Assert.AreEqual(1.0, peaks[1].Value, 0.01);
            Assert.IsTrue(peaks[1].Prominence > peaks[0].Prominence);

            Peak[] strongest = SpectrumProcessing.FindPeaks(x, y, 0.05, 1);
            Assert.AreEqual(1, strongest.Length);
            Assert.AreEqual(520.0, strongest[0].Wavelength, 1.5);
        }

        [TestMethod]
        public void AShoulderThatIsNotALocalMaximumIsNotReported()
        {
            double[] x = Grid(601, 300, 900);
            var y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
                y[i] = Gaussian(x[i], 500, 40, 1.0) + Gaussian(x[i], 560, 12, 0.08);

            // A bump riding on the steep flank of a larger band never turns the slope around,
            // so no local maximum exists there and no threshold can reveal one. The operator
            // sees a shoulder; the peak list honestly reports the one band.
            Assert.AreEqual(1, SpectrumProcessing.FindPeaks(x, y, 0.05, 10).Length);
            Assert.AreEqual(1, SpectrumProcessing.FindPeaks(x, y, 0.000001, 10).Length);
        }

        [TestMethod]
        public void NoPeaksInAFlatOrEmptySpectrum()
        {
            double[] x = Grid(50, 400, 500);
            Assert.AreEqual(0, SpectrumProcessing.FindPeaks(x, Map(x, v => 1.0), 0.01, 10).Length);
            Assert.AreEqual(0, SpectrumProcessing.FindPeaks(null, null, 0.01, 10).Length);
        }

        // ---------------------------------------------------------------- Beer-Lambert

        [TestMethod]
        public void ConcentrationTextIsReadWithItsUnit()
        {
            double value;

            Assert.IsTrue(BeerLambert.TryParseConcentration("0.05", out value));
            Assert.AreEqual(5e-5, value, 1e-12, "a bare number is mM, as the field label says");

            Assert.IsTrue(BeerLambert.TryParseConcentration("0.05 mM", out value));
            Assert.AreEqual(5e-5, value, 1e-12);

            Assert.IsTrue(BeerLambert.TryParseConcentration("50 uM", out value));
            Assert.AreEqual(5e-5, value, 1e-12);

            Assert.IsTrue(BeerLambert.TryParseConcentration("1.5 M", out value));
            Assert.AreEqual(1.5, value, 1e-12);

            Assert.IsTrue(BeerLambert.TryParseConcentration("2e-3 M", out value));
            Assert.AreEqual(2e-3, value, 1e-15);

            Assert.IsFalse(BeerLambert.TryParseConcentration("TBA", out value));
            Assert.IsFalse(BeerLambert.TryParseConcentration("5 cats", out value));
            Assert.IsFalse(BeerLambert.TryParseConcentration(null, out value));
        }

        [TestMethod]
        public void PathLengthTextIsReadWithItsUnit()
        {
            double value;

            Assert.IsTrue(BeerLambert.TryParsePathLength("10", out value));
            Assert.AreEqual(1.0, value, 1e-12, "a bare number is mm");

            Assert.IsTrue(BeerLambert.TryParsePathLength("10 mm", out value));
            Assert.AreEqual(1.0, value, 1e-12);

            Assert.IsTrue(BeerLambert.TryParsePathLength("1 cm", out value));
            Assert.AreEqual(1.0, value, 1e-12);

            Assert.IsFalse(BeerLambert.TryParsePathLength("thick", out value));
        }

        [TestMethod]
        public void MolarAbsorptivityAndConcentrationAreInverses()
        {
            double epsilon = BeerLambert.MolarAbsorptivity(0.5, 1e-4, 1.0);
            Assert.AreEqual(5000.0, epsilon, 1e-9);

            Assert.AreEqual(1e-4, BeerLambert.Concentration(0.5, epsilon, 1.0), 1e-15);
            Assert.AreEqual(2e-4, BeerLambert.Concentration(1.0, epsilon, 1.0), 1e-15);

            Assert.IsTrue(double.IsNaN(BeerLambert.MolarAbsorptivity(0.5, 0.0, 1.0)));
            Assert.IsTrue(double.IsNaN(BeerLambert.MolarAbsorptivity(double.NaN, 1e-4, 1.0)));
            Assert.IsTrue(double.IsNaN(BeerLambert.Concentration(0.5, 0.0, 1.0)));
        }

        [TestMethod]
        public void ConcentrationIsShownInAReadableUnit()
        {
            StringAssert.EndsWith(BeerLambert.DescribeConcentration(1.5), "M");
            Assert.AreEqual("0.5 mM", BeerLambert.DescribeConcentration(5e-4));
            Assert.AreEqual("50 uM", BeerLambert.DescribeConcentration(5e-5));
            StringAssert.EndsWith(BeerLambert.DescribeConcentration(5e-10), "nM");
            Assert.AreEqual("-", BeerLambert.DescribeConcentration(double.NaN));
        }
    }
}
