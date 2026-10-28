using System;
using UvVisMeasurement.Localization;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;
using UvVisMeasurement.Analysis;

namespace UvVisMeasurement.Ui
{
    /// <summary>
    /// Time-versus-wavelength absorbance map for the "Timed Absorption Series (2D plot)" tab.
    /// The image is rasterised into a bitmap via LockBits because a 5000 x 2136 matrix is far
    /// too large to push through GDI+ primitives on every repaint.
    ///
    /// All three scales - time, wavelength and the colour range - follow the data until the
    /// operator pins them from the right-click menu, after which the first and last number
    /// on that scale can be clicked and typed over. Pinning time or wavelength crops the
    /// raster to that window rather than stretching it, so the map zooms.
    /// </summary>
    internal sealed class HeatmapControl : Control
    {
        // 96-dpi pixels, scaled to the monitor so axis labels keep their room at 150 %.
        private int MarginLeft { get { return LogicalToDeviceUnits(74); } }
        private int MarginRight { get { return LogicalToDeviceUnits(96); } }
        private int MarginTop { get { return LogicalToDeviceUnits(18); } }
        private int MarginBottom { get { return LogicalToDeviceUnits(48); } }
        private int ColorBarWidth { get { return LogicalToDeviceUnits(22); } }

        /// <summary>Numbers turn this colour once their scale stops following the data.</summary>
        private static readonly Color ManualLabelColor = Color.FromArgb(120, 190, 255);

        private static readonly Color[] Viridis =
        {
            Color.FromArgb( 68,   1,  84), Color.FromArgb( 72,  40, 120),
            Color.FromArgb( 62,  74, 137), Color.FromArgb( 49, 104, 142),
            Color.FromArgb( 38, 130, 142), Color.FromArgb( 31, 158, 137),
            Color.FromArgb( 53, 183, 121), Color.FromArgb(109, 205,  89),
            Color.FromArgb(253, 231,  37)
        };

        /// <summary>A number painted on a scale, and which end of which scale it sets.</summary>
        private struct EditableLabel
        {
            public Rectangle Bounds;
            public PlotAxis Axis;
            public bool UpperEnd;
            public double Value;
        }

        private readonly AxisScale _time = new AxisScale();
        private readonly AxisScale _wavelength = new AxisScale();
        private readonly AxisScale _color = new AxisScale();

        private readonly List<EditableLabel> _labels = new List<EditableLabel>();

        private string _timeTitle = "Time (s)";
        private string _wavelengthTitle = "Wavelength (nm)";
        private string _emptyText = "No timed datasets.\r\nEnable \"Save timed datasets\" while a measurement is running.";
        private readonly ToolTip _hint = new ToolTip { ShowAlways = true };
        private ToolStripMenuItem _autoTimeItem;
        private ToolStripMenuItem _autoWavelengthItem;
        private ToolStripMenuItem _autoColorItem;

        private TimedSeriesSnapshot _data = TimedSeriesSnapshot.Empty;
        private Bitmap _cache;
        private Rectangle _cachedPlotArea;
        private bool _dirty = true;

        public void SetAxisTitles(string time, string wavelength)
        {
            _timeTitle = time;
            _wavelengthTitle = wavelength;
        }

        public void SetEmptyText(string text)
        {
            _emptyText = text;
            Invalidate();
        }

        public HeatmapControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(28, 28, 28);
            ForeColor = Color.Gainsboro;

            // Seeded so an empty map still draws numbered axes instead of a bare rectangle.
            _time.SetDataRange(0.0, 1.0);
            _wavelength.SetDataRange(0.0, 1.0);
            _color.SetDataRange(0.0, 1.0);

            BuildContextMenu();
        }

        /// <summary>Raised after the operator changes an auto-scale flag or a manual limit.</summary>
        public event EventHandler ScalesChanged;

        /// <summary>Autoscale the colour range to the data instead of using the manual limits.</summary>
        public bool AutoScale
        {
            get { return _color.Auto; }
            set { SetAutoScale(PlotAxis.Color, value); }
        }

        public double ScaleMinimum
        {
            get { return _color.Minimum; }
        }

        public double ScaleMaximum
        {
            get { return _color.Maximum; }
        }

        public void SetManualScale(double minimum, double maximum)
        {
            if (!_color.SetManualRange(minimum, maximum))
                _color.SetManualRange(minimum, minimum + 1e-6);

            RebuildAndRedraw();
        }

        public bool IsAutoScale(PlotAxis axis)
        {
            return ScaleFor(axis).Auto;
        }

        public void SetAutoScale(PlotAxis axis, bool auto)
        {
            AxisScale scale = ScaleFor(axis);
            if (scale.Auto == auto)
                return;

            if (auto)
                scale.GoAuto();
            else
                scale.GoManual(scale.Minimum, scale.Maximum);

            if (auto)
                ApplyDataRanges();

            RebuildAndRedraw();
            OnScalesChanged();
        }

        /// <summary>One line naming what each scale is doing, for the status bar.</summary>
        public string DescribeScales()
        {
            return L.T("axisName.time", "time") + " " + Describe(_time) +
                   ",  " + L.T("axisName.wavelength", "wavelength") + " " + Describe(_wavelength) +
                   ",  " + L.T("axisName.colour", "colour") + " " + Describe(_color);
        }

        private static string Describe(AxisScale scale)
        {
            if (scale.Auto)
                return L.T("scale.auto", "auto");

            return string.Format(CultureInfo.CurrentCulture, "{0:0.###} .. {1:0.###}",
                scale.Minimum, scale.Maximum);
        }

        /// <summary>Takes an immutable snapshot of the series to rasterise.</summary>
        public void SetSource(TimedSeriesSnapshot data)
        {
            _data = data ?? TimedSeriesSnapshot.Empty;
            ApplyDataRanges();
            RebuildAndRedraw();
        }

        /// <summary>Feeds every scale the range the data covers; pinned ones ignore it.</summary>
        private void ApplyDataRanges()
        {
            TimedRecord[] records = _data.Records;

            if (records.Length > 0)
            {
                double first = records[0].ElapsedSeconds;
                double last = records[records.Length - 1].ElapsedSeconds;
                if (last - first < 1e-6)
                    last = first + 1e-6;

                _time.SetDataRange(first, last);
            }

            double[] wavelengths = _data.Wavelengths;
            if (wavelengths != null && wavelengths.Length >= 2)
                _wavelength.SetDataRange(wavelengths[0], wavelengths[wavelengths.Length - 1]);

            double min, max;
            if (_data.TryGetAbsorbanceRange(out min, out max))
            {
                if (max - min < 1e-9)
                    max = min + 1e-6;

                _color.SetDataRange(min, max);
            }
        }

        /// <summary>Discards the rasterised cache and schedules a repaint.</summary>
        public void RebuildAndRedraw()
        {
            _dirty = true;
            Invalidate();
        }

        private AxisScale ScaleFor(PlotAxis axis)
        {
            if (axis == PlotAxis.Horizontal) return _time;
            if (axis == PlotAxis.Vertical) return _wavelength;
            return _color;
        }

        private void OnScalesChanged()
        {
            EventHandler handler = ScalesChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            _dirty = true;
            base.OnResize(e);
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            _labels.Clear();

            Rectangle plot = new Rectangle(
                MarginLeft, MarginTop,
                Math.Max(1, Width - MarginLeft - MarginRight),
                Math.Max(1, Height - MarginTop - MarginBottom));

            if (plot.Width < 8 || plot.Height < 8)
                return;

            bool hasData = !_data.IsEmpty;

            if (hasData)
            {
                EnsureCache(plot);
                if (_cache != null)
                {
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(_cache, plot);
                }
            }
            else
            {
                using (var brush = new SolidBrush(Color.FromArgb(12, 12, 12)))
                    g.FillRectangle(brush, plot);

                TextRenderer.DrawText(g,
                    _emptyText,
                    Font, plot, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using (var pen = new Pen(Color.DimGray))
                g.DrawRectangle(pen, plot);

            DrawAxes(g, plot);
            DrawColorBar(g, plot);
        }

        private void EnsureCache(Rectangle plot)
        {
            if (!_dirty && _cache != null && _cachedPlotArea.Size == plot.Size)
                return;

            if (_cache != null && _cache.Size != plot.Size)
            {
                _cache.Dispose();
                _cache = null;
            }

            if (_cache == null)
                _cache = new Bitmap(plot.Width, plot.Height, PixelFormat.Format32bppPArgb);

            _cachedPlotArea = plot;
            _dirty = false;

            TimedRecord[] records = _data.Records;
            double span = _color.Maximum - _color.Minimum;
            if (span <= 0.0)
                span = 1e-6;

            // Which record and which detector pixel each output pixel samples. Doing this
            // once per axis rather than per output pixel keeps a full-window rebuild cheap,
            // and -1 marks a pixel that falls outside the data the operator zoomed past.
            int[] columnOf = MapColumns(_cache.Width, records);
            int[] rowOf = MapRows(_cache.Height, _data.Wavelengths, _data.PixelCount);

            BitmapData data = _cache.LockBits(new Rectangle(0, 0, _cache.Width, _cache.Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);

            try
            {
                unsafe
                {
                    for (int py = 0; py < _cache.Height; py++)
                    {
                        int rowIndex = rowOf[py];
                        byte* line = (byte*)data.Scan0 + (long)py * data.Stride;

                        for (int px = 0; px < _cache.Width; px++)
                        {
                            int columnIndex = columnOf[px];
                            Color color;

                            if (rowIndex < 0 || columnIndex < 0)
                            {
                                color = Color.FromArgb(255, 20, 20, 20);
                            }
                            else
                            {
                                float[] values = records[columnIndex].Absorbance;

                                if (values == null || rowIndex >= values.Length || float.IsNaN(values[rowIndex]))
                                    color = Color.FromArgb(255, 20, 20, 20);
                                else
                                    color = MapColor((values[rowIndex] - _color.Minimum) / span);
                            }

                            int offset = px * 4;
                            line[offset + 0] = color.B;
                            line[offset + 1] = color.G;
                            line[offset + 2] = color.R;
                            line[offset + 3] = 255;
                        }
                    }
                }
            }
            finally
            {
                _cache.UnlockBits(data);
            }
        }

        /// <summary>Output column -> record index, or -1 when that time carries no data.</summary>
        private int[] MapColumns(int width, TimedRecord[] records)
        {
            var map = new int[width];
            double from = _time.Minimum;
            double to = _time.Maximum;

            for (int px = 0; px < width; px++)
            {
                if (records.Length == 0)
                {
                    map[px] = -1;
                    continue;
                }

                double t = width == 1 ? from : from + (to - from) * px / (width - 1);

                if (t < records[0].ElapsedSeconds - 1e-9 ||
                    t > records[records.Length - 1].ElapsedSeconds + 1e-9)
                {
                    map[px] = -1;
                    continue;
                }

                map[px] = NearestRecord(records, t);
            }

            return map;
        }

        /// <summary>Output row -> detector pixel, or -1 when that wavelength carries no data.</summary>
        private int[] MapRows(int height, double[] wavelengths, int pixelCount)
        {
            var map = new int[height];
            double from = _wavelength.Minimum;
            double to = _wavelength.Maximum;

            for (int py = 0; py < height; py++)
            {
                if (wavelengths == null || pixelCount <= 0)
                {
                    map[py] = -1;
                    continue;
                }

                // Wavelength increases upwards, matching the axis labels.
                int flipped = height - 1 - py;
                double w = height == 1 ? from : from + (to - from) * flipped / (height - 1);

                if (w < wavelengths[0] - 1e-9 || w > wavelengths[pixelCount - 1] + 1e-9)
                {
                    map[py] = -1;
                    continue;
                }

                int index = NearestAscending(wavelengths, pixelCount, w);
                map[py] = index >= pixelCount ? pixelCount - 1 : index;
            }

            return map;
        }

        /// <summary>Records are appended in acquisition order, so their times ascend.</summary>
        private static int NearestRecord(TimedRecord[] records, double seconds)
        {
            int low = 0;
            int high = records.Length - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;
                if (records[middle].ElapsedSeconds < seconds)
                    low = middle + 1;
                else
                    high = middle;
            }

            if (low > 0 &&
                Math.Abs(records[low - 1].ElapsedSeconds - seconds) <=
                Math.Abs(records[low].ElapsedSeconds - seconds))
            {
                return low - 1;
            }

            return low;
        }

        private static int NearestAscending(double[] values, int count, double target)
        {
            int low = 0;
            int high = count - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;
                if (values[middle] < target)
                    low = middle + 1;
                else
                    high = middle;
            }

            if (low > 0 && Math.Abs(values[low - 1] - target) <= Math.Abs(values[low] - target))
                return low - 1;

            return low;
        }

        private static Color MapColor(double normalized)
        {
            if (double.IsNaN(normalized))
                return Color.FromArgb(20, 20, 20);
            if (normalized <= 0.0)
                return Viridis[0];
            if (normalized >= 1.0)
                return Viridis[Viridis.Length - 1];

            double scaled = normalized * (Viridis.Length - 1);
            int index = (int)scaled;
            double fraction = scaled - index;

            Color low = Viridis[index];
            Color high = Viridis[Math.Min(index + 1, Viridis.Length - 1)];

            return Color.FromArgb(
                (int)(low.R + (high.R - low.R) * fraction),
                (int)(low.G + (high.G - low.G) * fraction),
                (int)(low.B + (high.B - low.B) * fraction));
        }

        private void DrawAxes(Graphics g, Rectangle plot)
        {
            using (var pen = new Pen(Color.DimGray))
            using (var brush = new SolidBrush(ForeColor))
            using (var manualBrush = new SolidBrush(ManualLabelColor))
            using (var format = new StringFormat())
            {
                double timeMin = _time.Minimum;
                double timeMax = _time.Maximum;
                double wavelengthMin = _wavelength.Minimum;
                double wavelengthMax = _wavelength.Maximum;

                format.Alignment = StringAlignment.Center;
                Brush timeBrush = _time.Auto ? brush : manualBrush;
                Brush wavelengthBrush = _wavelength.Auto ? brush : manualBrush;

                const int XTicks = 6;
                for (int i = 0; i <= XTicks; i++)
                {
                    int x = plot.Left + plot.Width * i / XTicks;
                    g.DrawLine(pen, x, plot.Bottom, x, plot.Bottom + 4);

                    double value = timeMin + (timeMax - timeMin) * i / XTicks;
                    string text = value.ToString(Math.Abs(value) >= 100 ? "F0" : "F1", CultureInfo.InvariantCulture);
                    g.DrawString(text, Font, timeBrush, new PointF(x, plot.Bottom + 6), format);

                    if (i == 0 || i == XTicks)
                    {
                        Register(new Rectangle(x - LogicalToDeviceUnits(32), plot.Bottom + 4, LogicalToDeviceUnits(64), Font.Height + 4),
                            PlotAxis.Horizontal, i == XTicks, value);
                    }
                }

                const int YTicks = 8;
                using (var rightAligned = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    for (int i = 0; i <= YTicks; i++)
                    {
                        int y = plot.Bottom - plot.Height * i / YTicks;
                        g.DrawLine(pen, plot.Left - 4, y, plot.Left, y);

                        double value = wavelengthMin + (wavelengthMax - wavelengthMin) * i / YTicks;
                        g.DrawString(value.ToString("F0", CultureInfo.InvariantCulture), Font, wavelengthBrush,
                            new RectangleF(0, y - Font.Height / 2f, plot.Left - 6, Font.Height + 1), rightAligned);

                        if (i == 0 || i == YTicks)
                        {
                            Register(new Rectangle(0, y - Font.Height / 2 - 1, plot.Left - 4, Font.Height + 3),
                                PlotAxis.Vertical, i == YTicks, value);
                        }
                    }
                }

                // Offsets follow the font so the titles keep clear of the tick labels at 150 %.
                g.DrawString(_timeTitle, Font, brush,
                    new PointF(plot.Left + plot.Width / 2f, plot.Bottom + 8 + Font.Height), format);

                var state = g.Save();
                g.TranslateTransform(Font.Height + 2, plot.Top + plot.Height / 2f);
                g.RotateTransform(-90);
                g.DrawString(_wavelengthTitle, Font, brush, new PointF(0, -Font.Height), format);
                g.Restore(state);
            }
        }

        private void DrawColorBar(Graphics g, Rectangle plot)
        {
            var bar = new Rectangle(plot.Right + LogicalToDeviceUnits(16), plot.Top, ColorBarWidth, plot.Height);
            if (bar.Height <= 0)
                return;

            for (int y = 0; y < bar.Height; y++)
            {
                double normalized = 1.0 - (double)y / Math.Max(1, bar.Height - 1);
                using (var brush = new SolidBrush(MapColor(normalized)))
                    g.FillRectangle(brush, bar.Left, bar.Top + y, bar.Width, 1);
            }

            using (var pen = new Pen(Color.DimGray))
                g.DrawRectangle(pen, bar);

            using (var brush = new SolidBrush(_color.Auto ? ForeColor : ManualLabelColor))
            using (var titleBrush = new SolidBrush(ForeColor))
            {
                const int Ticks = 5;
                for (int i = 0; i <= Ticks; i++)
                {
                    int y = bar.Bottom - bar.Height * i / Ticks;
                    double value = _color.Minimum + (_color.Maximum - _color.Minimum) * i / Ticks;
                    g.DrawString(value.ToString("F3", CultureInfo.InvariantCulture), Font, brush,
                        new PointF(bar.Right + 3, y - Font.Height / 2f));

                    if (i == 0 || i == Ticks)
                    {
                        Register(new Rectangle(bar.Right + 1, y - Font.Height / 2 - 2, LogicalToDeviceUnits(56), Font.Height + 4),
                            PlotAxis.Color, i == Ticks, value);
                    }
                }

                g.DrawString("OD", Font, titleBrush, new PointF(bar.Left, bar.Top - Font.Height - 1));
            }
        }

        private void Register(Rectangle bounds, PlotAxis axis, bool upperEnd, double value)
        {
            _labels.Add(new EditableLabel
            {
                Bounds = bounds,
                Axis = axis,
                UpperEnd = upperEnd,
                Value = value
            });
        }

        // ------------------------------------------------------------- context menu

        private ToolStripMenuItem _tipItem;

        private void BuildContextMenu()
        {
            _autoTimeItem = new ToolStripMenuItem(string.Empty, null, OnAutoTimeClick)
            {
                CheckOnClick = true,
                Checked = true
            };

            _autoWavelengthItem = new ToolStripMenuItem(string.Empty, null, OnAutoWavelengthClick)
            {
                CheckOnClick = true,
                Checked = true
            };

            _autoColorItem = new ToolStripMenuItem(string.Empty, null, OnAutoColorClick)
            {
                CheckOnClick = true,
                Checked = true
            };

            _tipItem = new ToolStripMenuItem { Enabled = false };

            var menu = new ContextMenuStrip();
            menu.Items.Add(_autoTimeItem);
            menu.Items.Add(_autoWavelengthItem);
            menu.Items.Add(_autoColorItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_tipItem);
            menu.Opening += OnMenuOpening;

            ContextMenuStrip = menu;
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // The colour flag also moves through the panel checkbox above the map, so the
            // ticks are refreshed on every open rather than only when clicked here.
            _autoTimeItem.Checked = _time.Auto;
            _autoWavelengthItem.Checked = _wavelength.Auto;
            _autoColorItem.Checked = _color.Auto;

            _autoTimeItem.Text = L.T("map.autoTime", "Auto scale &time (X)");
            _autoWavelengthItem.Text = L.T("map.autoWavelength", "Auto scale &wavelength (Y)");
            _autoColorItem.Text = L.T("map.autoColour", "Auto scale &colour (OD)");
            _tipItem.Text = L.T("map.tipEdit", "With auto scale off, click the first or last number on a scale");
        }

        private void OnAutoTimeClick(object sender, EventArgs e)
        {
            SetAutoScale(PlotAxis.Horizontal, _autoTimeItem.Checked);
        }

        private void OnAutoWavelengthClick(object sender, EventArgs e)
        {
            SetAutoScale(PlotAxis.Vertical, _autoWavelengthItem.Checked);
        }

        private void OnAutoColorClick(object sender, EventArgs e)
        {
            SetAutoScale(PlotAxis.Color, _autoColorItem.Checked);
        }

        // -------------------------------------------------------- editing a limit

        /// <summary>On the release, so a hint tool tip is not dismissed by its own click.</summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Left)
                TryEditLabel(e.Location);
        }

        private bool EditorOpen
        {
            get
            {
                foreach (Control control in Controls)
                {
                    if (control is TextBox)
                        return true;
                }
                return false;
            }
        }

        private void TryEditLabel(Point location)
        {
            if (EditorOpen)
                return;

            for (int i = 0; i < _labels.Count; i++)
            {
                EditableLabel label = _labels[i];
                if (!label.Bounds.Contains(location))
                    continue;

                AxisScale scale = ScaleFor(label.Axis);
                if (scale.Auto)
                {
                    _hint.Show(L.F("hint.autoScaleOnMap",
                        "Auto scale {0} is on.\nRight-click the map to turn it off first.",
                        NameOf(label.Axis)).Replace("\n", Environment.NewLine),
                        this, location.X + 14, location.Y + 14, 2500);
                    return;
                }

                var bounds = new Rectangle(
                    label.Bounds.X + label.Bounds.Width / 2 - 38,
                    label.Bounds.Y + label.Bounds.Height / 2 - (Font.Height + 6) / 2,
                    76, Font.Height + 6);

                PlotAxis axis = label.Axis;
                bool upperEnd = label.UpperEnd;

                InlineNumberBox.Open(this, bounds, label.Value,
                    delegate(double typed) { CommitEnd(axis, upperEnd, typed, location); });
                return;
            }
        }

        private void CommitEnd(PlotAxis axis, bool upperEnd, double value, Point where)
        {
            if (!ScaleFor(axis).SetEnd(upperEnd, value))
            {
                _hint.Show(upperEnd
                        ? L.T("hint.upperTooLow", "The last number has to stay above the first one.")
                        : L.T("hint.lowerTooHigh", "The first number has to stay below the last one."),
                    this, where.X + 14, where.Y + 14, 2500);
                return;
            }

            RebuildAndRedraw();
            OnScalesChanged();
        }

        private static string NameOf(PlotAxis axis)
        {
            if (axis == PlotAxis.Horizontal) return L.T("axisName.time", "time");
            if (axis == PlotAxis.Vertical) return L.T("axisName.wavelength", "wavelength");
            return L.T("axisName.colour", "colour");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_cache != null)
                {
                    _cache.Dispose();
                    _cache = null;
                }

                _hint.Dispose();

                if (ContextMenuStrip != null)
                {
                    ContextMenuStrip.Opening -= OnMenuOpening;
                    ContextMenuStrip.Dispose();
                    ContextMenuStrip = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
