using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace UvVisMeasurement.Ui
{
    internal enum IconKind
    {
        Start,
        Stop,
        Dark,
        White,
        Clear,
        Save,
        SaveTimed,
        Record,
        Folder,
        Panel,
        Info,
        Manual
    }

    /// <summary>
    /// Toolbar and menu icons drawn as vectors at the size the display needs, so they stay
    /// sharp at any scaling and the program ships no third-party image assets.
    /// </summary>
    internal static class Icons
    {
        public static Bitmap Create(IconKind kind, int size, Color color)
        {
            var bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // Drawn on a 16-unit grid and scaled, so every glyph shares one geometry.
                float scale = size / 16f;
                g.ScaleTransform(scale, scale);

                using (var pen = new Pen(color, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                using (var brush = new SolidBrush(color))
                {
                    Draw(g, kind, pen, brush, color);
                }
            }
            return bitmap;
        }

        private static void Draw(Graphics g, IconKind kind, Pen pen, Brush brush, Color color)
        {
            switch (kind)
            {
                case IconKind.Start:
                    g.FillPolygon(brush, new[] { new PointF(4.5f, 2.5f), new PointF(13.5f, 8f), new PointF(4.5f, 13.5f) });
                    break;

                case IconKind.Stop:
                    g.FillRectangle(brush, 3.5f, 3.5f, 9f, 9f);
                    break;

                case IconKind.Dark:
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(2f, 2f, 12f, 12f);
                        g.FillPath(brush, path);
                    }
                    using (var cut = new SolidBrush(Color.White))
                        g.FillEllipse(cut, 6.5f, 0.5f, 10f, 10f);
                    break;

                case IconKind.White:
                    g.DrawEllipse(pen, 5f, 5f, 6f, 6f);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4.0;
                        float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
                        g.DrawLine(pen, 8f + 4.8f * c, 8f + 4.8f * s, 8f + 6.6f * c, 8f + 6.6f * s);
                    }
                    break;

                case IconKind.Clear:
                    g.DrawLine(pen, 4f, 4f, 12f, 12f);
                    g.DrawLine(pen, 12f, 4f, 4f, 12f);
                    break;

                case IconKind.Save:
                case IconKind.SaveTimed:
                    g.DrawRectangle(pen, 2.5f, 2.5f, 11f, 11f);
                    g.DrawRectangle(pen, 5f, 2.5f, 6f, 3.5f);
                    if (kind == IconKind.SaveTimed)
                    {
                        g.DrawLine(pen, 5f, 11f, 7.5f, 8.5f);
                        g.DrawLine(pen, 7.5f, 8.5f, 9f, 10f);
                        g.DrawLine(pen, 9f, 10f, 11.5f, 7.5f);
                    }
                    else
                    {
                        g.DrawLine(pen, 5f, 10f, 11f, 10f);
                    }
                    break;

                case IconKind.Record:
                    using (var red = new SolidBrush(Theme.Danger))
                        g.FillEllipse(red, 3f, 3f, 10f, 10f);
                    break;

                case IconKind.Folder:
                    g.DrawLines(pen, new[]
                    {
                        new PointF(2f, 4f), new PointF(6f, 4f), new PointF(7.5f, 5.5f), new PointF(14f, 5.5f),
                        new PointF(14f, 13f), new PointF(2f, 13f), new PointF(2f, 4f)
                    });
                    break;

                case IconKind.Panel:
                    g.DrawRectangle(pen, 2f, 3f, 12f, 10f);
                    g.FillRectangle(brush, 2f, 3f, 4.5f, 10f);
                    break;

                case IconKind.Info:
                    g.DrawEllipse(pen, 2f, 2f, 12f, 12f);
                    g.DrawLine(pen, 8f, 7.5f, 8f, 11.5f);
                    g.FillEllipse(brush, 7.1f, 4.2f, 1.8f, 1.8f);
                    break;

                case IconKind.Manual:
                    g.DrawRectangle(pen, 3.5f, 2f, 9f, 12f);
                    g.DrawLine(pen, 6f, 5.5f, 10f, 5.5f);
                    g.DrawLine(pen, 6f, 8f, 10f, 8f);
                    g.DrawLine(pen, 6f, 10.5f, 9f, 10.5f);
                    break;
            }
        }
    }
}
