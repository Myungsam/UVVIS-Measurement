using System;

namespace UvVisMeasurement.Analysis
{
    /// <summary>Absorbance arithmetic.</summary>
    internal static class SpectrumMath
    {
        /// <summary>
        /// Absorbance against the dark and white reference stored for the same spectrometer:
        ///     A = -log10( (S - D) / (W - D) )
        /// </summary>
        public static double[] StoredReferenceAbsorbance(double[] sample, double[] dark, double[] reference)
        {
            if (sample == null || reference == null)
                return null;

            var result = new double[sample.Length];

            for (int i = 0; i < result.Length; i++)
            {
                if (i >= reference.Length)
                {
                    result[i] = double.NaN;
                    continue;
                }

                double s = sample[i] - Sample(dark, i);
                double r = reference[i] - Sample(dark, i);

                result[i] = (s > 0.0 && r > 0.0) ? -Math.Log10(s / r) : double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Live lamp-drift factor measured on the reference spectrometer:
        ///     f = (Sr - Dr) / (Wr - Dr)
        ///
        /// Both terms come from the same detector, so its gain, quantum efficiency and
        /// integration time all cancel: f is 1.0 while the lamp is exactly as bright as it
        /// was when the white references were captured, and scales with the lamp from
        /// there. That is what makes it transferable to the probe channel.
        ///
        /// Pixels where the reference channel has no usable signal become NaN rather than
        /// a fabricated 1.0, so they can be excluded instead of silently disabling the
        /// correction over part of the spectrum.
        /// </summary>
        public static double[] DriftFactor(double[] sample, double[] dark, double[] white)
        {
            if (sample == null || white == null)
                return null;

            var result = new double[sample.Length];

            for (int i = 0; i < result.Length; i++)
            {
                if (i >= white.Length)
                {
                    result[i] = double.NaN;
                    continue;
                }

                double d = Sample(dark, i);
                double live = sample[i] - d;
                double stored = white[i] - d;

                result[i] = (live > 0.0 && stored > 0.0) ? live / stored : double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Absorbance against a white reference that is rescaled by the live drift factor:
        ///     A = -log10( (S - D) / ((W - D) * f) )
        ///
        /// With f = 1 this is identical to <see cref="StoredReferenceAbsorbance"/>; f
        /// tracks how far the lamp has moved since W was captured, so a lamp that dims by
        /// 5% no longer shows up as 0.022 OD of absorbance across the whole spectrum.
        /// </summary>
        public static double[] DriftCorrectedAbsorbance(double[] sample, double[] dark, double[] reference,
                                                        double[] drift)
        {
            if (sample == null || reference == null || drift == null)
                return null;

            var result = new double[sample.Length];

            for (int i = 0; i < result.Length; i++)
            {
                if (i >= reference.Length || i >= drift.Length)
                {
                    result[i] = double.NaN;
                    continue;
                }

                double factor = drift[i];
                if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0.0)
                {
                    result[i] = double.NaN;
                    continue;
                }

                double d = Sample(dark, i);
                double s = sample[i] - d;
                double r = (reference[i] - d) * factor;

                result[i] = (s > 0.0 && r > 0.0) ? -Math.Log10(s / r) : double.NaN;
            }

            return result;
        }

        /// <summary>
        /// Linear interpolation of (sourceX, sourceY) onto <paramref name="targetX"/>.
        ///
        /// Two spectrometers never share a wavelength grid - a Flame-S puts 2048 pixels
        /// over 180-871 nm and an Ocean FX 2136 over 316-1031 nm - so the reference
        /// channel has to be resampled before it can be combined with the probe channel
        /// pixel by pixel.
        ///
        /// Target wavelengths outside the source range return NaN. Extrapolating past the
        /// reference detector's range would invent lamp data for wavelengths it cannot
        /// see, which is exactly the kind of silent error this correction is meant to
        /// remove; a NaN breaks the trace instead and is honest about the overlap.
        /// </summary>
        public static double[] Resample(double[] sourceX, double[] sourceY, double[] targetX)
        {
            if (sourceX == null || sourceY == null || targetX == null)
                return null;

            var result = new double[targetX.Length];
            int count = Math.Min(sourceX.Length, sourceY.Length);

            if (count < 2)
            {
                for (int i = 0; i < result.Length; i++)
                    result[i] = double.NaN;
                return result;
            }

            double first = sourceX[0];
            double last = sourceX[count - 1];
            int cursor = 0;

            for (int i = 0; i < result.Length; i++)
            {
                double x = targetX[i];
                if (double.IsNaN(x) || x < first || x > last)
                {
                    result[i] = double.NaN;
                    continue;
                }

                // Both grids are ascending, so the cursor normally only walks forward; the
                // backward step keeps the result correct if that ever stops holding.
                while (cursor < count - 2 && sourceX[cursor + 1] < x)
                    cursor++;
                while (cursor > 0 && sourceX[cursor] > x)
                    cursor--;

                double x0 = sourceX[cursor];
                double x1 = sourceX[cursor + 1];
                double span = x1 - x0;

                if (span <= 0.0)
                {
                    result[i] = sourceY[cursor];
                    continue;
                }

                double y0 = sourceY[cursor];
                double y1 = sourceY[cursor + 1];
                result[i] = y0 + (y1 - y0) * ((x - x0) / span);
            }

            return result;
        }

        /// <summary>Mean of the finite values only; NaN when nothing is finite.</summary>
        public static double FiniteMean(double[] values)
        {
            if (values == null)
                return double.NaN;

            double sum = 0.0;
            int count = 0;

            for (int i = 0; i < values.Length; i++)
            {
                double value = values[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                    continue;

                sum += value;
                count++;
            }

            return count == 0 ? double.NaN : sum / count;
        }

        /// <summary>Index of the pixel whose wavelength is closest to <paramref name="wavelength"/>.</summary>
        public static int NearestIndex(double[] wavelengths, double wavelength)
        {
            if (wavelengths == null || wavelengths.Length == 0)
                return -1;

            int best = 0;
            double bestDistance = Math.Abs(wavelengths[0] - wavelength);

            for (int i = 1; i < wavelengths.Length; i++)
            {
                double distance = Math.Abs(wavelengths[i] - wavelength);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Min/max over the finite values only. Returns false when nothing is finite.</summary>
        public static bool TryGetFiniteRange(double[] values, out double min, out double max)
        {
            min = 0.0;
            max = 0.0;
            bool any = false;

            if (values == null)
                return false;

            foreach (double value in values)
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
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

            return any;
        }

        private static double Sample(double[] array, int index)
        {
            if (array == null || index >= array.Length)
                return 0.0;

            double value = array[index];
            return double.IsNaN(value) ? 0.0 : value;
        }
    }
}
