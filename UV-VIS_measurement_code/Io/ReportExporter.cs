using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UvVisMeasurement.Io
{
    internal sealed class ReportContent
    {
        public string Title = string.Empty;
        public string Subtitle = string.Empty;

        /// <summary>Label / value rows printed above the plot, in the order given.</summary>
        public List<KeyValuePair<string, string>> Fields = new List<KeyValuePair<string, string>>();

        public Bitmap Plot;
        public string Footer = string.Empty;

        /// <summary>Rows printed under the grid across the whole width, wrapped not clipped.</summary>
        public List<KeyValuePair<string, string>> WideFields = new List<KeyValuePair<string, string>>();

        public void Add(string label, string value)
        {
            Fields.Add(new KeyValuePair<string, string>(label, value ?? string.Empty));
        }

        public void AddWide(string label, string value)
        {
            WideFields.Add(new KeyValuePair<string, string>(label, value ?? string.Empty));
        }
    }

    /// <summary>
    /// Writes the plot as a PNG and the measurement report as a one-page A4 PDF.
    ///
    /// The PDF is produced here rather than with a library: a commercial product cannot
    /// casually take on a third-party licence, and the one thing needed is a single page
    /// holding one image. The page is rendered with GDI+ first - which is also what makes
    /// Korean text come out right, since no font has to be embedded - and that bitmap is
    /// then wrapped in the smallest valid PDF that shows it.
    /// </summary>
    internal static class ReportExporter
    {
        private const double PointsPerInch = 72.0;
        private const double A4WidthInches = 8.268;
        private const double A4HeightInches = 11.693;

        public static string SavePng(Bitmap image, string path)
        {
            if (image == null)
                throw new ArgumentNullException("image");

            path = EnsureExtension(path, ".png");
            EnsureDirectory(path);
            image.Save(path, ImageFormat.Png);
            return path;
        }

        /// <summary>Draws the report onto an A4 page at <paramref name="dpi"/> dots per inch.</summary>
        public static Bitmap RenderPage(ReportContent content, int dpi)
        {
            if (content == null)
                throw new ArgumentNullException("content");

            int width = (int)Math.Round(A4WidthInches * dpi);
            int height = (int)Math.Round(A4HeightInches * dpi);
            float scale = dpi / 96f;

            var page = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            page.SetResolution(dpi, dpi);

            using (Graphics g = Graphics.FromImage(page))
            using (var title = new Font("Segoe UI", 16f * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var subtitle = new Font("Segoe UI", 10f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var label = new Font("Segoe UI", 9f * scale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var value = new Font("Segoe UI", 9f * scale, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var dark = new SolidBrush(Color.FromArgb(0x1E, 0x24, 0x2B)))
            using (var muted = new SolidBrush(Color.FromArgb(0x5F, 0x6B, 0x7A)))
            using (var accent = new SolidBrush(Color.FromArgb(0x1F, 0x5F, 0xAD)))
            using (var rule = new Pen(Color.FromArgb(0xCF, 0xD6, 0xDF), Math.Max(1f, scale)))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                float margin = 48f * scale;
                float y = margin;

                g.DrawString(content.Title, title, accent, margin, y);
                y += title.GetHeight(g) + 4f * scale;

                if (!string.IsNullOrEmpty(content.Subtitle))
                {
                    g.DrawString(content.Subtitle, subtitle, muted, margin, y);
                    y += subtitle.GetHeight(g);
                }

                y += 8f * scale;
                g.DrawLine(rule, margin, y, width - margin, y);
                y += 10f * scale;

                // Two columns of label/value, so a long settings list still fits above the plot.
                float columnWidth = (width - 2f * margin) / 2f;
                float labelWidth = 120f * scale;
                float rowHeight = value.GetHeight(g) + 3f * scale;
                int rows = (content.Fields.Count + 1) / 2;

                for (int i = 0; i < content.Fields.Count; i++)
                {
                    int column = i / rows;
                    int row = i % rows;
                    float x = margin + column * columnWidth;
                    float rowY = y + row * rowHeight;

                    g.DrawString(content.Fields[i].Key, label, muted, x, rowY);
                    var box = new RectangleF(x + labelWidth, rowY, columnWidth - labelWidth - 8f * scale, rowHeight);
                    g.DrawString(content.Fields[i].Value, value, dark, box,
                        new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                }

                y += rows * rowHeight + 6f * scale;

                foreach (KeyValuePair<string, string> field in content.WideFields)
                {
                    g.DrawString(field.Key, label, muted, margin, y);

                    var box = new RectangleF(margin + labelWidth, y,
                        width - 2f * margin - labelWidth, rowHeight * 3f);
                    SizeF needed = g.MeasureString(field.Value, value, new SizeF(box.Width, box.Height));
                    g.DrawString(field.Value, value, dark, box);

                    y += Math.Max(rowHeight, needed.Height + 2f * scale);
                }

                y += 8f * scale;
                g.DrawLine(rule, margin, y, width - margin, y);
                y += 14f * scale;

                if (content.Plot != null)
                {
                    float available = width - 2f * margin;
                    float bottom = height - margin - subtitle.GetHeight(g) - 10f * scale;
                    float plotScale = Math.Min(available / content.Plot.Width, (bottom - y) / content.Plot.Height);
                    plotScale = Math.Min(plotScale, 4f);

                    float plotWidth = content.Plot.Width * plotScale;
                    float plotHeight = content.Plot.Height * plotScale;
                    var target = new RectangleF(margin + (available - plotWidth) / 2f, y, plotWidth, plotHeight);

                    g.DrawImage(content.Plot, target);
                    g.DrawRectangle(rule, target.X, target.Y, target.Width, target.Height);
                }

                if (!string.IsNullOrEmpty(content.Footer))
                {
                    g.DrawString(content.Footer, subtitle, muted, margin,
                        height - margin - subtitle.GetHeight(g));
                }
            }

            return page;
        }

        public static string SavePdf(Bitmap page, string path)
        {
            if (page == null)
                throw new ArgumentNullException("page");

            path = EnsureExtension(path, ".pdf");
            EnsureDirectory(path);

            byte[] image = Deflate(ToRgb(page));
            double pageWidth = A4WidthInches * PointsPerInch;
            double pageHeight = A4HeightInches * PointsPerInch;

            using (FileStream file = File.Create(path))
                WritePdf(file, page.Width, page.Height, image, pageWidth, pageHeight);

            return path;
        }

        private static byte[] ToRgb(Bitmap bitmap)
        {
            var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                var rgb = new byte[bitmap.Width * bitmap.Height * 3];
                var row = new byte[Math.Abs(data.Stride)];

                for (int y = 0; y < bitmap.Height; y++)
                {
                    IntPtr line = data.Scan0 + y * data.Stride;
                    System.Runtime.InteropServices.Marshal.Copy(line, row, 0, row.Length);

                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int source = x * 3;
                        int target = (y * bitmap.Width + x) * 3;

                        // GDI+ stores 24bpp as BGR; PDF /DeviceRGB wants RGB.
                        rgb[target] = row[source + 2];
                        rgb[target + 1] = row[source + 1];
                        rgb[target + 2] = row[source];
                    }
                }

                return rgb;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        /// <summary>
        /// zlib stream for the PDF /FlateDecode filter: the 2-byte zlib header, the raw
        /// deflate data .NET produces, then the Adler-32 checksum (RFC 1950).
        /// </summary>
        private static byte[] Deflate(byte[] raw)
        {
            using (var output = new MemoryStream())
            {
                output.WriteByte(0x78);
                output.WriteByte(0x9C);

                using (var deflate = new DeflateStream(output, CompressionMode.Compress, true))
                    deflate.Write(raw, 0, raw.Length);

                uint a = 1, b = 0;
                for (int i = 0; i < raw.Length; i++)
                {
                    a = (a + raw[i]) % 65521;
                    b = (b + a) % 65521;
                }

                uint adler = (b << 16) | a;
                output.WriteByte((byte)(adler >> 24));
                output.WriteByte((byte)(adler >> 16));
                output.WriteByte((byte)(adler >> 8));
                output.WriteByte((byte)adler);

                return output.ToArray();
            }
        }

        private static void WritePdf(Stream stream, int imageWidth, int imageHeight, byte[] image,
                                     double pageWidth, double pageHeight)
        {
            var offsets = new List<long>();
            var ascii = new UTF8Encoding(false);

            Action<string> write = text =>
            {
                byte[] bytes = ascii.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            };

            Action<int, string> beginObject = (number, header) =>
            {
                offsets.Add(stream.Position);
                write(number + " 0 obj\n" + header + "\n");
            };

            write("%PDF-1.4\n");

            beginObject(1, "<< /Type /Catalog /Pages 2 0 R >>\nendobj");
            beginObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj");
            beginObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " +
                           Number(pageWidth) + " " + Number(pageHeight) + "]" +
                           " /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj");

            offsets.Add(stream.Position);
            write("4 0 obj\n<< /Type /XObject /Subtype /Image /Width " + imageWidth +
                  " /Height " + imageHeight + " /ColorSpace /DeviceRGB /BitsPerComponent 8" +
                  " /Filter /FlateDecode /Length " + image.Length + " >>\nstream\n");
            stream.Write(image, 0, image.Length);
            write("\nendstream\nendobj\n");

            string content = "q " + Number(pageWidth) + " 0 0 " + Number(pageHeight) + " 0 0 cm /Im0 Do Q\n";
            offsets.Add(stream.Position);
            write("5 0 obj\n<< /Length " + ascii.GetByteCount(content) + " >>\nstream\n" + content + "endstream\nendobj\n");

            long xref = stream.Position;
            var table = new StringBuilder();
            table.Append("xref\n0 ").Append(offsets.Count + 1).Append('\n');
            table.Append("0000000000 65535 f \n");
            foreach (long offset in offsets)
                table.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");

            table.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
                 .Append(xref).Append("\n%%EOF\n");

            write(table.ToString());
        }

        private static string Number(double value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string EnsureExtension(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("Choose a file name first.");

            return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
                ? path
                : path + extension;
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
