using System;
using System.Globalization;

namespace UvVisMeasurement.Analysis
{
    /// <summary>
    /// Beer-Lambert at one wavelength: A = e c l.
    ///
    /// The operator types a concentration and a cell thickness in whatever unit suits the
    /// sample, so the text is parsed here with its unit and converted to mol/L and cm - the
    /// units the molar absorptivity e is quoted in (L mol^-1 cm^-1). Getting that conversion
    /// wrong is the easiest way to be out by a factor of 1000, so it is done in one place
    /// and tested.
    /// </summary>
    internal static class BeerLambert
    {
        /// <summary>mol/L from a number with a unit: M, mM, uM/µM, nM. Bare numbers are mM.</summary>
        public static bool TryParseConcentration(string text, out double molesPerLitre)
        {
            molesPerLitre = double.NaN;

            double value;
            string unit;
            if (!Split(text, out value, out unit))
                return false;

            switch (unit)
            {
                case "":
                case "mm":
                case "mmol/l":
                    molesPerLitre = value * 1e-3;
                    return true;
                case "m":
                case "mol/l":
                    molesPerLitre = value;
                    return true;
                case "um":
                case "µm":
                case "umol/l":
                case "µmol/l":
                    molesPerLitre = value * 1e-6;
                    return true;
                case "nm":
                case "nmol/l":
                    molesPerLitre = value * 1e-9;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>cm from a number with a unit: cm, mm, m. A bare number is mm.</summary>
        public static bool TryParsePathLength(string text, out double centimetres)
        {
            centimetres = double.NaN;

            double value;
            string unit;
            if (!Split(text, out value, out unit))
                return false;

            switch (unit)
            {
                case "":
                case "mm":
                    centimetres = value * 0.1;
                    return true;
                case "cm":
                    centimetres = value;
                    return true;
                case "m":
                    centimetres = value * 100.0;
                    return true;
                default:
                    return false;
            }
        }

        private static bool Split(string text, out double value, out string unit)
        {
            value = double.NaN;
            unit = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string trimmed = text.Trim();
            int end = 0;
            while (end < trimmed.Length &&
                   (char.IsDigit(trimmed[end]) || trimmed[end] == '.' || trimmed[end] == '-' ||
                    trimmed[end] == '+' || trimmed[end] == 'e' || trimmed[end] == 'E'))
            {
                end++;
            }

            // An 'e' that is not followed by a digit or sign starts the unit, not an exponent.
            while (end > 0 && (trimmed[end - 1] == 'e' || trimmed[end - 1] == 'E'))
                end--;

            if (end == 0)
                return false;

            if (!double.TryParse(trimmed.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;

            unit = trimmed.Substring(end).Trim().ToLowerInvariant();
            return true;
        }

        /// <summary>e = A / (c l), in L mol^-1 cm^-1. NaN when the inputs cannot give one.</summary>
        public static double MolarAbsorptivity(double absorbance, double molesPerLitre, double centimetres)
        {
            if (!Finite(absorbance) || !Finite(molesPerLitre) || !Finite(centimetres))
                return double.NaN;

            double denominator = molesPerLitre * centimetres;
            return Math.Abs(denominator) < 1e-30 ? double.NaN : absorbance / denominator;
        }

        /// <summary>c = A / (e l), in mol/L.</summary>
        public static double Concentration(double absorbance, double molarAbsorptivity, double centimetres)
        {
            if (!Finite(absorbance) || !Finite(molarAbsorptivity) || !Finite(centimetres))
                return double.NaN;

            double denominator = molarAbsorptivity * centimetres;
            return Math.Abs(denominator) < 1e-30 ? double.NaN : absorbance / denominator;
        }

        /// <summary>The same concentration in the unit that keeps it readable (M, mM, uM, nM).</summary>
        public static string DescribeConcentration(double molesPerLitre)
        {
            if (!Finite(molesPerLitre))
                return "-";

            double magnitude = Math.Abs(molesPerLitre);
            if (magnitude >= 0.1)
                return Format(molesPerLitre) + " M";
            if (magnitude >= 1e-4)
                return Format(molesPerLitre * 1e3) + " mM";
            if (magnitude >= 1e-7)
                return Format(molesPerLitre * 1e6) + " uM";

            return Format(molesPerLitre * 1e9) + " nM";
        }

        private static string Format(double value)
        {
            return value.ToString("G4", CultureInfo.InvariantCulture);
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
