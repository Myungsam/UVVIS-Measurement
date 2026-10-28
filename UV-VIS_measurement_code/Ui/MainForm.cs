using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Io;
using UvVisMeasurement.Localization;

namespace UvVisMeasurement.Ui
{
    internal partial class MainForm : Form
    {
        /// <summary>Inclusive numeric range a table cell is clamped to.</summary>
        private struct Limits
        {
            public double Minimum;
            public double Maximum;

            public Limits(double minimum, double maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public double Clamp(double value)
            {
                if (value < Minimum) return Minimum;
                if (value > Maximum) return Maximum;
                return value;
            }
        }

        /// <summary>
        /// Trace colours for every plot, in the order series are added.
        ///
        /// All of them are dark enough to read as a thin line on the white plot background:
        /// the palette the plots used to have was picked for a black one, where yellow and
        /// cyan stand out and here would be all but invisible. Neighbouring entries differ
        /// in hue as well as lightness, so traces stay apart from each other on screen and
        /// in a greyscale printout.
        /// </summary>
        private static readonly Color[] TraceColors =
        {
            Color.FromArgb(0x1F, 0x77, 0xB4),   // blue
            Color.FromArgb(0xD6, 0x27, 0x28),   // red
            Color.FromArgb(0x2C, 0xA0, 0x2C),   // green
            Color.FromArgb(0xFF, 0x7F, 0x0E),   // orange
            Color.FromArgb(0x94, 0x67, 0xBD),   // purple
            Color.FromArgb(0x8C, 0x56, 0x4B),   // brown
            Color.FromArgb(0x17, 0x8E, 0x9F),   // teal
            Color.FromArgb(0x60, 0x60, 0x60)    // grey
        };

        private readonly AcquisitionEngine _engine;
        private readonly MeasurementSettings _settings = new MeasurementSettings();
        private readonly TimedSeriesStore _timedStore = new TimedSeriesStore();
        private readonly List<double> _traceWavelengths = new List<double>();
        private readonly List<double> _traceTimeBuffer = new List<double>();
        private readonly List<double> _traceValueBuffer = new List<double>();

        private readonly Timer _settingsDebounce = new Timer { Interval = 300 };
        private readonly Timer _slowRefresh = new Timer { Interval = 250 };

        /// <summary>The Hardware Settings tab, built from whatever the driver reported.</summary>
        private HardwareSettingsView _hardware;

        private UserSettings _userSettings = new UserSettings();

        private DeviceInfo[] _devices = new DeviceInfo[0];

        /// <summary>One entry per grid row, in the same order as <see cref="_devices"/>.</summary>
        private DeviceSettings[] _deviceSettings = new DeviceSettings[0];

        private SplashForm _splash;
        private SpectrumFrame _lastFrame;
        private ReferenceSnapshot _lastReferences;
        private string _appliedDelaysText = string.Empty;

        /// <summary>
        /// Pin states per spectrometer, keyed by <see cref="DeviceInfo.Identity"/> rather
        /// than by index so an unplugged device cannot hand its entry to another one.
        /// Kept for the CSV header; the Hardware Settings tab holds its own copy.
        /// </summary>
        private readonly Dictionary<string, GpioChannelInfo[]> _gpioByDevice =
            new Dictionary<string, GpioChannelInfo[]>(StringComparer.Ordinal);

        private bool _accumulateTimed;
        private bool _suppressSettingsPush;
        private bool _suppressGridEvents;
        private bool _suppressRunToggle;
        private bool _suppressHeatSync;

        /// <summary>What the run toggle is asking for, which may briefly differ from the engine.</summary>
        private bool _runRequested;

        private bool _timedDirty;
        private bool _heatmapSeeded;
        private double _frameRate;
        private DateTime _lastFrameTime = DateTime.MinValue;

        public MainForm()
        {
            InitializeComponent();

            // Created here and not in a field initializer: the engine captures
            // SynchronizationContext.Current to deliver its events, and WinForms only installs
            // its UI-thread context once the first control exists. Created any earlier, every
            // event would run on a thread-pool thread and touch the controls cross-thread.
            _engine = new AcquisitionEngine(SpectrometerBackend.CreateSession);

            ApplyProductTitle();
            ApplyWindowIcon();
            ConfigureInputs();
            RestoreUserSettings();
            ConfigureDeviceGrid();
            ConfigureHardwareTab();
            CaptureLayoutBaseline();
            StyleCharts();
            BuildShell();
            WireEvents();
            UpdateRunStateUi();
        }

        /// <summary>
        /// Remembers the designed positions of everything that moves when the device table
        /// grows, so repeated device-list changes cannot accumulate offsets.
        /// </summary>
        private void CaptureLayoutBaseline()
        {
            _baseGridHeight = gridDevices.Height;
            _baseParametersHeight = grpParameters.Height;

            foreach (Control control in ControlsBelowGrid())
                _baseTops[control] = control.Top;
        }

        private Control[] ControlsBelowGrid()
        {
            return new Control[]
            {
                lblAppliedDelay, btnCaptureDark, btnCaptureReference, btnClearReference,
                lblReferenceState, lblDriftState, grpSave, grpTimedDatasets
            };
        }

        /// <summary>
        /// Sizes the table to show every spectrometer at once and pushes the controls below
        /// it down by the same amount. A scroll bar inside a four-row-tall table would make
        /// rows unreachable in practice; panelRight scrolls instead, so growing past the
        /// window is harmless.
        /// </summary>
        private void AdjustGridHeight()
        {
            int rowHeight = gridDevices.RowCount > 0
                ? gridDevices.Rows[0].Height
                : Math.Max(18, gridDevices.RowTemplate.Height);

            // Two rows is the designed minimum; beyond six the table scrolls rather than
            // pushing the save controls entirely off the panel.
            int visibleRows = Math.Min(6, Math.Max(2, gridDevices.RowCount));
            int desired = gridDevices.ColumnHeadersHeight + visibleRows * rowHeight + 4;

            int delta = Math.Max(0, desired - _baseGridHeight);
            if (gridDevices.Height == _baseGridHeight + delta)
                return;

            panelRight.SuspendLayout();
            try
            {
                gridDevices.Height = _baseGridHeight + delta;
                grpParameters.Height = _baseParametersHeight + delta;

                foreach (Control control in ControlsBelowGrid())
                {
                    int top;
                    if (_baseTops.TryGetValue(control, out top))
                        control.Top = top + delta;
                }
            }
            finally
            {
                panelRight.ResumeLayout(true);
            }
        }

        // ---------------------------------------------------------------- setup

        /// <summary>
        /// Takes the title-bar and taskbar icon from the EXE's own Win32 icon resource
        /// (set by ApplicationIcon in the project file), so the icon lives in exactly
        /// one place instead of being duplicated into a .resx.
        /// </summary>
        private void ApplyWindowIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                    Icon = icon;
            }
            catch (Exception)
            {
                // Cosmetic only - a missing icon must never stop the program starting.
            }
        }

        /// <summary>
        /// The version comes from the assembly (Properties\AssemblyInfo.cs) so the window,
        /// the crash log and the file properties can never disagree. Demo mode is stamped
        /// into the title bar so a screenshot of simulated data cannot pass for a measurement.
        /// </summary>
        private void ApplyProductTitle()
        {
            string version = Program.Version;
            lblTitle.Text = "UV-VIS Measurement v" + version;
            Text = "Ocean Optics - UV-VIS meas  v" + version +
                   (SpectrometerBackend.IsSimulated
                       ? "   [" + L.T("title.demo", "DEMO MODE - simulated spectrometers") + "]"
                       : string.Empty);
        }

        private void ConfigureInputs()
        {
            _suppressSettingsPush = true;

            SetRange(numMinInterval, 0, 600000, 0, 100);
            SetRange(numAverage, 1, 5000, 0, 1);
            SetRange(numTraceWavelength, 0, 5000, 2, 500);
            SetRange(numHeatMin, -1000, 1000, 3, 0);
            SetRange(numHeatMax, -1000, 1000, 3, 1);

            numTraceWavelength.Increment = 1m;
            numHeatMin.Increment = 0.05m;
            numHeatMax.Increment = 0.05m;

            // FlatStyle is what makes a latching CheckBox actually paint its pressed state
            // in a colour of our choosing; the visual-style renderer ignores BackColor.
            chkRun.FlatStyle = FlatStyle.Flat;
            chkRun.FlatAppearance.BorderSize = 1;
            chkRun.FlatAppearance.CheckedBackColor = Color.LimeGreen;
            chkRun.UseVisualStyleBackColor = false;
            chkRun.BackColor = SystemColors.Control;

            // This combo edits the measurement mode of the spectrometer under "Use"; the
            // Hardware Settings tab edits any of them. Both take their names from the same
            // table so they can never drift apart.
            foreach (TriggerMode mode in TriggerModes.All)
                cmbTriggerMode.Items.Add(TriggerModes.Label(mode));

            cmbTriggerMode.SelectedIndex = 0;

            txtSavePath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            numHeatMin.Enabled = false;
            numHeatMax.Enabled = false;

            _suppressSettingsPush = false;
        }

        private void ConfigureHardwareTab()
        {
            _hardware = new HardwareSettingsView(panelHardware);
            _hardware.ChangeRequested += OnHardwareChangeRequested;
            _hardware.Reported += OnChartMessage;
        }

        private void ConfigureDeviceGrid()
        {
            gridDevices.RowTemplate.Height = LogicalToDeviceUnits(22);
            // Fixed in the designer and not autoscaled by DataGridView: two header lines need it.
            gridDevices.ColumnHeadersHeight = LogicalToDeviceUnits(42);
            gridDevices.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            gridDevices.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            gridDevices.ColumnHeadersDefaultCellStyle.Padding = new Padding(1);

            // This is a settings table, not a list to pick from: highlighting the current
            // cell in system blue would read as a selection the user did not make.
            gridDevices.DefaultCellStyle.SelectionBackColor = SystemColors.Window;
            gridDevices.DefaultCellStyle.SelectionForeColor = SystemColors.ControlText;

            colIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colIntegration.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            colTriggerDelay.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            // Index and model come from the driver and are never editable, so they are
            // tinted to distinguish them from the cells that do accept input.
            foreach (DataGridViewColumn column in new DataGridViewColumn[] { colIndex, colModel })
            {
                column.DefaultCellStyle.BackColor = ReadOnlyCellColor;
                column.DefaultCellStyle.SelectionBackColor = ReadOnlyCellColor;
                column.DefaultCellStyle.SelectionForeColor = SystemColors.ControlText;
            }
        }

        private static void SetRange(NumericUpDown control, decimal minimum, decimal maximum,
                                     int decimals, decimal value)
        {
            control.Minimum = minimum;
            control.Maximum = maximum;
            control.DecimalPlaces = decimals;
            control.Value = Clamp(value, minimum, maximum);
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private void StyleCharts()
        {
            StyleChart(chartAbsorption, "Wavelength (nm)", "Absorbance (OD)");
            StyleChart(chartFluorescence, "Wavelength (nm)", "Intensity (counts)");
            StyleChart(chartProbe, "Wavelength (nm)", "Intensity (counts)");
            StyleChart(chartTimed, "Time (s)", "Absorbance (OD)");

            AddSeries(chartAbsorption, "Absorbance", TraceColors[0]);
            AddSeries(chartFluorescence, "Intensity", TraceColors[0]);

            // Plotting the stored dark and white reference next to the live spectrum is
            // what makes a capture verifiable at a glance.
            AddSeries(chartProbe, "Sample", TraceColors[0]);
            AddSeries(chartProbe, "Dark", TraceColors[1]);
            AddSeries(chartProbe, "White reference", TraceColors[2]);

            // Without an explicit range an empty chart draws no axes at all, so the
            // idle window would show a blank black rectangle.
            SetStartRange(chartAbsorption, 300, 1100, -0.1, 1.0);
            SetStartRange(chartFluorescence, 300, 1100, 0, 65535);
            SetStartRange(chartProbe, 300, 1100, 0, 65535);
            SetStartRange(chartTimed, 0, 60, -0.1, 1.0);

            foreach (SpectrumChart chart in SpectrumCharts())
            {
                chart.ScalesChanged += OnChartScalesChanged;
                chart.MessageRaised += OnChartMessage;
            }

            heatmap.ScalesChanged += OnHeatmapScalesChanged;
        }

        private SpectrumChart[] SpectrumCharts()
        {
            return new[] { chartAbsorption, chartFluorescence, chartProbe, chartTimed };
        }

        private static void SetStartRange(SpectrumChart chart, double xMin, double xMax, double yMin, double yMax)
        {
            chart.SetDataRange(PlotAxis.Horizontal, xMin, xMax);
            chart.SetDataRange(PlotAxis.Vertical, yMin, yMax);
        }

        /// <summary>Reports an axis the operator has pinned, so a frozen plot is never a mystery.</summary>
        private void OnChartScalesChanged(object sender, EventArgs e)
        {
            var chart = (SpectrumChart)sender;
            statusState.Text = L.F("status.axes", "{0} axes:  {1}", ChartTitleOf(chart), chart.DescribeScales());
        }

        private void OnChartMessage(object sender, ChartMessageEventArgs e)
        {
            statusState.Text = e.Message;
        }

        private string ChartTitleOf(SpectrumChart chart)
        {
            if (chart == chartAbsorption) return "Absorption";
            if (chart == chartFluorescence) return "Fluorescence";
            if (chart == chartProbe) return "Probe";
            return "Timed";
        }

        /// <summary>
        /// White plot area with light grid lines.
        ///
        /// Only the plotting area is styled here: the control's own BackColor stays on the
        /// system colour so the plot sits in the window like every other control and the
        /// rest of the application keeps its theme.
        /// </summary>
        private static void StyleChart(Chart chart, string xTitle, string yTitle)
        {
            chart.ChartAreas.Clear();
            chart.Series.Clear();
            chart.Legends.Clear();

            chart.BackColor = SystemColors.Control;
            chart.AntiAliasing = AntiAliasingStyles.Graphics;

            var area = new ChartArea("main")
            {
                BackColor = Color.White,
                BorderColor = Color.FromArgb(150, 150, 150),
                BorderWidth = 1,
                BorderDashStyle = ChartDashStyle.Solid
            };

            foreach (Axis axis in new[] { area.AxisX, area.AxisY })
            {
                axis.LineColor = Color.FromArgb(110, 110, 110);
                axis.LabelStyle.ForeColor = SystemColors.ControlText;
                axis.TitleForeColor = Color.FromArgb(0, 90, 0);

                // Light enough to stay behind the traces rather than compete with them.
                axis.MajorGrid.LineColor = Color.FromArgb(216, 216, 216);
                axis.MajorGrid.LineDashStyle = ChartDashStyle.Solid;
                axis.MinorGrid.Enabled = false;
                axis.MajorTickMark.LineColor = Color.FromArgb(110, 110, 110);
                axis.IsStartedFromZero = false;
                // Elapsed-time ticks land on values like 5.0001276; trim the noise digits.
                axis.LabelStyle.Format = "0.###";
            }

            area.AxisX.Title = xTitle;
            area.AxisY.Title = yTitle;

            chart.ChartAreas.Add(area);
            chart.Legends.Add(new Legend("legend")
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Far,
                IsDockedInsideChartArea = true,
                DockedToChartArea = "main",
                // Opaque with a border: on a white plot a translucent legend would blend
                // into the background and let traces run through the text.
                BackColor = Color.FromArgb(250, 250, 250),
                BorderColor = Color.FromArgb(190, 190, 190),
                BorderWidth = 1
            });
        }

        private static Series AddSeries(Chart chart, string name, Color color)
        {
            var series = new Series(name)
            {
                ChartType = SeriesChartType.FastLine,
                Color = color,
                // Two pixels of a solid dark line stays readable on the white background
                // at the sizes this window is used at.
                BorderWidth = 2,
                ChartArea = "main",
                Legend = "legend",
                XValueType = ChartValueType.Double,
                YValueType = ChartValueType.Double
            };

            chart.Series.Add(series);
            return series;
        }

        private void WireEvents()
        {
            _engine.DevicesOpened += OnDevicesOpened;
            _engine.FrameReady += OnFrameReady;
            _engine.StatusChanged += OnEngineStatus;
            _engine.SettingsApplied += OnSettingsApplied;
            _engine.HardwareApplied += OnHardwareApplied;
            _engine.ReferenceChanged += OnReferenceChanged;
            _engine.Failed += OnEngineFailed;
            _engine.RunStateChanged += (s, e) => UpdateRunStateUi();

            _settingsDebounce.Tick += (s, e) =>
            {
                _settingsDebounce.Stop();
                PushSettings();
            };

            // A rendering hiccup must never surface as an unhandled-exception dialog in the
            // middle of a measurement, so the periodic refresh swallows and reports instead.
            _slowRefresh.Tick += (s, e) =>
            {
                try
                {
                    RefreshSlowViews();
                }
                catch (Exception ex)
                {
                    statusState.Text = L.F("status.refreshFailed", "Display refresh failed: {0}", ex.Message);
                }
            };
            _slowRefresh.Start();

            numMinInterval.ValueChanged += OnSettingChanged;
            numAverage.ValueChanged += OnSettingChanged;

            // The measurement mode is a device setting, so it goes through the stop / apply
            // / restart path rather than the debounced settings push.
            cmbTriggerMode.SelectedIndexChanged += OnTriggerModeChanged;

            // Check-box cells only raise CellValueChanged once the edit is committed, which
            // by default does not happen until the cell loses focus.
            gridDevices.CurrentCellDirtyStateChanged += OnGridDirtyStateChanged;
            gridDevices.CellValueChanged += OnGridCellValueChanged;
            gridDevices.CellEndEdit += OnGridCellEndEdit;
            gridDevices.DataError += (s, e) => { e.ThrowException = false; e.Cancel = true; };

            tabMain.SelectedIndexChanged += (s, e) => RefreshSlowViews();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            RestoreWindowPlacement();
            FitToWorkingArea();

            // Opening the spectrometers takes seconds on real hardware; the splash screen
            // closes again in OnDevicesOpened.
            _splash = new SplashForm();
            _splash.Show(this);
            _splash.Refresh();

            _engine.Start();
            _engine.OpenDevices();
            statusDevice.Text = L.T("status.opening", "Opening spectrometers...");
        }

        /// <summary>
        /// Shrinks the window when it would not fit on the display. The right-hand panel
        /// scrolls, so the controls stay reachable on small or scaled screens.
        /// </summary>
        private void FitToWorkingArea()
        {
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;

            int width = Math.Min(Width, workingArea.Width);
            int height = Math.Min(Height, workingArea.Height);
            if (width == Width && height == Height)
                return;

            Size = new Size(width, height);
            Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2));
        }

        // ---------------------------------------------------------------- run toggle

        private void OnRunToggled(object sender, EventArgs e)
        {
            if (_suppressRunToggle)
                return;

            if (chkRun.Checked)
            {
                string problem = DescribeStartProblem();
                if (problem != null)
                {
                    SetRunToggle(false);
                    statusState.Text = problem;
                    MessageBox.Show(this, problem, L.T("dialog.cannotStart", "Cannot start"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _runRequested = true;
                _frameRate = 0.0;
                _lastFrameTime = DateTime.MinValue;

                MeasurementSettings settings = BuildSettings();
                if (!(_lastReferences != null && _lastReferences.HasWhiteReference))
                    statusState.Text = L.T("status.noWhiteYet",
                        "Running without a white reference - only raw counts are available.");

                _engine.StartMeasurement(settings);
            }
            else
            {
                _runRequested = false;
                _engine.StopMeasurement();
            }

            ApplyRunToggleStyle();
            ApplyGridEnabledState();
        }

        private string DescribeStartProblem()
        {
            if (_devices.Length == 0)
                return L.T("problem.noDevice", "No spectrometer is connected.");

            bool anyEnabled = false;
            for (int i = 0; i < _deviceSettings.Length; i++)
            {
                if (_deviceSettings[i].Enabled)
                {
                    anyEnabled = true;
                    break;
                }
            }

            if (!anyEnabled)
                return L.T("problem.noUse", "Tick the spectrometer to measure in the Use column before starting.");

            return null;
        }

        private void SetRunToggle(bool on)
        {
            _suppressRunToggle = true;
            try { chkRun.Checked = on; }
            finally { _suppressRunToggle = false; }

            _runRequested = on;
            ApplyRunToggleStyle();
        }

        private void ApplyRunToggleStyle()
        {
            bool on = chkRun.Checked;

            chkRun.Text = on
                ? L.T("run.running", "Measurement\nRUNNING\n(click to stop)").Replace("\n", "\r\n")
                : L.T("run.start", "Measurement\nSTART").Replace("\n", "\r\n");
            chkRun.ForeColor = on ? Color.Black : SystemColors.ControlText;

            UpdateShellState();
        }

        private void UpdateRunStateUi()
        {
            bool running = _engine.IsRunning;

            // Only forced when the engine ended up somewhere the user did not ask for,
            // so the toggle is not reset while a start command is still in flight.
            if (running != _runRequested)
                SetRunToggle(running);

            bool hasDevices = _devices.Length > 0;
            chkRun.Enabled = hasDevices;
            btnCaptureDark.Enabled = hasDevices;
            btnCaptureReference.Enabled = hasDevices;

            ApplyGridEnabledState();

            statusState.Text = running
                ? L.T("status.running", "Measurement running.")
                : L.T("status.idle", "Idle.");

            UpdateShellState();
        }

        // ---------------------------------------------------------------- teardown

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _slowRefresh.Stop();
            _settingsDebounce.Stop();

            try
            {
                SaveUserSettings();
            }
            catch (Exception ex)
            {
                // Closing must not be blocked by a settings file; the reason goes to the log.
                Program.Log(ex, "saving user settings");
            }

            // Closes the GPIO / ExternalTriggerDelay controllers, then
            // closeAllSpectrometers() and Dispose() on the wrapper.
            _engine.Dispose();

            base.OnFormClosing(e);
        }
    }
}
