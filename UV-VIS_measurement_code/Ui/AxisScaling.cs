using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace UvVisMeasurement.Ui
{
    /// <summary>Which scale of a plot a range applies to.</summary>
    internal enum PlotAxis
    {
        Horizontal,
        Vertical,

        /// <summary>The 2D map's colour bar. It carries numbers and a range like an axis.</summary>
        Color
    }

    /// <summary>
    /// Auto/manual range of one axis.
    ///
    /// Two ranges are tracked instead of one: what the data suggests, refreshed on every
    /// frame, and what the operator typed. Turning auto scaling back on therefore snaps
    /// straight back to the live range rather than leaving the axis wherever the last
    /// manual entry put it.
    ///
    /// Turning it off seeds the manual range from whatever is on screen at that instant,
    /// so the plot never jumps at the moment of the switch and the operator edits numbers
    /// they can actually see.
    /// </summary>
    internal sealed class AxisScale
    {
        private double _autoMinimum = double.NaN;
        private double _autoMaximum = double.NaN;
        private double _manualMinimum = double.NaN;
        private double _manualMaximum = double.NaN;
        private bool _auto = true;

        public bool Auto
        {
            get { return _auto; }
        }

        /// <summary>Lower limit in force. NaN asks the plot to work it out from the data.</summary>
        public double Minimum
        {
            get { return _auto ? _autoMinimum : _manualMinimum; }
        }

        public double Maximum
        {
            get { return _auto ? _autoMaximum : _manualMaximum; }
        }

        /// <summary>
        /// Records the range the data suggests. Returns true when that changes what is
        /// displayed, which is only the case while the axis is auto scaling.
        /// </summary>
        public bool SetDataRange(double minimum, double maximum)
        {
            if (Same(_autoMinimum, minimum) && Same(_autoMaximum, maximum))
                return false;

            _autoMinimum = minimum;
            _autoMaximum = maximum;
            return _auto;
        }

        /// <summary>Hands the axis back to auto scaling.</summary>
        public void GoAuto()
        {
            _auto = true;
        }

        /// <summary>
        /// Pins the axis, seeding it with the range currently on screen. The caller passes
        /// the resolved range because an auto-scaled axis usually holds NaN, which would
        /// give the operator nothing to edit.
        /// </summary>
        public void GoManual(double shownMinimum, double shownMaximum)
        {
            _auto = false;

            if (IsUsableRange(shownMinimum, shownMaximum))
            {
                _manualMinimum = shownMinimum;
                _manualMaximum = shownMaximum;
            }
            else if (!IsUsableRange(_manualMinimum, _manualMaximum))
            {
                // Nothing has ever been drawn on this axis, so there is no sensible range
                // to inherit. 0..1 at least gives the operator two numbers to overwrite.
                _manualMinimum = 0.0;
                _manualMaximum = 1.0;
            }
        }

        public bool SetManualRange(double minimum, double maximum)
        {
            if (!IsUsableRange(minimum, maximum))
                return false;

            _auto = false;
            _manualMinimum = minimum;
            _manualMaximum = maximum;
            return true;
        }

        /// <summary>
        /// Moves one end of a manual range. Returns false when the value would collapse or
        /// invert the axis, which the caller reports rather than silently ignoring.
        /// </summary>
        public bool SetEnd(bool upperEnd, double value)
        {
            if (_auto)
                return false;

            return upperEnd
                ? SetManualRange(_manualMinimum, value)
                : SetManualRange(value, _manualMaximum);
        }

        public static bool IsUsableRange(double minimum, double maximum)
        {
            return !double.IsNaN(minimum) && !double.IsNaN(maximum)
                   && !double.IsInfinity(minimum) && !double.IsInfinity(maximum)
                   && maximum > minimum;
        }

        private static bool Same(double a, double b)
        {
            return a.Equals(b);   // NaN.Equals(NaN) is true, unlike ==
        }
    }

    /// <summary>
    /// One-shot text box laid over a number that is painted rather than typed - an axis
    /// tick label. It removes itself when the operator commits with Enter, abandons with
    /// Escape, or clicks away.
    ///
    /// The value is parsed the same way the device table parses its cells: invariant
    /// first, then the current culture, so both "0.5" and a locale that writes "0,5" work.
    /// </summary>
    internal sealed class InlineNumberBox
    {
        private readonly Control _owner;
        private readonly TextBox _box;
        private readonly Action<double> _commit;
        private bool _closed;

        private InlineNumberBox(Control owner, Rectangle bounds, double current, Action<double> commit)
        {
            _owner = owner;
            _commit = commit;

            _box = new TextBox
            {
                Bounds = bounds,
                Text = current.ToString("0.######", CultureInfo.CurrentCulture),
                TextAlign = HorizontalAlignment.Center,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.Black
            };

            _box.KeyDown += OnKeyDown;
            _box.KeyPress += OnKeyPress;
            _box.Leave += OnLeave;

            owner.Controls.Add(_box);
            _box.BringToFront();
            _box.Focus();
            _box.SelectAll();
        }

        /// <summary>Opens an editor over <paramref name="bounds"/> in the owner's client coordinates.</summary>
        public static void Open(Control owner, Rectangle bounds, double current, Action<double> commit)
        {
            if (owner == null || commit == null)
                return;

            // Keep the box inside the plot; an editor half off the control cannot be used.
            Rectangle client = owner.ClientRectangle;
            if (bounds.Right > client.Right) bounds.X = client.Right - bounds.Width;
            if (bounds.Bottom > client.Bottom) bounds.Y = client.Bottom - bounds.Height;
            if (bounds.X < client.Left) bounds.X = client.Left;
            if (bounds.Y < client.Top) bounds.Y = client.Top;

            new InlineNumberBox(owner, bounds, current, commit);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Close(true);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Close(false);
                e.Handled = true;
            }
        }

        private static void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            // Without this a single-line TextBox beeps at Enter and Escape.
            if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Escape)
                e.Handled = true;
        }

        private void OnLeave(object sender, EventArgs e)
        {
            // Clicking away commits, matching how the device table finishes a cell edit.
            Close(true);
        }

        private void Close(bool apply)
        {
            if (_closed)
                return;
            _closed = true;

            double value = 0.0;
            bool parsed = apply && TryParse(_box.Text, out value);

            _box.KeyDown -= OnKeyDown;
            _box.KeyPress -= OnKeyPress;
            _box.Leave -= OnLeave;

            // Disposing a control from inside its own Leave handler is asking for trouble,
            // so the teardown is posted behind the current message.
            TextBox box = _box;
            Control owner = _owner;
            owner.BeginInvoke((MethodInvoker)delegate
            {
                owner.Controls.Remove(box);
                box.Dispose();
                if (owner.CanFocus)
                    owner.Focus();
            });

            if (parsed)
                _commit(value);
        }

        private static bool TryParse(string text, out double value)
        {
            text = (text ?? string.Empty).Trim();

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }
    }
}
