using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace UvVisMeasurement.Ui
{
    internal enum WorkflowStep
    {
        Start,
        Dark,
        White,
        Measure
    }

    internal enum WorkflowStepState
    {
        Pending,
        Next,
        Done,

        /// <summary>Passed over: a white reference exists but no dark was captured.</summary>
        Skipped
    }

    /// <summary>
    /// The measurement order as four clickable steps: start the acquisition, capture Dark,
    /// capture White, then measure and save. Dark and White can only be taken from a
    /// spectrum that is on screen, which is why starting comes first. The step to do next
    /// is highlighted; clicking a step raises <see cref="StepClicked"/> and the form runs
    /// the same action as the matching button.
    /// </summary>
    internal sealed class WorkflowStrip : Control
    {
        private readonly WorkflowStepState[] _states = new WorkflowStepState[4];
        private readonly string[] _labels = { "Start", "Dark", "White", "Measure / Save" };
        private int _hover = -1;

        public event EventHandler<WorkflowStepEventArgs> StepClicked;

        public WorkflowStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
        }

        public void SetLabels(string start, string dark, string white, string measure)
        {
            _labels[0] = start;
            _labels[1] = dark;
            _labels[2] = white;
            _labels[3] = measure;
            Invalidate();
        }

        public void SetProgress(bool running, bool hasDark, bool hasWhite)
        {
            _states[0] = running ? WorkflowStepState.Done : WorkflowStepState.Next;
            _states[1] = hasDark ? WorkflowStepState.Done
                : hasWhite ? WorkflowStepState.Skipped
                : running ? WorkflowStepState.Next : WorkflowStepState.Pending;
            _states[2] = hasWhite ? WorkflowStepState.Done
                : running && hasDark ? WorkflowStepState.Next : WorkflowStepState.Pending;
            _states[3] = running && hasWhite ? WorkflowStepState.Next : WorkflowStepState.Pending;
            Invalidate();
        }

        public WorkflowStepState StateOf(WorkflowStep step)
        {
            return _states[(int)step];
        }

        private Rectangle CellBounds(int index)
        {
            int width = Width / _states.Length;
            return new Rectangle(index * width, 0, width, Height);
        }

        private int HitTest(Point point)
        {
            for (int i = 0; i < _states.Length; i++)
                if (CellBounds(i).Contains(point))
                    return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hover = HitTest(e.Location);
            if (hover != _hover)
            {
                _hover = hover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int index = HitTest(e.Location);
            EventHandler<WorkflowStepEventArgs> handler = StepClicked;
            if (index >= 0 && e.Button == MouseButtons.Left && handler != null)
                handler(this, new WorkflowStepEventArgs((WorkflowStep)index));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            int diameter = Math.Min(Height - LogicalToDeviceUnits(6), LogicalToDeviceUnits(20));
            int gap = LogicalToDeviceUnits(6);

            for (int i = 0; i < _states.Length; i++)
            {
                Rectangle cell = CellBounds(i);
                if (i == _hover)
                {
                    using (var hover = new SolidBrush(Theme.AccentSoft))
                        g.FillRectangle(hover, cell);
                }

                var circle = new Rectangle(cell.Left + gap, cell.Top + (cell.Height - diameter) / 2, diameter, diameter);
                Color color = ColorOf(_states[i]);

                if (i > 0)
                {
                    using (var line = new Pen(Theme.Border, LogicalToDeviceUnits(2)))
                        g.DrawLine(line, cell.Left - gap, circle.Top + diameter / 2, cell.Left + gap - 2, circle.Top + diameter / 2);
                }

                using (var fill = new SolidBrush(_states[i] == WorkflowStepState.Pending ? Theme.Card : color))
                using (var ring = new Pen(color, LogicalToDeviceUnits(2)))
                {
                    g.FillEllipse(fill, circle);
                    g.DrawEllipse(ring, circle);
                }

                string mark = _states[i] == WorkflowStepState.Done ? "\u2713"
                    : _states[i] == WorkflowStepState.Skipped ? "!"
                    : (i + 1).ToString();
                using (var markFont = new Font(Font, FontStyle.Bold))
                using (var markBrush = new SolidBrush(_states[i] == WorkflowStepState.Pending ? Theme.TextMuted : Color.White))
                using (var centred = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(mark, markFont, markBrush, circle, centred);
                }

                var text = new Rectangle(circle.Right + gap / 2, cell.Top, cell.Right - circle.Right - gap, cell.Height);
                FontStyle style = _states[i] == WorkflowStepState.Next ? FontStyle.Bold : FontStyle.Regular;
                using (var labelFont = new Font(Font, style))
                {
                    TextRenderer.DrawText(g, _labels[i], labelFont, text,
                        _states[i] == WorkflowStepState.Pending ? Theme.TextMuted : Theme.Text,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private static Color ColorOf(WorkflowStepState state)
        {
            switch (state)
            {
                case WorkflowStepState.Done: return Theme.Success;
                case WorkflowStepState.Next: return Theme.Accent;
                case WorkflowStepState.Skipped: return Theme.Warning;
                default: return Theme.Border;
            }
        }
    }

    internal sealed class WorkflowStepEventArgs : EventArgs
    {
        public WorkflowStepEventArgs(WorkflowStep step) { Step = step; }
        public WorkflowStep Step { get; private set; }
    }
}
