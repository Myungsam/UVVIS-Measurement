using System;
using System.Collections.Generic;

namespace UvVisMeasurement.Analysis
{
    /// <summary>What the spectrum plot shows. The stored data is always absorbance and counts.</summary>
    internal enum DisplayMode
    {
        Absorbance,
        Transmittance,
        Counts
    }

    internal enum BaselineMode
    {
        None,

        /// <summary>Subtract the value at one wavelength, so that point reads zero.</summary>
        Offset,

        /// <summary>Subtract the straight line through two wavelengths.</summary>
        Linear,

        /// <summary>Subtract the taut line under the whole spectrum (convex-hull "rubber band").</summary>
        Rubberband
    }

    internal sealed class Peak
    {
        public int Index;
        public double Wavelength;
        public double Value;

        /// <summary>How far this peak rises above the higher of the two valleys around it.</summary>
        public double Prominence;
    }

    /// <summary>
    /// Display conversions and the optional processing steps applied to what is plotted:
    /// smoothing, baseline subtraction and peak finding.
    ///
    /// None of this touches the stored spectra or the CSV files - the exported numbers stay
    /// the ones the spectrometer produced, so a processed view can never be mistaken for
    /// measured data.
    /// </summary>
    internal static class SpectrumProcessing
    {
        /// <summary>%T = 100 x 10^-A, the definition absorbance comes from.</summary>
        public static double[] ToTransmittance(double[] absorbance)
        {
            if (absorbance == null)
                return null;

            var result = new double[absorbance.Length];
            for (int i = 0; i < result.Length; i++)
            {
                double a = absorbance[i];
                result[i] = double.IsNaN(a) || double.IsInfinity(a) ? double.NaN : 100.0 * Math.Pow(10.0, -a);
            }

            return result;
        }

        /// <summary>
        /// Savitzky-Golay smoothing: each point is replaced by the value of a least-squares
        /// polynomial fitted over a window around it. Unlike a moving average this keeps the
        /// height and position of a peak, which is the whole point for spectra.
        ///
        /// <paramref name="window"/> is forced odd and at least <paramref name="order"/> + 2
        /// points; the half window at each end is fitted from the one-sided data rather than
        /// padded, so the ends are not dragged towards zero.
        /// </summary>
        public static double[] SavitzkyGolay(double[] values, int window, int order)
        {
            if (values == null)
                return null;

            order = Math.Max(0, order);
            window = Math.Max(order + 2, window);
            if (window % 2 == 0)
                window++;

            int half = window / 2;
            if (values.Length < window)
                return (double[])values.Clone();

            var result = new double[values.Length];
            var cache = new Dictionary<int, double[]>();

            for (int i = 0; i < values.Length; i++)
            {
                // Away from the ends every point shares the same centred window, so its
                // coefficients are computed once; the ends shift the window instead.
                int offset = i < half ? i - half : (i > values.Length - 1 - half ? i - (values.Length - 1 - half) : 0);
                double[] coefficients;
                if (!cache.TryGetValue(offset, out coefficients))
                {
                    coefficients = Coefficients(half, order, offset);
                    cache[offset] = coefficients;
                }

                int start = i - half - offset;
                double sum = 0.0;
                bool usable = true;

                for (int k = 0; k < window; k++)
                {
                    double value = values[start + k];
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        usable = false;
                        break;
                    }

                    sum += coefficients[k] * value;
                }

                result[i] = usable ? sum : values[i];
            }

            return result;
        }

        /// <summary>
        /// Row of (A^T A)^-1 A^T that evaluates the fitted polynomial at <paramref name="at"/>,
        /// where A holds the powers of the sample positions -half..half.
        /// </summary>
        private static double[] Coefficients(int half, int order, int at)
        {
            int window = 2 * half + 1;
            int terms = order + 1;

            var normal = new double[terms, terms];
            for (int r = 0; r < terms; r++)
            {
                for (int c = 0; c < terms; c++)
                {
                    double sum = 0.0;
                    for (int x = -half; x <= half; x++)
                        sum += Math.Pow(x, r + c);

                    normal[r, c] = sum;
                }
            }

            // Solving for the powers of `at` gives the fitted value there directly.
            var target = new double[terms];
            for (int r = 0; r < terms; r++)
                target[r] = Math.Pow(at, r);

            double[] weights = SolveSymmetric(normal, target);

            var coefficients = new double[window];
            for (int k = 0; k < window; k++)
            {
                int x = k - half;
                double sum = 0.0;
                for (int r = 0; r < terms; r++)
                    sum += weights[r] * Math.Pow(x, r);

                coefficients[k] = sum;
            }

            return coefficients;
        }

        /// <summary>Gaussian elimination with partial pivoting; the matrix is small (order+1).</summary>
        private static double[] SolveSymmetric(double[,] matrix, double[] target)
        {
            int n = target.Length;
            var a = new double[n, n + 1];

            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                    a[r, c] = matrix[r, c];

                a[r, n] = target[r];
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col]))
                        pivot = r;

                if (pivot != col)
                {
                    for (int c = col; c <= n; c++)
                    {
                        double swap = a[col, c];
                        a[col, c] = a[pivot, c];
                        a[pivot, c] = swap;
                    }
                }

                double diagonal = a[col, col];
                if (Math.Abs(diagonal) < 1e-12)
                    continue;

                for (int r = 0; r < n; r++)
                {
                    if (r == col)
                        continue;

                    double factor = a[r, col] / diagonal;
                    for (int c = col; c <= n; c++)
                        a[r, c] -= factor * a[col, c];
                }
            }

            var solution = new double[n];
            for (int r = 0; r < n; r++)
                solution[r] = Math.Abs(a[r, r]) < 1e-12 ? 0.0 : a[r, n] / a[r, r];

            return solution;
        }

        /// <summary>
        /// The baseline to subtract, on the same grid as the spectrum. Returns null when
        /// nothing should be subtracted, so callers can skip the work entirely.
        /// </summary>
        public static double[] Baseline(double[] wavelengths, double[] values, BaselineMode mode,
                                        double firstAnchor, double secondAnchor)
        {
            if (wavelengths == null || values == null || values.Length == 0 || mode == BaselineMode.None)
                return null;

            switch (mode)
            {
                case BaselineMode.Offset:
                {
                    double level = ValueAt(wavelengths, values, firstAnchor);
                    if (double.IsNaN(level))
                        return null;

                    var flat = new double[values.Length];
                    for (int i = 0; i < flat.Length; i++)
                        flat[i] = level;

                    return flat;
                }

                case BaselineMode.Linear:
                {
                    int left = SpectrumMath.NearestIndex(wavelengths, firstAnchor);
                    int right = SpectrumMath.NearestIndex(wavelengths, secondAnchor);
                    if (left < 0 || right < 0 || left == right)
                        return null;

                    if (left > right)
                    {
                        int swap = left;
                        left = right;
                        right = swap;
                    }

                    double x0 = wavelengths[left], x1 = wavelengths[right];
                    double y0 = values[left], y1 = values[right];
                    if (double.IsNaN(y0) || double.IsNaN(y1) || Math.Abs(x1 - x0) < 1e-12)
                        return null;

                    double slope = (y1 - y0) / (x1 - x0);
                    var line = new double[values.Length];
                    for (int i = 0; i < line.Length; i++)
                        line[i] = y0 + slope * (wavelengths[i] - x0);

                    return line;
                }

                default:
                    return Rubberband(wavelengths, values);
            }
        }

        /// <summary>
        /// Lower convex hull of the spectrum, interpolated back onto every pixel: the line a
        /// rubber band would take if stretched under the curve. It needs no anchor from the
        /// operator, which is why it is the default offered for a spectrum with drifting
        /// background.
        /// </summary>
        private static double[] Rubberband(double[] wavelengths, double[] values)
        {
            var hull = new List<int>();

            for (int i = 0; i < values.Length; i++)
            {
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    continue;

                // Drop the previous vertex while it sits above the line to the new point.
                while (hull.Count >= 2 && !TurnsUp(wavelengths, values, hull[hull.Count - 2], hull[hull.Count - 1], i))
                    hull.RemoveAt(hull.Count - 1);

                hull.Add(i);
            }

            if (hull.Count < 2)
                return null;

            var baseline = new double[values.Length];
            for (int i = 0; i < baseline.Length; i++)
                baseline[i] = double.NaN;

            for (int segment = 0; segment + 1 < hull.Count; segment++)
            {
                int from = hull[segment], to = hull[segment + 1];
                double x0 = wavelengths[from], x1 = wavelengths[to];
                double y0 = values[from], y1 = values[to];
                double span = x1 - x0;

                for (int i = from; i <= to; i++)
                    baseline[i] = span <= 0.0 ? y0 : y0 + (y1 - y0) * ((wavelengths[i] - x0) / span);
            }

            double firstKnown = baseline[hull[0]];
            double lastKnown = baseline[hull[hull.Count - 1]];
            for (int i = 0; i < hull[0]; i++)
                baseline[i] = firstKnown;
            for (int i = hull[hull.Count - 1] + 1; i < baseline.Length; i++)
                baseline[i] = lastKnown;

            return baseline;
        }

        private static bool TurnsUp(double[] x, double[] y, int a, int b, int c)
        {
            double cross = (x[b] - x[a]) * (y[c] - y[a]) - (y[b] - y[a]) * (x[c] - x[a]);
            return cross > 0.0;
        }

        public static double[] Subtract(double[] values, double[] baseline)
        {
            if (values == null)
                return null;
            if (baseline == null)
                return values;

            var result = new double[values.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = i < baseline.Length && !double.IsNaN(baseline[i])
                    ? values[i] - baseline[i]
                    : values[i];
            }

            return result;
        }

        /// <summary>
        /// Local maxima whose prominence reaches <paramref name="minimumProminence"/>, strongest
        /// first and at most <paramref name="maximumCount"/> of them.
        ///
        /// Prominence - the drop to the higher of the two surrounding valleys - is used rather
        /// than plain height because it is what separates a real band from ripples riding on a
        /// large one, and it does not change when a baseline is subtracted.
        /// </summary>
        public static Peak[] FindPeaks(double[] wavelengths, double[] values, double minimumProminence,
                                       int maximumCount)
        {
            var peaks = new List<Peak>();
            if (wavelengths == null || values == null || values.Length < 3)
                return peaks.ToArray();

            for (int i = 1; i < values.Length - 1; i++)
            {
                double value = values[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;

                if (!(value > values[i - 1]) && !(value > values[i + 1]))
                    continue;
                if (value < values[i - 1] || value < values[i + 1])
                    continue;

                double prominence = Prominence(values, i);
                if (double.IsNaN(prominence) || prominence < minimumProminence)
                    continue;

                peaks.Add(new Peak
                {
                    Index = i,
                    Wavelength = i < wavelengths.Length ? wavelengths[i] : double.NaN,
                    Value = value,
                    Prominence = prominence
                });
            }

            peaks.Sort((a, b) => b.Prominence.CompareTo(a.Prominence));
            if (maximumCount > 0 && peaks.Count > maximumCount)
                peaks.RemoveRange(maximumCount, peaks.Count - maximumCount);

            peaks.Sort((a, b) => a.Index.CompareTo(b.Index));
            return peaks.ToArray();
        }

        private static double Prominence(double[] values, int peak)
        {
            double height = values[peak];
            double leftFloor = height;
            for (int i = peak - 1; i >= 0; i--)
            {
                double value = values[i];
                if (double.IsNaN(value))
                    continue;
                if (value > height)
                    break;

                leftFloor = Math.Min(leftFloor, value);
            }

            double rightFloor = height;
            for (int i = peak + 1; i < values.Length; i++)
            {
                double value = values[i];
                if (double.IsNaN(value))
                    continue;
                if (value > height)
                    break;

                rightFloor = Math.Min(rightFloor, value);
            }

            return height - Math.Max(leftFloor, rightFloor);
        }

        public static double ValueAt(double[] wavelengths, double[] values, double wavelength)
        {
            int index = SpectrumMath.NearestIndex(wavelengths, wavelength);
            if (index < 0 || values == null || index >= values.Length)
                return double.NaN;

            return values[index];
        }
    }
}
