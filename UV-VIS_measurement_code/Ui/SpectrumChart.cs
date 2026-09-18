using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using UvVisMeasurement.Io;

namespace UvVisMeasurement.Ui
{
    /// <summary>Something the plot wants to say in the status bar.</summary>
    internal sealed class ChartMessageEventArgs : EventArgs
    {
        public ChartMessageEventArgs(string message) { Message = message; }
        public string Message { get; private set; }
    }

    /// <summary>
    /// Chart that keeps the last complete frame on screen while its data is being replaced.
    ///
    /// System.Windows.Forms.DataVisualization walks Series.Points from inside its own
    /// OnPaint (ChartArea.SetDefaultAxesValues -> ChartHelper.SeriesXValuesZeros). A
    /// WM_PAINT that lands in the middle of a point update therefore throws
    /// "Collection was modified" straight out of the paint handler, which in a plain
    /// Chart takes the whole process down. Re-entrant paints are not hypothetical here:
    /// the message queue gets pumped whenever a modal dialog opens or another process
    /// sends this window a message, and new frames arrive continuously during a run.
    ///
    /// Skipping those paints keeps the process alive but cannot simply return: Chart
    /// paints with ControlStyles.OptimizedDoubleBuffer, and the buffer it renders into is
    /// zero-filled, so a paint that draws nothing blits a solid black rectangle. At frame
    /// rate that reads as the chart flashing black - measured at 10-17% of frames once a
    /// dark and a white reference are stored and three series are being rewritten.
    ///
    /// So every complete render goes to a persistent off-screen frame first and is then
    /// blitted to the screen. A paint that has to be skipped re-blits that same frame,
    /// which leaves the previous spectrum on screen instead of a black flash.
    ///
    /// It also owns the axis scaling the operator drives. Auto scaling is continuous: the
    /// range is recomputed from the points that are actually on the plot every time the
    /// data changes and the instant auto scaling is switched back on, so it never waits
    /// for anything to be restarted. Zooming or panning pins the axis instead, and with an
    /// axis pinned the first and last number on it can be clicked and typed over.
    /// </summary>
    internal sealed class SpectrumChart : Chart
    {
        /// <summary>Axis numbers turn this colour once the axis stops following the data.</summary>
        private static readonly Color ManualLabelColor = Color.FromArgb(0, 70, 170);

        /// <summary>Pixels the pointer has to travel before a click counts as a drag.</summary>
        private const int DragThreshold = 4;

        private enum DragKind { None, Zoom, Pan }

        private int _updateDepth;
        private bool _repaintQueued;
        private Bitmap _frame;

        private readonly AxisScale _horizontal = new AxisScale();
        private readonly AxisScale _vertical = new AxisScale();
        private readonly ToolTip _hint = new ToolTip { ShowAlways = true };
        private ToolStripMenuItem _autoHorizontalItem;
        private ToolStripMenuItem _autoVerticalItem;

        private DragKind _drag;
        private Point _dragStart;
        private Point _dragLast;
        private Rectangle _band;
        private bool _bandVisible;

        public SpectrumChart()
        {
            BuildContextMenu();
        }

        /// <summary>Raised after the operator changes an auto-scale flag or a manual limit.</summary>
        public event EventHandler ScalesChanged;

        /// <summary>Raised for things worth putting in the status bar, such as a copy.</summary>
        public event EventHandler<ChartMessageEventArgs> MessageRaised;

        /// <summary>Marks the start of a point/series mutation. Always pair with <see cref="EndDataUpdate"/>.</summary>
        public void BeginDataUpdate()
        {
            _updateDepth++;
        }

        public void EndDataUpdate()
        {
            if (_updateDepth == 0)
                return;

            if (--_updateDepth != 0)
                return;

            // Every finished data change re-scales the axes that are following the data,
            // which is what makes auto scaling track a live acquisition.
            AutoScaleFromData();
            Invalidate();
        }

        // ------------------------------------------------------------------ scaling

        public bool IsAutoScale(PlotAxis axis)
        {
            return ScaleFor(axis).Auto;
        }

        /// <summary>
        /// Offers a range to an axis. It reaches the screen only while that axis is auto
        /// scaling, so it can never fight a range the operator typed.
        ///
        /// Used for the range an empty plot starts with, before any spectrum exists to
        /// scale to; once there are points, <see cref="AutoScaleFromData"/> takes over.
        /// </summary>
        public void SetDataRange(PlotAxis axis, double minimum, double maximum)
        {
            ScaleFor(axis).SetDataRange(minimum, maximum);
            ApplyScales();
        }

        public void SetAutoScale(PlotAxis axis, bool auto)
        {
            AxisScale scale = ScaleFor(axis);
            if (scale.Auto == auto)
                return;

            if (auto)
            {
                scale.GoAuto();

                // Switching it on has to bite straight away rather than at the next frame,
                // so the range is taken from the spectrum that is on the plot right now.
                AutoScaleFromData();
            }
            else
            {
                Pin(axis);
            }

            ApplyScales();
            Invalidate();
            OnScalesChanged();
        }

        /// <summary>
        /// Recomputes the range every auto-scaled axis should show from the points that are
        /// currently plotted, and applies it.
        ///
        /// The suggestion is recorded for both axes even when one of them is pinned, so
        /// releasing an axis later snaps it to the live data instead of to whatever the
        /// data happened to look like when it was pinned.
        ///
        /// An empty plot leaves the previous range alone: a chart whose series hold no
        /// points draws no axes at all, so collapsing the range would only replace the
        /// start-up range with nothing.
        /// </summary>
        public void AutoScaleFromData()
        {
            if (ChartAreas.Count == 0)
                return;

            double xLow = double.MaxValue, xHigh = double.MinValue;
            double yLow = double.MaxValue, yHigh = double.MinValue;
            bool anyX = false, anyY = false;

            foreach (Series series in Series)
            {
                if (!series.Enabled)
                    continue;

                DataPointCollection points = series.Points;
                for (int i = 0; i < points.Count; i++)
                {
                    DataPoint point = points[i];

                    double x = point.XValue;
                    if (IsFinite(x))
                    {
                        if (x < xLow) xLow = x;
                        if (x > xHigh) xHigh = x;
                        anyX = true;
                    }

                    // An empty point marks a wavelength where the value is not defined -
                    // its stored Y is a placeholder zero and must not stretch the axis.
                    if (point.IsEmpty || point.YValues.Length == 0)
                        continue;

                    double y = point.YValues[0];
                    if (!IsFinite(y))
                        continue;

                    if (y < yLow) yLow = y;
                    if (y > yHigh) yHigh = y;
                    anyY = true;
                }
            }

            if (anyX)
            {
                double low, high;
                NiceRange(xLow, xHigh, 0.0, out low, out high);
                _horizontal.SetDataRange(low, high);
            }

            if (anyY)
            {
                double low, high;
                NiceRange(yLow, yHigh, 0.05, out low, out high);
                _vertical.SetDataRange(low, high);
            }

            ApplyScales();
        }

        /// <summary>
        /// Widens a data range to round numbers so the ticks land on values an operator
        /// would have chosen: pad by a fraction of the span, then snap outwards to a 1, 2
        /// or 5 step. A range with no width at all is opened up around its own value,
        /// because an axis whose ends are equal cannot be drawn.
        /// </summary>
        private static void NiceRange(double low, double high, double padFraction,
                                      out double lower, out double upper)
        {
            double span = high - low;

            if (!(span > 0.0))
            {
                double width = Math.Abs(high) > 0.0 ? Math.Abs(high) * 0.1 : 1.0;
                low -= width / 2.0;
                high += width / 2.0;
                span = high - low;
            }
            else if (padFraction > 0.0)
            {
                double pad = span * padFraction;
                low -= pad;
                high += pad;
                span = high - low;
            }

            double step = NiceStep(span);
            lower = Math.Floor(low / step) * step;
            upper = Math.Ceiling(high / step) * step;

            if (!(upper > lower))
                upper = lower + step;
        }

        /// <summary>A 1, 2 or 5 times a power of ten step giving roughly ten intervals.</summary>
        private static double NiceStep(double span)
        {
            double raw = span / 10.0;
            if (!IsFinite(raw) || raw <= 0.0)
                return 1.0;

            double magnitude = Math.Pow(10.0, Math.Floor(Math.Log10(raw)));
            double normalized = raw / magnitude;

            double nice;
            if (normalized <= 1.0) nice = 1.0;
            else if (normalized <= 2.0) nice = 2.0;
            else if (normalized <= 5.0) nice = 5.0;
            else nice = 10.0;

            return nice * magnitude;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Stops an axis following the data, keeping exactly what is on screen at that
        /// instant so the plot does not jump at the moment of the switch.
        /// </summary>
        private void Pin(PlotAxis axis)
        {
            AxisScale scale = ScaleFor(axis);
            if (!scale.Auto)
                return;

            double low = scale.Minimum;
            double high = scale.Maximum;

            if (!AxisScale.IsUsableRange(low, high))
            {
                // Nothing has been plotted yet, so fall back to whatever the library drew.
                Axis drawn = AxisFor(axis);
                low = drawn == null ? double.NaN : drawn.Minimum;
                high = drawn == null ? double.NaN : drawn.Maximum;
            }

            scale.GoManual(low, high);
        }

        /// <summary>One line naming what each axis is doing, for the status bar.</summary>
        public string DescribeScales()
        {
            return "X " + Describe(_horizontal) + ",  Y " + Describe(_vertical);
        }

        private static string Describe(AxisScale scale)
        {
            if (scale.Auto)
                return "auto";

            return string.Format(CultureInfo.CurrentCulture, "{0:0.###} .. {1:0.###}",
                scale.Minimum, scale.Maximum);
        }

        private AxisScale ScaleFor(PlotAxis axis)
        {
            return axis == PlotAxis.Vertical ? _vertical : _horizontal;
        }

        private Axis AxisFor(PlotAxis axis)
        {
            if (ChartAreas.Count == 0)
                return null;

            ChartArea area = ChartAreas[0];
            return axis == PlotAxis.Vertical ? area.AxisY : area.AxisX;
        }

        /// <summary>
        /// Pushes both ranges onto the chart. Every range this class shows is an explicit
        /// number - the library is never left to work one out - so the axis on screen and
        /// the state in <see cref="AxisScale"/> can only ever say the same thing.
        /// </summary>
        private void ApplyScales()
        {
            if (ChartAreas.Count == 0)
                return;

            ChartArea area = ChartAreas[0];

            area.AxisX.Minimum = _horizontal.Minimum;
            area.AxisX.Maximum = _horizontal.Maximum;
            area.AxisY.Minimum = _vertical.Minimum;
            area.AxisY.Maximum = _vertical.Maximum;

            // Left to itself the library divides whatever range it is given into five, which
            // on a 100..900 nm axis puts ticks at 260 and 420. Asking for the same 1/2/5
            // step the range was rounded to keeps the numbers ones an operator would read.
            area.AxisX.Interval = TickInterval(_horizontal);
            area.AxisY.Interval = TickInterval(_vertical);

            area.AxisX.LabelStyle.ForeColor = _horizontal.Auto ? SystemColors.ControlText : ManualLabelColor;
            area.AxisY.LabelStyle.ForeColor = _vertical.Auto ? SystemColors.ControlText : ManualLabelColor;
        }

        /// <summary>Tick spacing for a range, or 0 to leave the choice to the library.</summary>
        private static double TickInterval(AxisScale scale)
        {
            if (!AxisScale.IsUsableRange(scale.Minimum, scale.Maximum))
                return 0.0;

            return NiceStep(scale.Maximum - scale.Minimum);
        }

        private void OnScalesChanged()
        {
            EventHandler handler = ScalesChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------- context menu

        private void BuildContextMenu()
        {
            _autoHorizontalItem = new ToolStripMenuItem("Auto scale &X", null, OnAutoHorizontalClick)
            {
                CheckOnClick = true,
                Checked = true
            };

            _autoVerticalItem = new ToolStripMenuItem("Auto scale &Y", null, OnAutoVerticalClick)
            {
                CheckOnClick = true,
                Checked = true
            };

            var copy = new ToolStripMenuItem("&Copy Spectrum Data", null, OnCopyDataClick);

            var tip = new ToolStripMenuItem(
                "Drag to zoom, Shift-drag to pan - either one turns auto scale off." +
                Environment.NewLine +
                "With auto scale off, click the first or last number on an axis to type a limit.")
            {
                Enabled = false
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add(_autoHorizontalItem);
            menu.Items.Add(_autoVerticalItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(copy);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(tip);
            menu.Opening += OnMenuOpening;

            ContextMenuStrip = menu;
        }

        // ------------------------------------------------------------------ clipboard

        /// <summary>
        /// Puts every trace on the clipboard as CSV: the X axis in the first column, one
        /// column per series, one row per X position.
        ///
        /// What is copied is the data the series hold, not the part of it that happens to
        /// be inside the current zoom - a zoom only changes which points are drawn.
        /// </summary>
        private void OnCopyDataClick(object sender, EventArgs e)
        {
            string csv;
            int rows, traces;

            try
            {
                csv = BuildDataCsv(out rows, out traces);
            }
            catch (Exception ex)
            {
                RaiseMessage("Copy Spectrum Data failed: " + ex.Message);
                return;
            }

            if (csv == null)
            {
                RaiseMessage("Copy Spectrum Data: there is nothing plotted yet.");
                return;
            }

            try
            {
                Clipboard.SetText(csv);
            }
            catch (ExternalException ex)
            {
                // Another process can hold the clipboard open; that is not this program's
                // fault and not worth a dialog, but it must not look like it worked.
                RaiseMessage("Copy Spectrum Data: the clipboard was not available (" + ex.Message + ").");
                return;
            }

            RaiseMessage(string.Format(CultureInfo.CurrentCulture,
                "Copied {0} rows x {1} trace(s) to the clipboard as CSV.", rows, traces));
        }

        /// <summary>Builds the clipboard text, or null when no series holds any points.</summary>
        private string BuildDataCsv(out int rows, out int traces)
        {
            rows = 0;
            traces = 0;

            var plotted = new List<Series>();
            foreach (Series series in Series)
            {
                if (series.Points.Count > 0)
                    plotted.Add(series);
            }

            if (plotted.Count == 0)
                return null;

            // The series of one plot share an X grid, but that is not something this has to
            // rely on: every X that occurs becomes a row, and a series that has no point
            // there simply leaves that field empty.
            var rowOf = new Dictionary<double, int>();
            var positions = new List<double>();

            foreach (Series series in plotted)
            {
                DataPointCollection points = series.Points;
                for (int i = 0; i < points.Count; i++)
                {
                    double x = points[i].XValue;
                    if (!IsFinite(x) || rowOf.ContainsKey(x))
                        continue;

                    rowOf.Add(x, positions.Count);
                    positions.Add(x);
                }
            }

            if (positions.Count == 0)
                return null;

            positions.Sort();
            for (int i = 0; i < positions.Count; i++)
                rowOf[positions[i]] = i;

            var headers = new string[plotted.Count];
            var columns = new double[plotted.Count][];

            for (int c = 0; c < plotted.Count; c++)
            {
                headers[c] = plotted[c].Name;

                var column = new double[positions.Count];
                for (int r = 0; r < column.Length; r++)
                    column[r] = double.NaN;

                DataPointCollection points = plotted[c].Points;
                for (int i = 0; i < points.Count; i++)
                {
                    DataPoint point = points[i];
                    int row;
                    if (!rowOf.TryGetValue(point.XValue, out row))
                        continue;

                    column[row] = point.IsEmpty || point.YValues.Length == 0
                        ? double.NaN
                        : point.YValues[0];
                }

                columns[c] = column;
            }

            rows = positions.Count;
            traces = plotted.Count;

            return CsvExporter.BuildTable(HorizontalTitle(), headers, positions.ToArray(), columns);
        }

        /// <summary>The X axis title, which is what names the first CSV column.</summary>
        private string HorizontalTitle()
        {
            if (ChartAreas.Count == 0)
                return "X";

            string title = ChartAreas[0].AxisX.Title;
            return string.IsNullOrEmpty(title) ? "X" : title;
        }

        private void RaiseMessage(string message)
        {
            EventHandler<ChartMessageEventArgs> handler = MessageRaised;
            if (handler != null)
                handler(this, new ChartMessageEventArgs(message));
        }

        private void OnMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // The flags also change through the axis editor, so the ticks are refreshed
            // every time rather than only when the menu items themselves are clicked.
            _autoHorizontalItem.Checked = _horizontal.Auto;
            _autoVerticalItem.Checked = _vertical.Auto;
        }

        private void OnAutoHorizontalClick(object sender, EventArgs e)
        {
            SetAutoScale(PlotAxis.Horizontal, _autoHorizontalItem.Checked);
        }

        private void OnAutoVerticalClick(object sender, EventArgs e)
        {
            SetAutoScale(PlotAxis.Vertical, _autoVerticalItem.Checked);
        }

        // -------------------------------------------------------- editing a limit

        // ------------------------------------------------------------- zoom and pan

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (EditorOpen)
                return;

            // The wheel only reaches the control that holds the focus, so a click on the
            // plot is what arms wheel zooming.
            if (CanFocus && !Focused)
                Focus();

            bool pan = e.Button == MouseButtons.Middle ||
                       (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Shift) != 0);
            bool zoom = e.Button == MouseButtons.Left && !pan;

            if ((!pan && !zoom) || !IsInsidePlot(e.Location))
                return;

            _drag = pan ? DragKind.Pan : DragKind.Zoom;
            _dragStart = e.Location;
            _dragLast = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_drag == DragKind.None)
                return;

            if (_drag == DragKind.Pan)
            {
                PanBy(_dragLast, e.Location);
                _dragLast = e.Location;
                return;
            }

            if (!_bandVisible &&
                Math.Abs(e.X - _dragStart.X) < DragThreshold &&
                Math.Abs(e.Y - _dragStart.Y) < DragThreshold)
            {
                return;
            }

            // Drawn straight onto the screen with an inverting frame: repainting the chart
            // on every mouse move to show a selection box would re-render the whole plot.
            HideBand();
            _band = Rectangle.FromLTRB(
                Math.Min(_dragStart.X, e.X), Math.Min(_dragStart.Y, e.Y),
                Math.Max(_dragStart.X, e.X), Math.Max(_dragStart.Y, e.Y));
            ShowBand();
        }

        /// <summary>
        /// Deliberately on the release rather than the press: a tool tip raised during
        /// WM_LBUTTONDOWN is torn down again by the WM_LBUTTONUP that follows it a few
        /// milliseconds later, so the hint shown for a number that cannot be edited would
        /// never actually be readable.
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            DragKind drag = _drag;
            bool banded = _bandVisible;
            Rectangle band = _band;

            HideBand();
            _drag = DragKind.None;

            if (banded)
            {
                ZoomTo(band);
                return;
            }

            if (drag == DragKind.Pan)
            {
                // The range moved during the drag; this is where it is worth reporting.
                if (_dragLast != _dragStart)
                    OnScalesChanged();
                return;
            }

            if (e.Button == MouseButtons.Left)
                TryEditAxisLabel(e.Location);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (e.Delta == 0 || EditorOpen || !IsInsidePlot(e.Location))
                return;

            double factor = e.Delta > 0 ? 1.0 / 1.25 : 1.25;
            bool changed = ZoomAbout(PlotAxis.Horizontal, e.Location.X, factor);
            changed |= ZoomAbout(PlotAxis.Vertical, e.Location.Y, factor);

            if (!changed)
                return;

            ApplyScales();
            Invalidate();
            OnScalesChanged();
        }

        /// <summary>Pins both axes to the dragged rectangle.</summary>
        private void ZoomTo(Rectangle band)
        {
            // A rectangle with no width or height is a slip of the hand, not a zoom.
            if (band.Width < DragThreshold || band.Height < DragThreshold || ChartAreas.Count == 0)
                return;

            ChartArea area = ChartAreas[0];

            bool changed = PinTo(PlotAxis.Horizontal, area.AxisX, band.Left, band.Right);
            changed |= PinTo(PlotAxis.Vertical, area.AxisY, band.Bottom, band.Top);

            if (!changed)
                return;

            ApplyScales();
            Invalidate();
            OnScalesChanged();
        }

        private bool PinTo(PlotAxis which, Axis axis, int fromPixel, int toPixel)
        {
            double first, second;
            if (!TryPixelToValue(axis, fromPixel, out first) || !TryPixelToValue(axis, toPixel, out second))
                return false;

            // Pinned even if the new limits turn out to be unusable: the operator asked for
            // a fixed range by dragging, and half-honouring that would be worse.
            Pin(which);
            ScaleFor(which).SetManualRange(Math.Min(first, second), Math.Max(first, second));
            return true;
        }

        /// <summary>Slides both ranges so the value under the pointer stays under it.</summary>
        private void PanBy(Point from, Point to)
        {
            if (ChartAreas.Count == 0 || from == to)
                return;

            ChartArea area = ChartAreas[0];

            bool changed = SlideBy(PlotAxis.Horizontal, area.AxisX, from.X, to.X);
            changed |= SlideBy(PlotAxis.Vertical, area.AxisY, from.Y, to.Y);

            if (!changed)
                return;

            ApplyScales();
            Invalidate();
        }

        private bool SlideBy(PlotAxis which, Axis axis, int fromPixel, int toPixel)
        {
            double before, after;
            if (!TryPixelToValue(axis, fromPixel, out before) || !TryPixelToValue(axis, toPixel, out after))
                return false;

            double shift = before - after;
            if (shift == 0.0)
                return false;

            Pin(which);
            AxisScale scale = ScaleFor(which);
            scale.SetManualRange(scale.Minimum + shift, scale.Maximum + shift);
            return true;
        }

        /// <summary>Scales one axis about the value under the given pixel.</summary>
        private bool ZoomAbout(PlotAxis which, int pixel, double factor)
        {
            Axis axis = AxisFor(which);
            if (axis == null)
                return false;

            double pivot;
            if (!TryPixelToValue(axis, pixel, out pivot))
                return false;

            Pin(which);
            AxisScale scale = ScaleFor(which);

            scale.SetManualRange(
                pivot + (scale.Minimum - pivot) * factor,
                pivot + (scale.Maximum - pivot) * factor);
            return true;
        }

        private static bool TryPixelToValue(Axis axis, int pixel, out double value)
        {
            value = double.NaN;
            if (axis == null)
                return false;

            try
            {
                value = axis.PixelPositionToValue(pixel);
            }
            catch (Exception)
            {
                // Hit testing and pixel mapping both need a finished render.
                return false;
            }

            return IsFinite(value);
        }

        /// <summary>
        /// True inside the drawing area, worked out from where the axis ends actually
        /// landed. InnerPlotPosition cannot be used for this: while it is set to Auto it
        /// reads back as an empty rectangle.
        /// </summary>
        private bool IsInsidePlot(Point location)
        {
            if (ChartAreas.Count == 0)
                return false;

            ChartArea area = ChartAreas[0];

            double left, right, bottom, top;
            if (!TryValueToPixel(area.AxisX, _horizontal.Minimum, out left) ||
                !TryValueToPixel(area.AxisX, _horizontal.Maximum, out right) ||
                !TryValueToPixel(area.AxisY, _vertical.Minimum, out bottom) ||
                !TryValueToPixel(area.AxisY, _vertical.Maximum, out top))
            {
                return false;
            }

            return location.X >= Math.Min(left, right) && location.X <= Math.Max(left, right)
                && location.Y >= Math.Min(top, bottom) && location.Y <= Math.Max(top, bottom);
        }

        private static bool TryValueToPixel(Axis axis, double value, out double pixel)
        {
            pixel = double.NaN;
            if (axis == null || !IsFinite(value))
                return false;

            try
            {
                pixel = axis.ValueToPixelPosition(value);
            }
            catch (Exception)
            {
                return false;
            }

            return IsFinite(pixel);
        }

        private void ShowBand()
        {
            if (_bandVisible || _band.Width <= 0 || _band.Height <= 0)
                return;

            ControlPaint.DrawReversibleFrame(RectangleToScreen(_band), BackColor, FrameStyle.Dashed);
            _bandVisible = true;
        }

        private void HideBand()
        {
            if (!_bandVisible)
                return;

            ControlPaint.DrawReversibleFrame(RectangleToScreen(_band), BackColor, FrameStyle.Dashed);
            _bandVisible = false;
        }

        /// <summary>
        /// An inverted frame is drawn straight onto the screen, so it has to be taken back
        /// off even when the mouse-up never arrives - losing the capture to another window
        /// would otherwise leave the box painted over whatever is there now.
        /// </summary>
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);

            if (Capture)
                return;

            HideBand();
            _drag = DragKind.None;
        }

        /// <summary>True while an inline editor is on screen; it is the only child control.</summary>
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

        private void TryEditAxisLabel(Point location)
        {
            if (EditorOpen || ChartAreas.Count == 0)
                return;

            HitTestResult hit;
            try
            {
                hit = HitTest(location.X, location.Y);
            }
            catch (Exception)
            {
                // Hit testing needs a finished render; before the first one there is
                // nothing to click on anyway.
                return;
            }

            if (hit == null || hit.ChartElementType != ChartElementType.AxisLabels || hit.Axis == null)
                return;

            var label = hit.Object as CustomLabel;
            if (label == null)
                return;

            ChartArea area = ChartAreas[0];
            bool horizontal = ReferenceEquals(hit.Axis, area.AxisX);
            if (!horizontal && !ReferenceEquals(hit.Axis, area.AxisY))
                return;

            PlotAxis which = horizontal ? PlotAxis.Horizontal : PlotAxis.Vertical;
            if (ScaleFor(which).Auto)
            {
                ShowHint(location, "Auto scale " + (horizontal ? "X" : "Y") +
                                   " is on.\r\nRight-click the plot to turn it off first.");
                return;
            }

            // A label's From/To straddle its tick, so their midpoint is the exact value
            // printed - no need to parse the text the operator sees.
            double value = (label.FromPosition + label.ToPosition) / 2.0;
            double minimum = hit.Axis.Minimum;
            double maximum = hit.Axis.Maximum;
            double span = maximum - minimum;
            if (!(span > 0.0))
                return;

            bool upperEnd;
            if (Math.Abs(value - minimum) <= span * 1e-6)
                upperEnd = false;
            else if (Math.Abs(value - maximum) <= span * 1e-6)
                upperEnd = true;
            else
            {
                ShowHint(location, "Only the first and last number set the range.");
                return;
            }

            bool upper = upperEnd;
            InlineNumberBox.Open(this, EditorBounds(hit.Axis, value, horizontal, location), value,
                delegate(double typed) { CommitEnd(which, upper, typed, location); });
        }

        private void CommitEnd(PlotAxis axis, bool upperEnd, double value, Point where)
        {
            if (!ScaleFor(axis).SetEnd(upperEnd, value))
            {
                ShowHint(where, upperEnd
                    ? "The last number has to stay above the first one."
                    : "The first number has to stay below the last one.");
                return;
            }

            ApplyScales();
            Invalidate();
            OnScalesChanged();
        }

        /// <summary>Puts the editor over the number that was clicked.</summary>
        private Rectangle EditorBounds(Axis axis, double value, bool horizontal, Point click)
        {
            int height = Font.Height + 6;
            const int Width = 76;

            double along;
            try
            {
                along = axis.ValueToPixelPosition(value);
            }
            catch (Exception)
            {
                along = horizontal ? click.X : click.Y;
            }

            if (double.IsNaN(along) || double.IsInfinity(along))
                along = horizontal ? click.X : click.Y;

            return horizontal
                ? new Rectangle((int)Math.Round(along) - Width / 2, click.Y - height / 2, Width, height)
                : new Rectangle(click.X + 18 - Width, (int)Math.Round(along) - height / 2, Width, height);
        }

        private void ShowHint(Point where, string text)
        {
            _hint.Show(text, this, where.X + 14, where.Y + 14, 2500);
        }

        // ------------------------------------------------------------------ painting

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // OnPaint always covers the whole client area, either with a fresh render or
            // with the cached frame, so erasing first would only add a flash.
            if (_frame == null)
                base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Bitmap frame = EnsureFrame();

            if (frame == null)
            {
                // Degenerate size; there is nothing to cache into.
                if (_updateDepth == 0)
                    PaintGuarded(e);
                return;
            }

            if (_updateDepth > 0)
            {
                // Data is half written. EndDataUpdate() invalidates once it is consistent.
                e.Graphics.DrawImageUnscaled(frame, 0, 0);
                _repaintQueued = true;
                return;
            }

            using (Graphics graphics = Graphics.FromImage(frame))
            {
                var args = new PaintEventArgs(graphics, new Rectangle(Point.Empty, frame.Size));
                if (!PaintGuarded(args))
                {
                    // The render tore; keep showing whatever the frame held before rather
                    // than blitting a partial one.
                    graphics.Clear(BackColor);
                }
            }

            e.Graphics.DrawImageUnscaled(frame, 0, 0);
        }

        /// <summary>Runs the real chart render. Returns false when it had to be abandoned.</summary>
        private bool PaintGuarded(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);
                _repaintQueued = false;
                return true;
            }
            catch (InvalidOperationException)
            {
                // Drop this frame and ask for another one, rather than letting the
                // exception escape the paint handler. Posted, so it cannot spin.
                if (_repaintQueued || !IsHandleCreated)
                    return false;

                _repaintQueued = true;
                BeginInvoke((MethodInvoker)delegate
                {
                    _repaintQueued = false;
                    Invalidate();
                });
                return false;
            }
        }

        private Bitmap EnsureFrame()
        {
            Size size = ClientSize;
            if (size.Width <= 0 || size.Height <= 0)
                return null;

            if (_frame != null && _frame.Size == size)
                return _frame;

            if (_frame != null)
                _frame.Dispose();

            _frame = new Bitmap(size.Width, size.Height);
            using (Graphics graphics = Graphics.FromImage(_frame))
                graphics.Clear(BackColor);

            return _frame;
        }

        protected override void OnResize(EventArgs e)
        {
            // Drop the cached frame so the next paint renders at the new size rather than
            // stretching a stale one.
            if (_frame != null && _frame.Size != ClientSize)
            {
                _frame.Dispose();
                _frame = null;
            }

            base.OnResize(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HideBand();

                if (_frame != null)
                {
                    _frame.Dispose();
                    _frame = null;
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
