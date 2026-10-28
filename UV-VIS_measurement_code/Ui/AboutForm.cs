using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using UvVisMeasurement.Localization;

namespace UvVisMeasurement.Ui
{
    internal sealed class AboutForm : Form
    {
        public AboutForm(string driverName)
        {
            Text = L.T("about.title", "About UV-VIS meas");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Card;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(LogicalToDeviceUnits(460), LogicalToDeviceUnits(356));

            int margin = LogicalToDeviceUnits(16);

            var product = new Label
            {
                AutoSize = true,
                Location = new Point(margin, margin),
                Font = new Font(Font.FontFamily, Font.SizeInPoints * 1.6f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                Text = "UV-VIS meas"
            };

            var version = new Label
            {
                AutoSize = true,
                Location = new Point(margin, product.Bottom + LogicalToDeviceUnits(6)),
                ForeColor = Theme.TextMuted,
                Text = L.F("about.version", "Version {0}   (built {1})",
                    Program.Version, BuildDate().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            };

            var copyright = new Label
            {
                AutoSize = true,
                Location = new Point(margin, version.Bottom + LogicalToDeviceUnits(2)),
                ForeColor = Theme.TextMuted,
                Text = Program.Copyright
            };

            var details = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Theme.Surface,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(margin, copyright.Bottom + LogicalToDeviceUnits(12)),
                Size = new Size(ClientSize.Width - 2 * margin, LogicalToDeviceUnits(200)),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Text = Details(driverName)
            };

            var close = new Button
            {
                Text = L.T("about.close", "Close"),
                DialogResult = DialogResult.OK,
                Size = new Size(LogicalToDeviceUnits(96), LogicalToDeviceUnits(28)),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            close.Location = new Point(ClientSize.Width - margin - close.Width,
                ClientSize.Height - margin - close.Height);
            Theme.StylePrimary(close);

            Controls.AddRange(new Control[] { product, version, copyright, details, close });
            AcceptButton = close;
            CancelButton = close;

            // Without this the read-only box opens with every line selected in blue.
            Shown += delegate
            {
                details.Select(0, 0);
                close.Focus();
            };
        }

        private static string Details(string driverName)
        {
            string newLine = Environment.NewLine;
            var text = new System.Text.StringBuilder();

            text.AppendLine(L.T("about.description",
                "Spectrum acquisition, dark / white-reference absorbance and dual-beam lamp-drift " +
                "correction for Ocean Optics / Ocean Insight spectrometers."));
            text.AppendLine();
            text.AppendLine(L.F("about.driver", "Spectrometer backend: {0}",
                string.IsNullOrEmpty(driverName) ? L.T("about.driverUnknown", "not opened yet") : driverName));
            text.AppendLine(L.F("about.runtime", ".NET Framework: {0}  ({1})",
                Environment.Version, Environment.Is64BitProcess ? "x64" : "x86"));
            text.AppendLine();
            text.AppendLine(L.T("about.thirdParty", "Third-party components"));
            text.AppendLine(L.T("about.omniDriver",
                "  OmniDriver (NETOmniDriver-NET40) - Ocean Optics, Inc. Installed separately; " +
                "not distributed with this program."));
            text.AppendLine(L.T("about.notices",
                "  The full notices are in THIRD-PARTY-NOTICES.md next to the program."));
            text.AppendLine();
            text.AppendLine(L.T("about.files", "Files"));
            text.AppendLine("  " + Program.SettingsPath);
            text.AppendLine("  " + Program.ErrorLogPath);

            return text.ToString().Replace("\n", newLine);
        }

        /// <summary>Write time of the running EXE, i.e. when this copy was built or installed.</summary>
        private static DateTime BuildDate()
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                return string.IsNullOrEmpty(path) ? DateTime.Now : File.GetLastWriteTime(path);
            }
            catch (Exception)
            {
                return DateTime.Now;
            }
        }
    }
}
