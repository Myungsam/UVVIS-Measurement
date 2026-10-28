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
            string version = Application.ProductVersion;
            lblTitle.Text = "UV-VIS Measurement v" + version;
            Text = "Ocean Optics - UV-VIS Measurement v" + version +
                   (SpectrometerBackend.IsSimulated ? "   [DEMO MODE - simulated spectrometers]" : string.Empty);
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
            statusState.Text = ChartTitleOf(chart) + " axes:  " + chart.DescribeScales();
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
                    statusState.Text = "Display refresh failed: " + ex.Message;
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

            _engine.Start();
            _engine.OpenDevices();
            statusDevice.Text = "Opening spectrometers...";
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

        // ---------------------------------------------------------------- device table

        private void OnDevicesOpened(object sender, DevicesOpenedEventArgs e)
        {
            _devices = e.Devices ?? new DeviceInfo[0];
            bool any = _devices.Length > 0;

            BuildDeviceSettings();
            RebuildDeviceGrid();
            PruneGpioState();

            // Built from the same device list, so a spectrometer that was unplugged loses
            // its section and no control is left holding an index that no longer exists.
            _hardware.Build(_devices, _deviceSettings);

            UpdateRoleDerivedUi();

            if (!any)
            {
                statusDevice.Text = "No spectrometer detected (" + e.DriverName + ")";
                statusDevice.ToolTipText = null;
            }
            else
            {
                // One device's full description already fills the status bar, so as soon as
                // a second one is connected only the short labels are shown and the details
                // move into the tool tip.
                statusDevice.Text = string.Join("   |   ", Array.ConvertAll(_devices,
                    d => _devices.Length == 1 ? d.ToString() : d.ShortLabel)) +
                    "   |   " + e.DriverName;
                statusDevice.ToolTipText = string.Join(Environment.NewLine,
                    Array.ConvertAll(_devices, d => d.ToString()));
            }

            chkRun.Enabled = any;
            btnCaptureDark.Enabled = any;
            btnCaptureReference.Enabled = any;

            if (any)
                PushSettings();
        }

        /// <summary>
        /// Seeds the table: the first spectrometer is the one being measured. Beam roles
        /// are only meaningful once a second spectrometer exists, so with one device they
        /// stay unassigned and their check boxes are disabled.
        /// </summary>
        private void BuildDeviceSettings()
        {
            _deviceSettings = new DeviceSettings[_devices.Length];
            bool pairable = _devices.Length >= 2;

            for (int i = 0; i < _devices.Length; i++)
            {
                DeviceInfo info = _devices[i];
                var settings = new DeviceSettings
                {
                    Index = info.Index,
                    Enabled = i == 0,
                    IsProbe = pairable && i == 0,
                    IsReference = pairable && i == 1,

                    // Beyond two spectrometers the first two are the default pair and the
                    // operator can move it; at two or fewer SyncAutoCorrectionPair below
                    // derives it instead.
                    InCorrectionPair = i < 2
                };

                // Values remembered for this physical spectrometer win over the defaults,
                // but are clamped again: the file may predate a firmware or model change.
                DeviceUserSettings saved = _userSettings.FindDevice(info.Identity);
                settings.IntegrationMs = IntegrationLimitsFor(i).Clamp(saved == null ? 1.0 : saved.IntegrationMs);
                settings.TriggerDelayUs = TriggerDelayLimitsFor(i).Clamp(saved == null ? 50.0 : saved.TriggerDelayUs);

                _deviceSettings[i] = settings;
            }

            SyncAutoCorrectionPair();
        }

        /// <summary>
        /// With two spectrometers or fewer the correction pair is not something to pick -
        /// there is only one possible pair - so it is derived instead of edited, and the
        /// Corr. column is display-only.
        ///
        /// It only exists while exactly one probe is assigned. Marking both devices as
        /// probes means two samples measured against their own references and leaves
        /// nothing watching the lamp, so the Corr. ticks clear themselves rather than
        /// advertising a pair that cannot be used. They come back when the second probe
        /// is released.
        ///
        /// From three spectrometers upwards the pair is the operator's choice and is left
        /// exactly as they set it; the state line below the table explains when it does
        /// not match the assigned roles.
        /// </summary>
        private void SyncAutoCorrectionPair()
        {
            if (_deviceSettings.Length > 2)
                return;

            bool paired = CountRole(true) == 1;
            for (int i = 0; i < _deviceSettings.Length; i++)
                _deviceSettings[i].InCorrectionPair = paired;
        }

        /// <summary>
        /// Drops the remembered pin states of spectrometers that are no longer connected,
        /// so an unplugged device leaves nothing behind that could be reported as current.
        /// A device that comes back gets its state read again when its section is built.
        /// </summary>
        private void PruneGpioState()
        {
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (DeviceInfo device in _devices)
                live.Add(device.Identity);

            var stale = new List<string>();
            foreach (string identity in _gpioByDevice.Keys)
            {
                if (!live.Contains(identity))
                    stale.Add(identity);
            }

            foreach (string identity in stale)
                _gpioByDevice.Remove(identity);
        }

        /// <summary>Beam roles need two spectrometers to mean anything.</summary>
        private bool RolesAvailable
        {
            get { return _devices.Length >= 2; }
        }

        private void RebuildDeviceGrid()
        {
            _suppressGridEvents = true;
            try
            {
                gridDevices.Rows.Clear();

                for (int i = 0; i < _deviceSettings.Length; i++)
                {
                    DeviceSettings settings = _deviceSettings[i];
                    DeviceInfo info = _devices[i];

                    int rowIndex = gridDevices.Rows.Add(
                        settings.Enabled,
                        settings.Index,
                        info.ModelLabel,
                        FormatNumber(settings.IntegrationMs, 3),
                        info.TriggerDelaySupported ? FormatNumber(settings.TriggerDelayUs, 1) : "n/a",
                        settings.IsProbe,
                        settings.IsReference,
                        settings.InCorrectionPair);

                    DataGridViewRow row = gridDevices.Rows[rowIndex];
                    row.Cells[colModel.Index].ToolTipText = info.ToString();
                    row.Cells[colIntegration.Index].ToolTipText = string.Format(CultureInfo.InvariantCulture,
                        "{0:F3} .. {1:F3} ms", IntegrationLimitsFor(i).Minimum, IntegrationLimitsFor(i).Maximum);

                    if (!info.TriggerDelaySupported)
                    {
                        row.Cells[colTriggerDelay.Index].ToolTipText =
                            "This spectrometer does not support an external trigger delay.";
                    }
                }
            }
            finally
            {
                _suppressGridEvents = false;
            }

            AdjustGridHeight();

            // Nothing should look picked before the user has touched the table.
            if (!gridDevices.Focused)
                gridDevices.CurrentCell = null;

            ApplyGridEnabledState();
        }

        /// <summary>
        /// Single place that decides which cells are editable, so the role locking and the
        /// running-measurement freeze cannot overwrite each other.
        /// </summary>
        private void ApplyGridEnabledState()
        {
            if (gridDevices.RowCount == 0)
                return;

            bool running = _runRequested || _engine.IsRunning;
            bool roles = RolesAvailable;

            int useRow = FindCheckedRow(colUse.Index);
            int referenceRow = FindCheckedRow(colReference.Index);

            // Two probes mean two independent samples, so no reference beam can exist and
            // that whole column goes dead rather than silently doing nothing.
            int probeCount = CountRole(true);
            bool referenceAvailable = roles && probeCount <= 1;

            // The correction pair is only a choice from three spectrometers upwards; below
            // that every device is in it and the column is display-only.
            bool pairSelectable = _deviceSettings.Length >= 3;
            int pairMembers = CountCorrectionPair();

            _suppressGridEvents = true;
            try
            {
                for (int row = 0; row < gridDevices.RowCount; row++)
                {
                    // Only one spectrometer is measured at a time, so once a row owns the
                    // Use tick the others are locked out until it is released. It is also
                    // frozen while acquiring - switching device mid-run would leave the
                    // chart and the timed series holding two different detectors' data.
                    SetCellLocked(row, colUse.Index, running || (useRow >= 0 && row != useRow));

                    // Beam roles need a second spectrometer; with one device they are dead
                    // controls, so they are disabled outright rather than silently ignored.
                    // Probe itself is multi-select, so it is never locked by another row.
                    SetCellLocked(row, colProbe.Index, !roles || running);

                    SetCellLocked(row, colReference.Index,
                        !referenceAvailable || running || (referenceRow >= 0 && row != referenceRow));

                    bool inPair = row < _deviceSettings.Length && _deviceSettings[row].InCorrectionPair;
                    SetCellLocked(row, colCorrection.Index,
                        !pairSelectable || running || (pairMembers >= 2 && !inPair));

                    bool delaySupported = row < _devices.Length && _devices[row].TriggerDelaySupported;
                    SetCellLocked(row, colTriggerDelay.Index, !delaySupported);
                }
            }
            finally
            {
                _suppressGridEvents = false;
            }
        }

        private int CountCorrectionPair()
        {
            int count = 0;
            for (int i = 0; i < _deviceSettings.Length; i++)
                if (_deviceSettings[i].InCorrectionPair)
                    count++;
            return count;
        }

        /// <summary>Grey, non-editable cell for "this choice is not available right now".</summary>
        private int _baseGridHeight;
        private int _baseParametersHeight;
        private readonly Dictionary<Control, int> _baseTops = new Dictionary<Control, int>();

        // The GPIO tables on the Hardware Settings tab use the same tints, so they live in
        // one place rather than being matched by eye in two files.
        private static readonly Color LockedCellColor = GridColors.Locked;
        private static readonly Color ReadOnlyCellColor = GridColors.ReadOnly;

        private void SetCellLocked(int row, int column, bool locked)
        {
            DataGridViewCell cell = gridDevices.Rows[row].Cells[column];

            Color background = locked ? LockedCellColor : SystemColors.Window;
            Color foreground = locked ? SystemColors.GrayText : SystemColors.ControlText;

            cell.ReadOnly = locked;
            cell.Style.BackColor = background;
            cell.Style.ForeColor = foreground;
            cell.Style.SelectionBackColor = background;
            cell.Style.SelectionForeColor = foreground;
        }

        private int FindCheckedRow(int column)
        {
            for (int row = 0; row < gridDevices.RowCount; row++)
            {
                if (ToBool(gridDevices.Rows[row].Cells[column].Value))
                    return row;
            }
            return -1;
        }

        private void OnGridDirtyStateChanged(object sender, EventArgs e)
        {
            if (gridDevices.IsCurrentCellDirty && gridDevices.CurrentCell is DataGridViewCheckBoxCell)
                gridDevices.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void OnGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressGridEvents || e.RowIndex < 0 || e.RowIndex >= _deviceSettings.Length)
                return;

            if (e.ColumnIndex == colUse.Index)
            {
                ApplyExclusiveUse(e.RowIndex);
            }
            else if (e.ColumnIndex == colProbe.Index)
            {
                ApplyProbeChange(e.RowIndex);
            }
            else if (e.ColumnIndex == colReference.Index)
            {
                ApplyReferenceChange(e.RowIndex);
            }
            else if (e.ColumnIndex == colCorrection.Index)
            {
                ApplyCorrectionPairChange(e.RowIndex);
            }
            else
            {
                return;
            }

            UpdateRoleDerivedUi();
            QueueSettingsPush();
        }

        /// <summary>
        /// Numeric cells are validated here rather than in CellValueChanged: the editing
        /// control is guaranteed to be gone, so rewriting the cell with the clamped value
        /// cannot be undone by the commit that follows.
        /// </summary>
        private void OnGridCellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (_suppressGridEvents || e.RowIndex < 0 || e.RowIndex >= _deviceSettings.Length)
                return;

            DeviceSettings settings = _deviceSettings[e.RowIndex];

            if (e.ColumnIndex == colIntegration.Index)
            {
                settings.IntegrationMs = CommitNumericCell(e.RowIndex, colIntegration.Index,
                    settings.IntegrationMs, IntegrationLimitsFor(e.RowIndex), 3);
            }
            else if (e.ColumnIndex == colTriggerDelay.Index)
            {
                if (e.RowIndex < _devices.Length && !_devices[e.RowIndex].TriggerDelaySupported)
                    return;

                settings.TriggerDelayUs = CommitNumericCell(e.RowIndex, colTriggerDelay.Index,
                    settings.TriggerDelayUs, TriggerDelayLimitsFor(e.RowIndex), 1);
            }
            else
            {
                return;
            }

            QueueSettingsPush();
        }

        /// <summary>Exactly one spectrometer is measured, so ticking one clears the rest.</summary>
        private void ApplyExclusiveUse(int row)
        {
            bool selected = ToBool(gridDevices.Rows[row].Cells[colUse.Index].Value);

            for (int i = 0; i < _deviceSettings.Length; i++)
                _deviceSettings[i].Enabled = selected && i == row;

            MirrorColumn(colUse.Index);
            ApplyGridEnabledState();
        }

        /// <summary>
        /// Probe is multi-select: marking a second spectrometer as a probe means two
        /// samples measured in turn, each against its own dark and white reference. That
        /// leaves nothing watching the lamp, so the reference beam is dropped and its
        /// column goes dead.
        ///
        /// While there is still exactly one probe the old convenience holds: the other
        /// member of the correction pair becomes the reference automatically.
        /// </summary>
        private void ApplyProbeChange(int row)
        {
            bool assigned = ToBool(gridDevices.Rows[row].Cells[colProbe.Index].Value);

            _deviceSettings[row].IsProbe = assigned;
            if (assigned)
                _deviceSettings[row].IsReference = false;

            int probes = CountRole(true);

            if (probes == 1 && CountRole(false) == 0)
            {
                // Sole probe: hand the reference beam to the other member of the pair.
                int partner = CorrectionPartnerOf(IndexOfRole(true));
                if (partner >= 0)
                    _deviceSettings[partner].IsReference = true;
            }
            else if (probes != 1)
            {
                // Two probes leave nothing to watch the lamp with, and releasing the last
                // probe releases the reference with it - the table never shows half an
                // assignment.
                ClearReferenceRole();
            }

            FinishRoleChange();
        }

        /// <summary>
        /// Reference is single-select. Assigning it when no probe exists yet puts the probe
        /// on the other member of the correction pair, so either check box can start the
        /// assignment.
        /// </summary>
        private void ApplyReferenceChange(int row)
        {
            bool assigned = ToBool(gridDevices.Rows[row].Cells[colReference.Index].Value);

            for (int i = 0; i < _deviceSettings.Length; i++)
                _deviceSettings[i].IsReference = assigned && i == row;

            if (assigned)
            {
                _deviceSettings[row].IsProbe = false;

                // Either check box can start the assignment: with no probe left, the other
                // member of the pair takes that role.
                if (CountRole(true) == 0)
                {
                    int partner = CorrectionPartnerOf(row);
                    if (partner >= 0)
                        _deviceSettings[partner].IsProbe = true;
                }
            }

            FinishRoleChange();
        }

        private void ClearReferenceRole()
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
                _deviceSettings[i].IsReference = false;
        }

        /// <summary>Mirrors the role columns back into the table and re-locks the cells.</summary>
        private void FinishRoleChange()
        {
            SyncAutoCorrectionPair();

            MirrorColumn(colProbe.Index);
            MirrorColumn(colReference.Index);
            MirrorColumn(colCorrection.Index);
            ApplyGridEnabledState();
        }

        /// <summary>
        /// Exactly two spectrometers may sit in the correction pair. Once two are ticked
        /// the rest are locked, which is the same rule the reference column follows.
        /// </summary>
        private void ApplyCorrectionPairChange(int row)
        {
            _deviceSettings[row].InCorrectionPair =
                ToBool(gridDevices.Rows[row].Cells[colCorrection.Index].Value);

            MirrorColumn(colCorrection.Index);
            ApplyGridEnabledState();
        }

        /// <summary>The other member of the correction pair, or -1 when it is not a clean pair.</summary>
        private int CorrectionPartnerOf(int row)
        {
            if (row < 0 || row >= _deviceSettings.Length)
                return -1;

            // With two spectrometers the pair is not a choice, so the partner is simply the
            // other one - the Corr. ticks themselves follow from the roles, not the reverse.
            if (_deviceSettings.Length == 2)
                return row == 0 ? 1 : 0;

            int partner = -1;
            int members = 0;

            for (int i = 0; i < _deviceSettings.Length; i++)
            {
                if (!_deviceSettings[i].InCorrectionPair)
                    continue;

                members++;
                if (i != row)
                    partner = i;
            }

            // Only a pair that has exactly two members and contains this row can name a partner.
            return members == 2 && _deviceSettings[row].InCorrectionPair ? partner : -1;
        }

        private int CountRole(bool probe)
        {
            int count = 0;
            for (int i = 0; i < _deviceSettings.Length; i++)
                if (probe ? _deviceSettings[i].IsProbe : _deviceSettings[i].IsReference)
                    count++;
            return count;
        }

        private int IndexOfRole(bool probe)
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
                if (probe ? _deviceSettings[i].IsProbe : _deviceSettings[i].IsReference)
                    return i;
            return -1;
        }

        /// <summary>Writes the model back into one check-box column without re-entering the handlers.</summary>
        private void MirrorColumn(int column)
        {
            _suppressGridEvents = true;
            try
            {
                for (int i = 0; i < gridDevices.RowCount && i < _deviceSettings.Length; i++)
                {
                    DeviceSettings settings = _deviceSettings[i];

                    bool value;
                    if (column == colUse.Index) value = settings.Enabled;
                    else if (column == colProbe.Index) value = settings.IsProbe;
                    else if (column == colCorrection.Index) value = settings.InCorrectionPair;
                    else value = settings.IsReference;

                    gridDevices.Rows[i].Cells[column].Value = value;
                }
            }
            finally
            {
                _suppressGridEvents = false;
            }
        }

        private double CommitNumericCell(int row, int column, double previous, Limits limits, int decimals)
        {
            DataGridViewCell cell = gridDevices.Rows[row].Cells[column];
            string text = cell.Value == null ? string.Empty : cell.Value.ToString();

            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                value = previous;
            }

            value = limits.Clamp(value);

            _suppressGridEvents = true;
            try { cell.Value = FormatNumber(value, decimals); }
            finally { _suppressGridEvents = false; }

            return value;
        }

        private Limits IntegrationLimitsFor(int row)
        {
            double minimum = 0.001;
            double maximum = 10000.0;

            if (row >= 0 && row < _devices.Length)
            {
                DeviceInfo device = _devices[row];
                if (device.MinimumIntegrationTimeUs > 0)
                    minimum = Math.Max(0.001, device.MinimumIntegrationTimeUs / 1000.0);
                if (device.MaximumIntegrationTimeUs > 0)
                    maximum = device.MaximumIntegrationTimeUs / 1000.0;
            }

            return new Limits(minimum, Math.Max(minimum, maximum));
        }

        private Limits TriggerDelayLimitsFor(int row)
        {
            if (row < 0 || row >= _devices.Length)
                return new Limits(0.0, 1e9);

            DeviceInfo device = _devices[row];
            if (!device.TriggerDelaySupported)
                return new Limits(0.0, 0.0);

            return new Limits(0.0, Math.Min(1e9, device.TriggerDelayMaximumCounts * device.MicrosecondsPerCount));
        }

        private static bool ToBool(object value)
        {
            return value is bool && (bool)value;
        }

        private static string FormatNumber(double value, int decimals)
        {
            return value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        /// <summary>Index of the spectrometer ticked under "Use", or -1.</summary>
        private int ActiveIndex()
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
            {
                if (_deviceSettings[i].Enabled)
                    return _deviceSettings[i].Index;
            }
            return -1;
        }

        private int ProbeIndex()
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
            {
                if (_deviceSettings[i].IsProbe)
                    return _deviceSettings[i].Index;
            }
            return -1;
        }

        private int ReferenceIndex()
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
            {
                if (_deviceSettings[i].IsReference)
                    return _deviceSettings[i].Index;
            }
            return -1;
        }

        /// <summary>Refreshes everything that follows from the Use tick and the beam roles.</summary>
        private void UpdateRoleDerivedUi()
        {
            int activeIndex = ActiveIndex();
            int probeIndex = ProbeIndex();
            int referenceIndex = ReferenceIndex();

            if (activeIndex < 0)
            {
                lblReferenceModeState.Text = "none selected";
                lblReferenceModeState.BackColor = Color.Khaki;
            }
            else if (!RolesAvailable)
            {
                lblReferenceModeState.Text = "[" + activeIndex + "] single";
                lblReferenceModeState.BackColor = SystemColors.Control;
            }
            else if (probeIndex >= 0 && referenceIndex >= 0)
            {
                lblReferenceModeState.Text = string.Format(CultureInfo.InvariantCulture,
                    "[{0}] {1}", activeIndex,
                    activeIndex == probeIndex ? "probe" : (activeIndex == referenceIndex ? "reference" : "unassigned"));
                lblReferenceModeState.BackColor = activeIndex == probeIndex
                    ? Color.MediumTurquoise
                    : SystemColors.Control;
            }
            else
            {
                lblReferenceModeState.Text = "[" + activeIndex + "] no beam assigned";
                lblReferenceModeState.BackColor = Color.Khaki;
            }

            string suffix = activeIndex >= 0 ? " [" + activeIndex + "]" : string.Empty;
            btnCaptureDark.Text = "Capture Dark" + suffix;
            btnCaptureReference.Text = "Capture White Ref." + suffix;
            btnClearReference.Text = "Clear Dark/White" + suffix;

            if (_lastReferences != null)
                lblReferenceState.Text = _lastReferences.Describe();

            // The roles and the measured device are shown on the Hardware Settings tab too,
            // and the trigger combo follows whichever spectrometer is now under Use.
            _hardware.ShowRoles(_deviceSettings);
            ShowActiveTriggerMode();

            UpdateDriftUi(_lastFrame);
            UpdateActiveDeviceUi();
        }

        /// <summary>Re-derives the limits that depend on which spectrometer is being measured.</summary>
        private void UpdateActiveDeviceUi()
        {
            int activeIndex = ActiveIndex();
            if (activeIndex < 0 || activeIndex >= _devices.Length)
                return;

            DeviceInfo device = _devices[activeIndex];
            bool suppressed = _suppressSettingsPush;
            _suppressSettingsPush = true;

            if (device.Wavelengths.Length > 1)
            {
                numTraceWavelength.Minimum = (decimal)Math.Floor(device.MinWavelength);
                numTraceWavelength.Maximum = (decimal)Math.Ceiling(device.MaxWavelength);
                numTraceWavelength.Value = Clamp(numTraceWavelength.Value,
                    numTraceWavelength.Minimum, numTraceWavelength.Maximum);
            }

            _suppressSettingsPush = suppressed;
        }

        // ---------------------------------------------------------------- engine events

        private void OnFrameReady(object sender, FrameEventArgs e)
        {
            SpectrumFrame frame = e.Frame;
            _lastFrame = frame;

            DateTime now = DateTime.UtcNow;
            if (_lastFrameTime != DateTime.MinValue)
            {
                double seconds = (now - _lastFrameTime).TotalSeconds;
                if (seconds > 0.0)
                {
                    double instantaneous = 1.0 / seconds;
                    _frameRate = _frameRate <= 0.0 ? instantaneous : _frameRate * 0.7 + instantaneous * 0.3;
                }
            }
            _lastFrameTime = now;

            if (_accumulateTimed)
            {
                _timedStore.Add(frame);
                _timedDirty = true;
            }

            UpdateSpectrumCharts(frame);
            UpdateFrameStatus(frame);
            UpdateDriftUi(frame);
        }

        private void OnEngineStatus(object sender, StatusEventArgs e)
        {
            statusState.Text = e.Message;
        }

        private void OnSettingsApplied(object sender, SettingsAppliedEventArgs e)
        {
            _appliedDelaysText = e.DescribeTriggerDelays();
            lblAppliedDelay.Text = _appliedDelaysText;
        }

        private void OnReferenceChanged(object sender, ReferenceChangedEventArgs e)
        {
            _lastReferences = e.Snapshot;
            lblReferenceState.Text = e.Snapshot.Describe();

            // Capturing the reference channel's dark/white is what arms the correction.
            UpdateDriftUi(_lastFrame);
        }

        private void OnEngineFailed(object sender, EngineFailedEventArgs e)
        {
            string message = e.Exception.Message;
            if (!string.IsNullOrWhiteSpace(e.DriverMessage))
                message += Environment.NewLine + Environment.NewLine + "OmniDriver: " + e.DriverMessage;

            statusState.Text = "Error: " + e.Exception.Message;

            // The engine may never have reached the running state, in which case no
            // RunStateChanged arrives to pop the toggle back out.
            SetRunToggle(false);
            ApplyGridEnabledState();

            MessageBox.Show(this, message, "Acquisition error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // ---------------------------------------------------------------- charts

        private void UpdateSpectrumCharts(SpectrumFrame frame)
        {
            if (frame.Wavelengths == null)
                return;

            if (tabMain.SelectedTab == tabPageAbsorbanceFluorescence)
            {
                if (tabInner.SelectedTab == tabPageAbsorption)
                {
                    chartAbsorption.BeginDataUpdate();
                    try
                    {
                        Plot(chartAbsorption.Series["Absorbance"], frame.Wavelengths, frame.Absorbance);
                    }
                    finally { chartAbsorption.EndDataUpdate(); }
                }
                else
                {
                    chartFluorescence.BeginDataUpdate();
                    try
                    {
                        Plot(chartFluorescence.Series["Intensity"], frame.Wavelengths, frame.Counts);
                    }
                    finally { chartFluorescence.EndDataUpdate(); }
                }
            }
            else if (tabMain.SelectedTab == tabPageProbe)
            {
                chartProbe.BeginDataUpdate();
                try
                {
                    Plot(chartProbe.Series["Sample"], frame.Wavelengths, frame.Counts);
                    Plot(chartProbe.Series["Dark"], frame.Wavelengths, frame.Dark);
                    Plot(chartProbe.Series["White reference"], frame.Wavelengths, frame.WhiteReference);
                }
                finally { chartProbe.EndDataUpdate(); }
            }
        }

        /// <summary>
        /// Replaces the series data. NaN samples become empty points so the line breaks.
        ///
        /// The point count almost never changes between frames, so the existing DataPoints
        /// are overwritten in place. Clearing and re-adding instead would allocate a couple
        /// of thousand objects per series per frame, which lengthens the window in which the
        /// chart cannot be painted (see <see cref="SpectrumChart"/>) and churns the GC.
        /// </summary>
        private static void Plot(Series series, double[] x, double[] y)
        {
            DataPointCollection points = series.Points;
            int count = y == null ? 0 : Math.Min(x.Length, y.Length);

            if (count == 0)
            {
                if (points.Count > 0)
                    points.Clear();
                return;
            }

            points.SuspendUpdates();
            try
            {
                if (points.Count != count)
                {
                    points.Clear();
                    for (int i = 0; i < count; i++)
                        points.AddXY(x[i], 0.0);
                }

                for (int i = 0; i < count; i++)
                {
                    DataPoint point = points[i];
                    double value = y[i];
                    bool empty = double.IsNaN(value) || double.IsInfinity(value);

                    point.XValue = x[i];
                    point.YValues[0] = empty ? 0.0 : value;

                    // Assigning IsEmpty writes through to the point's custom properties, so
                    // it is only touched when it actually has to change.
                    if (point.IsEmpty != empty)
                        point.IsEmpty = empty;
                }
            }
            finally
            {
                points.ResumeUpdates();
            }
        }

        /// <summary>
        /// The timed chart and the 2D map are expensive to rebuild, so they refresh on a
        /// timer and only while their tab is actually on screen.
        /// </summary>
        private void RefreshSlowViews()
        {
            if (tabMain.SelectedTab == tabPageTimed)
            {
                RebuildTimedChart(_timedStore.Snapshot());
                _timedDirty = false;
            }
            else if (tabMain.SelectedTab == tabPageTimed2D)
            {
                if (_timedDirty || !_heatmapSeeded)
                {
                    _heatmapSeeded = true;
                    heatmap.SetSource(_timedStore.Snapshot());
                    _timedDirty = false;
                }
                UpdateHeatInfo();
            }

            statusDatasets.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} datasets ({1:F1} MB){2}",
                _timedStore.Count, _timedStore.ApproximateBytes / (1024.0 * 1024.0),
                _accumulateTimed ? " - recording" : string.Empty);
        }

        private void RebuildTimedChart(TimedSeriesSnapshot series)
        {
            chartTimed.BeginDataUpdate();
            try
            {
                RebuildTimedChartCore(series);
            }
            finally
            {
                chartTimed.EndDataUpdate();
            }
        }

        private void RebuildTimedChartCore(TimedSeriesSnapshot series)
        {
            chartTimed.Series.Clear();

            if (series.IsEmpty || _traceWavelengths.Count == 0)
            {
                lblTraceInfo.Text = _traceWavelengths.Count == 0
                    ? "Add one or more wavelengths to plot."
                    : "No timed datasets yet.";
                return;
            }

            double[] wavelengths = series.Wavelengths;

            for (int i = 0; i < _traceWavelengths.Count; i++)
            {
                int pixel = SpectrumMath.NearestIndex(wavelengths, _traceWavelengths[i]);
                if (pixel < 0)
                    continue;

                series.FillTrace(pixel, _traceTimeBuffer, _traceValueBuffer);

                string name = string.Format(CultureInfo.InvariantCulture, "{0:F2} nm", wavelengths[pixel]);
                Series trace = AddSeries(chartTimed, name, TraceColors[i % TraceColors.Length]);

                trace.Points.SuspendUpdates();
                try
                {
                    for (int j = 0; j < _traceTimeBuffer.Count; j++)
                    {
                        double value = _traceValueBuffer[j];
                        if (double.IsNaN(value) || double.IsInfinity(value))
                        {
                            int index = trace.Points.AddXY(_traceTimeBuffer[j], 0.0);
                            trace.Points[index].IsEmpty = true;
                        }
                        else
                        {
                            trace.Points.AddXY(_traceTimeBuffer[j], value);
                        }
                    }
                }
                finally
                {
                    trace.Points.ResumeUpdates();
                }
            }

            lblTraceInfo.Text = string.Format(CultureInfo.InvariantCulture,
                "{0} record(s), {1} trace(s)", series.Count, _traceWavelengths.Count);
        }

        private void UpdateHeatInfo()
        {
            lblHeatInfo.Text = _timedStore.IsEmpty
                ? "No timed datasets yet."
                : string.Format(CultureInfo.InvariantCulture,
                    "{0} records x {1} pixels   scale {2:F3} .. {3:F3} OD   [{4}]",
                    _timedStore.Count,
                    _timedStore.Wavelengths == null ? 0 : _timedStore.Wavelengths.Length,
                    heatmap.ScaleMinimum, heatmap.ScaleMaximum,
                    heatmap.DescribeScales());
        }

        private void UpdateFrameStatus(SpectrumFrame frame)
        {
            string saturation = frame.Saturated ? "  SATURATED" : string.Empty;

            statusRate.Text = string.Format(CultureInfo.InvariantCulture,
                "[{0}]  {1:F1} fps   t = {2:F2} s{3}",
                frame.DeviceIndex, _frameRate, frame.ElapsedSeconds, saturation);
            statusRate.ForeColor = string.IsNullOrEmpty(saturation) ? SystemColors.ControlText : Color.Firebrick;
        }

        // ---------------------------------------------------------------- settings plumbing

        private void OnSettingChanged(object sender, EventArgs e)
        {
            if (_suppressSettingsPush)
                return;

            QueueSettingsPush();
        }

        private void QueueSettingsPush()
        {
            _settingsDebounce.Stop();
            _settingsDebounce.Start();
        }

        // ------------------------------------------------------- hardware configuration

        /// <summary>
        /// The trigger combo on the parameter panel edits the measurement mode of the
        /// spectrometer being measured. It is the same setting the Hardware Settings tab
        /// shows for that device, so it takes the same route to the driver.
        /// </summary>
        private void OnTriggerModeChanged(object sender, EventArgs e)
        {
            if (_suppressSettingsPush)
                return;

            int row = ActiveRow();
            if (row < 0)
            {
                statusState.Text = "Tick the spectrometer to configure in the Use column first.";
                ShowActiveTriggerMode();
                return;
            }

            var mode = (TriggerMode)Math.Max(0, cmbTriggerMode.SelectedIndex);
            if (mode == _deviceSettings[row].TriggerMode)
                return;

            RequestHardwareChange(new HardwareChange
            {
                DeviceIndex = _deviceSettings[row].Index,
                Kind = HardwareChangeKind.MeasurementMode,
                Mode = mode
            });
        }

        private void OnHardwareChangeRequested(object sender, HardwareChangeRequestedEventArgs e)
        {
            RequestHardwareChange(e.Change);
        }

        /// <summary>
        /// Hands one device setting to the acquisition thread. Nothing is written into
        /// <see cref="_deviceSettings"/> here: the model only moves once the driver has
        /// confirmed the change, so a refused setting cannot end up in a saved CSV header
        /// as though it had been in force.
        /// </summary>
        private void RequestHardwareChange(HardwareChange change)
        {
            if (change == null)
                return;

            if (_devices.Length == 0)
            {
                statusState.Text = "No spectrometer is connected.";
                return;
            }

            // A state read is not a change and should not narrate itself.
            if (change.Kind != HardwareChangeKind.ReadGpio)
                statusState.Text = "Applying " + change.Describe() + "...";

            _engine.ApplyHardware(change);
        }

        /// <summary>
        /// The driver has answered. Only now does the UI follow, and only for what it
        /// actually accepted.
        /// </summary>
        private void OnHardwareApplied(object sender, HardwareAppliedEventArgs e)
        {
            if (e.Applied && e.Change.Kind == HardwareChangeKind.MeasurementMode)
            {
                DeviceSettings device = SettingsFor(e.Change.DeviceIndex);
                if (device != null)
                    device.TriggerMode = e.Change.Mode;
            }

            // Filed under the spectrometer's own identity so the CSV header describes the
            // right device even after the list has been renumbered.
            if (e.GpioChannels != null && !string.IsNullOrEmpty(e.Change.DeviceIdentity))
                _gpioByDevice[e.Change.DeviceIdentity] = e.GpioChannels;

            _hardware.ApplyResult(e);
            ShowActiveTriggerMode();

            if (e.Change.Kind == HardwareChangeKind.ReadGpio)
                return;

            statusState.Text = e.Describe();

            if (!e.Applied && !e.PreviousRestored)
            {
                // The device is in a state nobody asked for and the acquisition is stopped;
                // that is worth interrupting for.
                MessageBox.Show(this, e.Describe(), "Hardware setting failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Puts the trigger combo on the mode of the spectrometer under "Use".</summary>
        private void ShowActiveTriggerMode()
        {
            int row = ActiveRow();
            bool suppressed = _suppressSettingsPush;
            _suppressSettingsPush = true;
            try
            {
                cmbTriggerMode.SelectedIndex = row < 0 ? 0 : (int)_deviceSettings[row].TriggerMode;
            }
            finally
            {
                _suppressSettingsPush = suppressed;
            }
        }

        private int ActiveRow()
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
                if (_deviceSettings[i].Enabled)
                    return i;

            return -1;
        }

        private DeviceSettings SettingsFor(int deviceIndex)
        {
            for (int i = 0; i < _deviceSettings.Length; i++)
                if (_deviceSettings[i].Index == deviceIndex)
                    return _deviceSettings[i];

            return null;
        }

        private MeasurementSettings BuildSettings()
        {
            // Shared by reference on purpose: ApplySettings/StartMeasurement deep-copy the
            // array before it crosses to the acquisition thread.
            _settings.Devices = _deviceSettings;
            _settings.ScansToAverage = (int)numAverage.Value;
            _settings.MinIntervalMs = (int)numMinInterval.Value;
            // TriggerMode is not set here: it lives on the device row and only changes once
            // the driver has confirmed it (see OnHardwareApplied). GPIO is not here either -
            // pins are configured per spectrometer from the Hardware Settings tab.
            return _settings;
        }

        /// <summary>
        /// Explains what the live reference correction is doing, or why it is not running.
        /// There is no switch for it - it follows from the table - so this label is the
        /// only thing telling the operator which absorbance formula is in force.
        /// Called both when the configuration changes (frame == null) and per frame.
        /// </summary>
        private void UpdateDriftUi(SpectrumFrame frame)
        {
            string blocker = BuildSettings().DescribeDriftBlocker(_devices.Length);

            if (blocker != null)
            {
                lblDriftState.Text = "Live reference correction: " + blocker;
                lblDriftState.ForeColor = SystemColors.GrayText;
                return;
            }

            if (frame != null && frame.DriftCorrected)
            {
                lblDriftState.Text = string.Format(CultureInfo.InvariantCulture,
                    "Live reference correction active: lamp x{0:F4} from [{1}]",
                    frame.DriftMean, frame.ReferenceDeviceIndex);
                lblDriftState.ForeColor = Color.DarkGreen;
                return;
            }

            lblDriftState.Text = "Live reference correction: armed - capture dark/white on [" +
                                 ReferenceIndex() + "] too";
            lblDriftState.ForeColor = SystemColors.GrayText;
        }

        private void PushSettings()
        {
            if (_devices.Length == 0)
                return;

            _engine.ApplySettings(BuildSettings());
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
                    MessageBox.Show(this, problem, "Cannot start",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _runRequested = true;
                _frameRate = 0.0;
                _lastFrameTime = DateTime.MinValue;

                MeasurementSettings settings = BuildSettings();
                if (!(_lastReferences != null && _lastReferences.HasWhiteReference))
                    statusState.Text = "Running without a white reference - only raw counts are available.";

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
                return "No spectrometer is connected.";

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
                return "Tick the spectrometer to measure in the Use column before starting.";

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
                ? "Measurement\r\nRUNNING\r\n(click to stop)"
                : "Measurement\r\nSTART";
            chkRun.ForeColor = on ? Color.Black : SystemColors.ControlText;
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

            statusState.Text = running ? "Measurement running." : "Idle.";
        }

        // ---------------------------------------------------------------- button handlers

        private void OnCaptureDarkClick(object sender, EventArgs e)
        {
            if (!RequireDisplayedSpectrum("Capture Dark"))
                return;

            _engine.CaptureDark(_lastFrame);
        }

        private void OnCaptureReferenceClick(object sender, EventArgs e)
        {
            if (!RequireDisplayedSpectrum("Capture White Reference"))
                return;

            _engine.CaptureWhiteReference(_lastFrame);
        }

        /// <summary>
        /// Both captures take the spectrum that is already on screen rather than triggering
        /// a fresh read, so what is stored is exactly what the operator was looking at.
        /// </summary>
        private bool RequireDisplayedSpectrum(string action)
        {
            if (_lastFrame != null && _lastFrame.HasSpectrum)
                return true;

            statusState.Text = action + ": no spectrum is displayed yet - press Measurement START first.";
            return false;
        }

        private void OnClearReferenceClick(object sender, EventArgs e)
        {
            _engine.ClearReferences();
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that CSV files are written to.";
                dialog.ShowNewFolderButton = true;

                if (Directory.Exists(txtSavePath.Text))
                    dialog.SelectedPath = txtSavePath.Text;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtSavePath.Text = dialog.SelectedPath;
            }
        }

        private void OnSaveSpectraClick(object sender, EventArgs e)
        {
            try
            {
                string path = CsvExporter.BuildPath(txtSavePath.Text, txtSample.Text, "spectrum");
                string written = CsvExporter.SaveSpectrum(path, BuildMetadata(), _lastFrame, _lastReferences);

                txtLastSavedPath.Text = written;
                statusState.Text = "Saved " + Path.GetFileName(written);
            }
            catch (Exception ex)
            {
                ShowSaveError(ex);
            }
        }

        private void OnSaveTimedSpectraClick(object sender, EventArgs e)
        {
            try
            {
                string path = CsvExporter.BuildPath(txtSavePath.Text, txtSample.Text, "timed");
                string[] written = CsvExporter.SaveTimedSeries(path, BuildMetadata(), _timedStore.Snapshot());

                txtLastSavedPath.Text = string.Join(" ; ", written);
                statusState.Text = "Saved " + written.Length + " file(s).";
            }
            catch (Exception ex)
            {
                ShowSaveError(ex);
            }
        }

        private void ShowSaveError(Exception exception)
        {
            statusState.Text = "Save failed: " + exception.Message;
            MessageBox.Show(this, exception.Message, "Save failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private ExportMetadata BuildMetadata()
        {
            MeasurementSettings settings = BuildSettings();
            DeviceSettings active = settings.Active;

            return new ExportMetadata
            {
                Sample = txtSample.Text,
                Concentration = txtConcentration.Text,
                CellThickness = txtCellThickness.Text,
                DeviceLines = BuildDeviceLines(),
                ActiveDevice = DescribeDevice(settings.ActiveIndex),
                ProbeDevice = DescribeDevice(settings.ProbeIndex),
                ReferenceDevice = DescribeDevice(settings.ReferenceIndex),
                BeamsAssigned = settings.BeamsAssigned,
                IntegrationMs = active == null ? 0.0 : active.IntegrationMs,
                ScansToAverage = settings.ScansToAverage,
                MinIntervalMs = settings.MinIntervalMs,
                TriggerMode = settings.TriggerMode,
                RequestedTriggerDelayUs = active == null ? 0.0 : active.TriggerDelayUs,
                AppliedTriggerDelay = _appliedDelaysText,
                GpioState = DescribeGpioState(),
                ReferenceState = _lastReferences == null ? "none" : _lastReferences.Describe(),
                DriftCorrection = DescribeDriftCorrection(settings),
                DriftCorrectionApplied = _lastFrame != null && _lastFrame.DriftCorrected
            };
        }

        private string DescribeDriftCorrection(MeasurementSettings settings)
        {
            if (_devices.Length < 2)
                return string.Empty;

            string blocker = settings.DescribeDriftBlocker(_devices.Length);
            if (blocker != null)
                return "not applied - " + blocker;

            return _lastFrame != null && _lastFrame.DriftCorrected
                ? "applied from " + DescribeDevice(settings.ReferenceIndex)
                : "armed but not applied - the reference channel has no dark/white reference yet";
        }

        /// <summary>
        /// The pins being driven, per spectrometer, for the CSV header. Only outputs are
        /// listed: an input's level says what was wired to it, not what this program did.
        /// </summary>
        private string DescribeGpioState()
        {
            var parts = new List<string>();

            foreach (DeviceInfo device in _devices)
            {
                GpioChannelInfo[] channels;
                if (!_gpioByDevice.TryGetValue(device.Identity, out channels))
                    channels = device.GpioChannels;

                var driven = new List<string>();
                foreach (GpioChannelInfo channel in channels)
                {
                    if (channel.IsOutput)
                        driven.Add("GPIO " + channel.Index + "=" + channel.DescribeLevel());
                }

                if (driven.Count > 0)
                    parts.Add(device.ShortLabel + " " + string.Join(" ", driven.ToArray()));
            }

            return parts.Count == 0 ? "no pin driven" : string.Join(" ; ", parts.ToArray());
        }

        private string[] BuildDeviceLines()
        {
            var lines = new List<string>(_deviceSettings.Length);

            for (int i = 0; i < _deviceSettings.Length && i < _devices.Length; i++)
            {
                DeviceSettings settings = _deviceSettings[i];
                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} beam={1} measured={2} integration={3:F3} ms trigger delay={4:F1} us",
                    _devices[i].ShortLabel, settings.DescribeRole(),
                    settings.Enabled ? "yes" : "no", settings.IntegrationMs, settings.TriggerDelayUs));
            }

            return lines.ToArray();
        }

        private string DescribeDevice(int index)
        {
            return index >= 0 && index < _devices.Length ? _devices[index].ShortLabel : "n/a";
        }

        private void OnToggleTimedClick(object sender, EventArgs e)
        {
            _accumulateTimed = !_accumulateTimed;

            btnToggleTimed.BackColor = _accumulateTimed ? Color.LimeGreen : SystemColors.Control;
            btnToggleTimed.UseVisualStyleBackColor = !_accumulateTimed;
            btnToggleTimed.Text = _accumulateTimed ? "Recording - click to stop" : "Save timed datasets";

            statusState.Text = _accumulateTimed
                ? "Accumulating timed datasets (cap " + _timedStore.Capacity + " records)."
                : "Timed dataset recording stopped.";
        }

        private void OnClearDatasetsClick(object sender, EventArgs e)
        {
            _timedStore.Clear();
            _timedDirty = true;

            chartTimed.BeginDataUpdate();
            try { chartTimed.Series.Clear(); }
            finally { chartTimed.EndDataUpdate(); }

            heatmap.SetSource(TimedSeriesSnapshot.Empty);
            RefreshSlowViews();

            statusState.Text = "Timed datasets cleared.";
        }

        private void OnAddTraceClick(object sender, EventArgs e)
        {
            double wavelength = (double)numTraceWavelength.Value;
            if (_traceWavelengths.Contains(wavelength))
                return;

            _traceWavelengths.Add(wavelength);
            _traceWavelengths.Sort();

            RefreshTraceList();
            _timedDirty = true;
            RefreshSlowViews();
        }

        private void OnRemoveTraceClick(object sender, EventArgs e)
        {
            int index = listTraces.SelectedIndex;
            if (index < 0 || index >= _traceWavelengths.Count)
                return;

            _traceWavelengths.RemoveAt(index);

            RefreshTraceList();
            _timedDirty = true;
            RefreshSlowViews();
        }

        private void RefreshTraceList()
        {
            listTraces.BeginUpdate();
            try
            {
                listTraces.Items.Clear();
                foreach (double wavelength in _traceWavelengths)
                    listTraces.Items.Add(wavelength.ToString("F2", CultureInfo.InvariantCulture) + " nm");
            }
            finally
            {
                listTraces.EndUpdate();
            }
        }

        private void OnHeatScaleChanged(object sender, EventArgs e)
        {
            if (_suppressHeatSync)
                return;

            bool auto = chkHeatAutoScale.Checked;
            numHeatMin.Enabled = !auto;
            numHeatMax.Enabled = !auto;

            if (sender == chkHeatAutoScale)
            {
                // Switching the colour scale off pins it to the range the map is already
                // showing, so the spinners follow the map rather than the map jumping to
                // whatever the spinners happened to hold.
                heatmap.SetAutoScale(PlotAxis.Color, auto);
                SyncHeatControls();
            }
            else if (!auto)
            {
                heatmap.SetManualScale((double)numHeatMin.Value, (double)numHeatMax.Value);
            }

            UpdateHeatInfo();
        }

        /// <summary>
        /// Mirrors the map's colour scale into the panel above it. The same range can be
        /// changed from the checkbox, the spinners, the right-click menu or by typing over
        /// a colour-bar number, so one of them has to be the single source of truth - the
        /// map is, and this pushes its state back into the panel.
        /// </summary>
        private void SyncHeatControls()
        {
            _suppressHeatSync = true;
            try
            {
                chkHeatAutoScale.Checked = heatmap.AutoScale;
                numHeatMin.Enabled = !heatmap.AutoScale;
                numHeatMax.Enabled = !heatmap.AutoScale;
                numHeatMin.Value = ClampToSpinner(numHeatMin, heatmap.ScaleMinimum);
                numHeatMax.Value = ClampToSpinner(numHeatMax, heatmap.ScaleMaximum);
            }
            finally
            {
                _suppressHeatSync = false;
            }
        }

        private static decimal ClampToSpinner(NumericUpDown spinner, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return spinner.Minimum;

            decimal rounded = Math.Round((decimal)value, spinner.DecimalPlaces);
            if (rounded < spinner.Minimum) return spinner.Minimum;
            if (rounded > spinner.Maximum) return spinner.Maximum;
            return rounded;
        }

        /// <summary>The map changed a scale itself, through its menu or an edited number.</summary>
        private void OnHeatmapScalesChanged(object sender, EventArgs e)
        {
            SyncHeatControls();
            UpdateHeatInfo();
            statusState.Text = "2D map scales:  " + heatmap.DescribeScales();
        }

        // ---------------------------------------------------------------- user settings

        private void RestoreUserSettings()
        {
            string error;
            _userSettings = UserSettingsStore.Load(Program.SettingsPath, out error);
            if (error != null)
                statusState.Text = error;

            UserSettings saved = _userSettings;
            _suppressSettingsPush = true;
            try
            {
                if (!string.IsNullOrEmpty(saved.SavePath))
                    txtSavePath.Text = saved.SavePath;
                if (saved.Sample != null)
                    txtSample.Text = saved.Sample;
                if (saved.Concentration != null)
                    txtConcentration.Text = saved.Concentration;
                if (saved.CellThickness != null)
                    txtCellThickness.Text = saved.CellThickness;

                if (saved.MinIntervalMs >= 0)
                    numMinInterval.Value = ClampToSpinner(numMinInterval, saved.MinIntervalMs);
                if (saved.ScansToAverage >= 1)
                    numAverage.Value = ClampToSpinner(numAverage, saved.ScansToAverage);

                _traceWavelengths.Clear();
                foreach (double wavelength in saved.TraceWavelengths ?? new List<double>())
                {
                    if (!double.IsNaN(wavelength) && !_traceWavelengths.Contains(wavelength))
                        _traceWavelengths.Add(wavelength);
                }
                _traceWavelengths.Sort();
                RefreshTraceList();

                if (saved.MainTabIndex >= 0 && saved.MainTabIndex < tabMain.TabCount)
                    tabMain.SelectedIndex = saved.MainTabIndex;
                if (saved.InnerTabIndex >= 0 && saved.InnerTabIndex < tabInner.TabCount)
                    tabInner.SelectedIndex = saved.InnerTabIndex;
            }
            finally
            {
                _suppressSettingsPush = false;
            }
        }

        /// <summary>
        /// Puts the window back where it was, but only when that spot is still on a screen:
        /// a laptop that was docked to a second monitor last time must not open off-screen.
        /// </summary>
        private void RestoreWindowPlacement()
        {
            UserSettings saved = _userSettings;
            if (!saved.HasWindowBounds)
                return;

            var bounds = new Rectangle(saved.WindowLeft, saved.WindowTop, saved.WindowWidth, saved.WindowHeight);
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle visible = Rectangle.Intersect(screen.WorkingArea, bounds);
                if (visible.Width < 100 || visible.Height < 100)
                    continue;

                StartPosition = FormStartPosition.Manual;
                Bounds = bounds;
                if (saved.WindowMaximized)
                    WindowState = FormWindowState.Maximized;
                return;
            }
        }

        private void SaveUserSettings()
        {
            UserSettings settings = _userSettings;

            settings.SavePath = txtSavePath.Text;
            settings.Sample = txtSample.Text;
            settings.Concentration = txtConcentration.Text;
            settings.CellThickness = txtCellThickness.Text;
            settings.MinIntervalMs = (int)numMinInterval.Value;
            settings.ScansToAverage = (int)numAverage.Value;
            settings.TraceWavelengths = new List<double>(_traceWavelengths);
            settings.MainTabIndex = tabMain.SelectedIndex;
            settings.InnerTabIndex = tabInner.SelectedIndex;

            Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            settings.WindowLeft = bounds.Left;
            settings.WindowTop = bounds.Top;
            settings.WindowWidth = bounds.Width;
            settings.WindowHeight = bounds.Height;
            settings.WindowMaximized = WindowState == FormWindowState.Maximized;

            // Rows of spectrometers that are not plugged in right now are kept, so moving a
            // device to another PC and back does not lose its settings.
            for (int i = 0; i < _devices.Length && i < _deviceSettings.Length; i++)
            {
                settings.Remember(new DeviceUserSettings
                {
                    Identity = _devices[i].Identity,
                    Model = _devices[i].ModelLabel,
                    IntegrationMs = _deviceSettings[i].IntegrationMs,
                    TriggerDelayUs = _deviceSettings[i].TriggerDelayUs
                });
            }

            UserSettingsStore.Save(Program.SettingsPath, settings);
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
