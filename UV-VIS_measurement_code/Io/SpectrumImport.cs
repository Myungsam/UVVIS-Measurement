using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UvVisMeasurement.Io
{
    /// <summary>One spectrum read back from a CSV file, for drawing beside a live measurement.</summary>
    internal sealed class ImportedSpectrum
    {
        public string Name = string.Empty;
        public string Path = string.Empty;
        public string ColumnName = string.Empty;
        public double[] Wavelengths = new double[0];
        public double[] Values = new double[0];

        /// <summary>True when the column holds absorbance, so it can share the absorbance axis.</summary>
        public bool IsAbsorbance;

        public int Count
        {
            get { return Values.Length; }
        }
    }

    /// <summary>
    /// Reads spectra back out of the CSV files this program writes, and out of plain CSV from
    /// other software.
    ///
    /// The format is the one documented in docs/README.md section 8: '#' comment lines, then
    /// a header row, then one row per pixel with the wavelength first. Absorbance is preferred
    /// when the file has it, because that is what the operator compares; a file with only raw
    /// counts is read as counts and marked as such so it is never drawn on the absorbance axis.
    /// </summary>
    internal static class SpectrumImport
    {
        private static readonly char[] Delimiters = { ',', '\t', ';' };

        public static ImportedSpectrum Load(string path, out string error)
        {
            error = null;

            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                return Parse(lines, path, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        public static ImportedSpectrum Parse(IList<string> lines, string path, out string error)
        {
            error = null;

            if (lines == null || lines.Count == 0)
            {
                error = "The file is empty.";
                return null;
            }

            int row = 0;
            while (row < lines.Count && (lines[row].TrimStart().StartsWith("#", StringComparison.Ordinal) ||
                                         lines[row].Trim().Length == 0))
            {
                row++;
            }

            if (row >= lines.Count)
            {
                error = "The file has no data rows.";
                return null;
            }

            char delimiter = DetectDelimiter(lines[row]);
            string[] headers = Split(lines[row], delimiter);

            // A header row is text; a file that starts straight into numbers has none.
            bool hasHeader = headers.Length > 1 && !IsNumber(headers[0]);
            if (hasHeader)
                row++;

            int column = ChooseColumn(headers, hasHeader);
            if (column < 1)
            {
                error = "No value column was found next to the wavelength column.";
                return null;
            }

            var wavelengths = new List<double>();
            var values = new List<double>();

            for (; row < lines.Count; row++)
            {
                string line = lines[row];
                if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;

                string[] cells = Split(line, delimiter);
                if (cells.Length <= column)
                    continue;

                double x;
                if (!double.TryParse(cells[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
                    continue;

                double y;
                if (!double.TryParse(cells[column], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
                    y = double.NaN;

                wavelengths.Add(x);
                values.Add(y);
            }

            if (wavelengths.Count < 2)
            {
                error = "The file holds fewer than two readable points.";
                return null;
            }

            string columnName = hasHeader && column < headers.Length ? headers[column] : "value";

            return new ImportedSpectrum
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(path ?? string.Empty),
                Path = path ?? string.Empty,
                ColumnName = columnName,
                IsAbsorbance = LooksLikeAbsorbance(columnName),
                Wavelengths = wavelengths.ToArray(),
                Values = values.ToArray()
            };
        }

        /// <summary>Absorbance if the file has it, otherwise the first column after the wavelength.</summary>
        private static int ChooseColumn(string[] headers, bool hasHeader)
        {
            if (!hasHeader)
                return headers.Length > 1 ? 1 : -1;

            for (int i = 1; i < headers.Length; i++)
            {
                if (LooksLikeAbsorbance(headers[i]))
                    return i;
            }

            return headers.Length > 1 ? 1 : -1;
        }

        private static bool LooksLikeAbsorbance(string header)
        {
            if (string.IsNullOrEmpty(header))
                return false;

            string lower = header.ToLowerInvariant();
            return lower.Contains("absorbance") || lower.Contains("(od)") || lower == "a";
        }

        private static char DetectDelimiter(string line)
        {
            char best = ',';
            int most = 0;

            foreach (char candidate in Delimiters)
            {
                int count = 0;
                foreach (char c in line)
                    if (c == candidate)
                        count++;

                if (count > most)
                {
                    most = count;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Splits one CSV row, honouring the quoting CsvExporter writes.</summary>
        private static string[] Split(string line, char delimiter)
        {
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                }
                else if (c == delimiter)
                {
                    cells.Add(cell.ToString().Trim());
                    cell.Length = 0;
                }
                else
                {
                    cell.Append(c);
                }
            }

            cells.Add(cell.ToString().Trim());
            return cells.ToArray();
        }

        private static bool IsNumber(string text)
        {
            double parsed;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }
    }
}
