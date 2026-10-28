using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Localization;

namespace UvVisMeasurement.Ui
{
    /// <summary>
    /// Shown while the spectrometers are opened, which takes a few seconds on real hardware
    /// and would otherwise look like a program that did not start.
    /// </summary>
    internal sealed class SplashForm : Form
    {
        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Theme.Card;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(LogicalToDeviceUnits(420), LogicalToDeviceUnits(170));
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int margin = LogicalToDeviceUnits(24);
            using (var border = new Pen(Theme.Accent, LogicalToDeviceUnits(2)))
                g.DrawRectangle(border, 1, 1, ClientSize.Width - 2, ClientSize.Height - 2);

            using (var bar = new SolidBrush(Theme.Accent))
                g.FillRectangle(bar, 0, 0, ClientSize.Width, LogicalToDeviceUnits(6));

            using (var title = new Font(Font.FontFamily, Font.SizeInPoints * 1.9f, FontStyle.Bold))
            using (var brush = new SolidBrush(Theme.Accent))
                g.DrawString("UV-VIS meas", title, brush, margin, margin);

            using (var muted = new SolidBrush(Theme.TextMuted))
            {
                g.DrawString(L.F("splash.version", "Version {0}", Program.Version), Font, muted,
                    margin, margin + LogicalToDeviceUnits(42));

                string state = SpectrometerBackend.IsSimulated
                    ? L.T("splash.demo", "Demo mode - simulated spectrometers")
                    : L.T("splash.opening", "Opening spectrometers...");
                g.DrawString(state, Font, muted, margin, ClientSize.Height - margin - Font.Height);
            }
        }
    }
}
