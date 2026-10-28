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
    // Display mode, smoothing, baseline, peaks, overlays, the cursor readout and the
    // Beer-Lambert line - everything that changes how the absorption plot is read.
    internal partial class MainForm
    {
        private const string PeakSeriesName = "Peaks";
        private const string OverlayPrefix = "overlay:";

        private Panel _analysisBar;
        private ComboBox _cmbDisplay, _cmbBaseline;
        private CheckBox _chkSmooth, _chkPeaks;
        private NumericUpDown _numSmoothWindow, _numProminence, _numAnchorFirst, _numAnchorSecond;
        private Label _lblSmooth, _lblBaseline, _lblAnchors, _lblPeaks, _lblReadout;
        private Button _btnOverlay, _btnClearOverlays;

        private readonly List<ImportedSpectrum> _overlays = new List<ImportedSpectrum>();
        private readonly TimedSeriesRecorder _recorder = new TimedSeriesRecorder();

        private DisplayMode _displayMode = DisplayMode.Absorbance;
        private BaselineMode _baselineMode = BaselineMode.None;
        private bool _suppressAnalysisEvents;

        /// <summary>Wavelength the pointer last hovered over, or NaN when it left the plot.</summary>
        private double _cursorWavelength = double.NaN;

        /// <summary>What the absorption plot currently shows, after processing.</summary>
        private double[] _displayedWavelengths;
        private double[] _displayedValues;
        private Peak[] _peaks = new Peak[0];

        private void BuildAnalysisBar()
        {
            _analysisBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = LogicalToDeviceUnits(84),
                BackColor = Theme.Surface,
                Padding = new Padding(LogicalToDeviceUnits(6))
            };

            int x = LogicalToDeviceUnits(8);
            int row1 = LogicalToDeviceUnits(6);
            int row2 = LogicalToDeviceUnits(34);
            int gap = LogicalToDeviceUnits(10);

            _cmbDisplay = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, row1),
                Width = LogicalToDeviceUnits(150)
            };
            _cmbDisplay.Items.AddRange(new object[] { string.Empty, string.Empty, string.Empty });
            _cmbDisplay.SelectedIndex = 0;
            _cmbDisplay.SelectedIndexChanged += OnAnalysisOptionChanged;
            x += _cmbDisplay.Width + gap;

            _chkSmooth = new CheckBox { Location = new Point(x, row1 + LogicalToDeviceUnits(3)), AutoSize = true };
            _chkSmooth.CheckedChanged += OnAnalysisOptionChanged;
            _numSmoothWindow = Spinner(5, 101, 0, 9, 2);
            _lblSmooth = Caption();

            _chkPeaks = new CheckBox { Location = new Point(x, row2 + LogicalToDeviceUnits(3)), AutoSize = true };
            _chkPeaks.CheckedChanged += OnAnalysisOptionChanged;
            _numProminence = Spinner(0.001m, 10m, 3, 0.02m, 0.01m);
            _lblPeaks = Caption();

            _cmbBaseline = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(x, row2),
                Width = LogicalToDeviceUnits(150)
            };
            _cmbBaseline.Items.AddRange(new object[] { string.Empty, string.Empty, string.Empty, string.Empty });
            _cmbBaseline.SelectedIndex = 0;
            _cmbBaseline.SelectedIndexChanged += OnAnalysisOptionChanged;

            _lblBaseline = Caption();
            _lblAnchors = Caption();
            _numAnchorFirst = Spinner(0, 5000, 1, 400, 1);
            _numAnchorSecond = Spinner(0, 5000, 1, 800, 1);

            _btnOverlay = new Button { AutoSize = true };
            _btnOverlay.Click += (s, e) => OpenOverlay();
            Theme.StyleButton(_btnOverlay);

            _btnClearOverlays = new Button { AutoSize = true };
            _btnClearOverlays.Click += (s, e) => ClearOverlays();
            Theme.StyleButton(_btnClearOverlays);

            // Its own full-width row: the three readings together are longer than any gap
            // left between the controls, and wrapping them would hide the last one.
            _lblReadout = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.Text,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            _analysisBar.Controls.AddRange(new Control[]
            {
                _cmbDisplay, _chkSmooth, _lblSmooth, _numSmoothWindow,
                _cmbBaseline, _lblBaseline, _lblAnchors, _numAnchorFirst, _numAnchorSecond,
                _chkPeaks, _lblPeaks, _numProminence,
                _btnOverlay, _btnClearOverlays, _lblReadout
            });

            tabPageAbsorption.Controls.Add(_analysisBar);

            // The last control in the collection docks first, so the bar has to go to the
            // back for the Fill chart to take the space left under it. BringToFront would
            // make the chart claim the whole page and the bar would cover its top edge.
            _analysisBar.SendToBack();

            AddSeries(chartAbsorption, PeakSeriesName, Theme.Danger).ChartType = SeriesChartType.Point;
            chartAbsorption.Series[PeakSeriesName].MarkerStyle = MarkerStyle.Diamond;
            chartAbsorption.Series[PeakSeriesName].MarkerSize = LogicalToDeviceUnits(8);
            chartAbsorption.Series[PeakSeriesName].IsVisibleInLegend = false;

            chartAbsorption.MouseMove += OnPlotMouseMove;
            chartAbsorption.MouseLeave += (s, e) => { _cursorWavelength = double.NaN; UpdateReadout(); };

            // The language pass already ran inside BuildShell, before this bar existed, so
            // its captions are filled in here.
            ApplyAnalysisTexts();
            _analysisBar.Resize += (s, e) => LayoutAnalysisBar();
        }

        private NumericUpDown Spinner(decimal minimum, decimal maximum, int decimals, decimal value, decimal step)
        {
            var spinner = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimals,
                Value = value,
                Increment = step,
                Width = LogicalToDeviceUnits(decimals > 0 ? 76 : 62)
            };
            spinner.ValueChanged += OnAnalysisOptionChanged;
            return spinner;
        }

        private static Label Caption()
        {
            return new Label { AutoSize = true, ForeColor = Theme.TextMuted };
        }

        /// <summary>
        /// Places the bar's controls after a language change, because the captions in front of
        /// each control are a different width in Korean and in English.
        /// </summary>
        private void LayoutAnalysisBar()
        {
            if (_analysisBar == null)
                return;

            int gap = LogicalToDeviceUnits(8);
            int row1 = LogicalToDeviceUnits(6);
            int row2 = LogicalToDeviceUnits(34);
            int textOffset = LogicalToDeviceUnits(4);

            int x = LogicalToDeviceUnits(8);
            _cmbDisplay.Location = new Point(x, row1);
            _cmbBaseline.Location = new Point(x, row2);
            x += Math.Max(_cmbDisplay.Width, _cmbBaseline.Width) + gap * 2;

            int top = x;
            _chkSmooth.Location = new Point(x, row1 + textOffset);
            int afterSmoothCheck = x + _chkSmooth.Width + gap;
            _lblSmooth.Location = new Point(afterSmoothCheck, row1 + textOffset);
            _numSmoothWindow.Location = new Point(afterSmoothCheck + _lblSmooth.Width + gap / 2, row1);

            _chkPeaks.Location = new Point(top, row2 + textOffset);
            int afterPeakCheck = top + _chkPeaks.Width + gap;
            _lblPeaks.Location = new Point(afterPeakCheck, row2 + textOffset);
            _numProminence.Location = new Point(afterPeakCheck + _lblPeaks.Width + gap / 2, row2);

            x = Math.Max(_numSmoothWindow.Right, _numProminence.Right) + gap * 2;
            _lblAnchors.Location = new Point(x, row2 + textOffset);
            _numAnchorFirst.Location = new Point(x + _lblAnchors.Width + gap / 2, row2);
            _numAnchorSecond.Location = new Point(_numAnchorFirst.Right + gap / 2, row2);

            _btnOverlay.Location = new Point(x, row1);
            _btnClearOverlays.Location = new Point(_btnOverlay.Right + gap / 2, row1);

            int left = LogicalToDeviceUnits(8);
            _lblReadout.Location = new Point(left, LogicalToDeviceUnits(62));
            _lblReadout.Size = new Size(Math.Max(LogicalToDeviceUnits(120), _analysisBar.Width - left - gap),
                LogicalToDeviceUnits(18));
        }

        private void ApplyAnalysisTexts()
        {
            if (_analysisBar == null)
                return;

            _suppressAnalysisEvents = true;
            try
            {
                int display = _cmbDisplay.SelectedIndex;
                _cmbDisplay.Items[0] = L.T("display.absorbance", "Absorbance (OD)");
                _cmbDisplay.Items[1] = L.T("display.transmittance", "Transmittance (%T)");
                _cmbDisplay.Items[2] = L.T("display.counts", "Raw counts");
                _cmbDisplay.SelectedIndex = display;

                int baseline = _cmbBaseline.SelectedIndex;
                _cmbBaseline.Items[0] = L.T("baseline.none", "No baseline correction");
                _cmbBaseline.Items[1] = L.T("baseline.offset", "Baseline: zero at");
                _cmbBaseline.Items[2] = L.T("baseline.linear", "Baseline: line between");
                _cmbBaseline.Items[3] = L.T("baseline.rubberband", "Baseline: rubber band");
                _cmbBaseline.SelectedIndex = baseline;

                _chkSmooth.Text = L.T("analysis.smooth", "Smooth");
                _lblSmooth.Text = L.T("analysis.window", "window (px)");
                _chkPeaks.Text = L.T("analysis.peaks", "Peaks");
                _lblPeaks.Text = L.T("analysis.prominence", "min. height");
                _lblAnchors.Text = L.T("analysis.anchors", "at (nm)");
                _btnOverlay.Text = L.T("analysis.openSpectrum", "Open spectrum...");
                _btnClearOverlays.Text = L.T("analysis.clearOverlays", "Clear overlays");
            }
            finally
            {
                _suppressAnalysisEvents = false;
            }

            LayoutAnalysisBar();
            UpdateAnalysisEnabled();
            UpdateReadout();
        }

        private void UpdateAnalysisEnabled()
        {
            _numSmoothWindow.Enabled = _chkSmooth.Checked;
            _numProminence.Enabled = _chkPeaks.Checked;
            _numAnchorFirst.Enabled = _baselineMode == BaselineMode.Offset || _baselineMode == BaselineMode.Linear;
            _numAnchorSecond.Enabled = _baselineMode == BaselineMode.Linear;
            _lblAnchors.Enabled = _numAnchorFirst.Enabled;
            _btnClearOverlays.Enabled = _overlays.Count > 0;
        }

        private void OnAnalysisOptionChanged(object sender, EventArgs e)
        {
            if (_suppressAnalysisEvents)
                return;

            _displayMode = (DisplayMode)Math.Max(0, _cmbDisplay.SelectedIndex);
            _baselineMode = (BaselineMode)Math.Max(0, _cmbBaseline.SelectedIndex);

            UpdateAnalysisEnabled();
            ApplyChartTexts();
            RedrawAbsorption();
        }

        // ---------------------------------------------------------------- plotting

        /// <summary>The raw series for the chosen display mode, before smoothing or baseline.</summary>
        private double[] RawForDisplay(SpectrumFrame frame)
        {
            if (frame == null)
                return null;

            switch (_displayMode)
            {
                case DisplayMode.Transmittance:
                    return SpectrumProcessing.ToTransmittance(frame.Absorbance);
                case DisplayMode.Counts:
                    return frame.Counts;
                default:
                    return frame.Absorbance;
            }
        }

        private double[] ApplyProcessing(double[] wavelengths, double[] values)
        {
            if (values == null)
                return null;

            double[] result = values;

            if (_chkSmooth != null && _chkSmooth.Checked)
                result = SpectrumProcessing.SavitzkyGolay(result, (int)_numSmoothWindow.Value, 2);

            if (_baselineMode != BaselineMode.None)
            {
                double[] baseline = SpectrumProcessing.Baseline(wavelengths, result, _baselineMode,
                    (double)_numAnchorFirst.Value, (double)_numAnchorSecond.Value);
                result = SpectrumProcessing.Subtract(result, baseline);
            }

            return result;
        }

        private void UpdateAbsorptionPlot(SpectrumFrame frame)
        {
            double[] wavelengths = frame == null ? null : frame.Wavelengths;
            double[] values = ApplyProcessing(wavelengths, RawForDisplay(frame));

            _displayedWavelengths = wavelengths;
            _displayedValues = values;

            chartAbsorption.BeginDataUpdate();
            try
            {
                Plot(chartAbsorption.Series["Absorbance"], wavelengths, values);
                PlotOverlays();
                PlotPeaks(wavelengths, values);
            }
            finally
            {
                chartAbsorption.EndDataUpdate();
            }

            UpdateReadout();
        }

        private void RedrawAbsorption()
        {
            if (_lastFrame != null)
                UpdateAbsorptionPlot(_lastFrame);
            else
                UpdateReadout();
        }

        /// <summary>
        /// Draws the loaded files. A counts file is only shown while the plot is on the counts
        /// scale, and an absorbance file only on an absorbance or %T scale, so two different
        /// quantities can never share one axis.
        /// </summary>
        private void PlotOverlays()
        {
            for (int i = 0; i < _overlays.Count; i++)
            {
                ImportedSpectrum overlay = _overlays[i];
                string name = OverlayPrefix + i;

                Series series = chartAbsorption.Series.FindByName(name);
                if (series == null)
                {
                    series = AddSeries(chartAbsorption, name, TraceColors[(i + 3) % TraceColors.Length]);
                    series.BorderDashStyle = ChartDashStyle.Dash;
                }

                series.LegendText = overlay.Name;

                bool fits = overlay.IsAbsorbance
                    ? _displayMode != DisplayMode.Counts
                    : _displayMode == DisplayMode.Counts;

                double[] values = null;
                if (fits)
                {
                    values = overlay.IsAbsorbance && _displayMode == DisplayMode.Transmittance
                        ? SpectrumProcessing.ToTransmittance(overlay.Values)
                        : overlay.Values;

                    values = ApplyProcessing(overlay.Wavelengths, values);
                }

                Plot(series, overlay.Wavelengths, values);
            }
        }

        private void PlotPeaks(double[] wavelengths, double[] values)
        {
            Series series = chartAbsorption.Series[PeakSeriesName];

            if (!_chkPeaks.Checked || wavelengths == null || values == null)
            {
                _peaks = new Peak[0];
                if (series.Points.Count > 0)
                    series.Points.Clear();
                return;
            }

            _peaks = SpectrumProcessing.FindPeaks(wavelengths, values, (double)_numProminence.Value, 12);

            series.Points.SuspendUpdates();
            try
            {
                series.Points.Clear();
                foreach (Peak peak in _peaks)
                {
                    int point = series.Points.AddXY(peak.Wavelength, peak.Value);
                    series.Points[point].Label = peak.Wavelength.ToString("F1", CultureInfo.InvariantCulture);
                    series.Points[point].LabelForeColor = Theme.Danger;
                }
            }
            finally
            {
                series.Points.ResumeUpdates();
            }
        }

        private void OnPlotMouseMove(object sender, MouseEventArgs e)
        {
            if (_displayedWavelengths == null || _displayedWavelengths.Length == 0)
                return;

            try
            {
                ChartArea area = chartAbsorption.ChartAreas[0];
                double wavelength = area.AxisX.PixelPositionToValue(e.X);

                if (wavelength < _displayedWavelengths[0] ||
                    wavelength > _displayedWavelengths[_displayedWavelengths.Length - 1])
                {
                    return;
                }

                _cursorWavelength = wavelength;
                UpdateReadout();
            }
            catch (InvalidOperationException)
            {
                // The chart has not been laid out yet; the next move will work.
            }
        }

        /// <summary>
        /// The line under the plot: where the pointer is, what the strongest peak is, and what
        /// Beer-Lambert makes of the value there with the concentration and cell thickness the
        /// operator typed.
        /// </summary>
        private void UpdateReadout()
        {
            if (_lblReadout == null)
                return;

            var parts = new List<string>(3);

            if (!double.IsNaN(_cursorWavelength) && _displayedValues != null)
            {
                double value = SpectrumProcessing.ValueAt(_displayedWavelengths, _displayedValues, _cursorWavelength);
                parts.Add(L.F("readout.cursor", "Cursor {0:F1} nm = {1}", _cursorWavelength, Format(value)));
            }

            if (_peaks.Length > 0)
            {
                Peak strongest = _peaks[0];
                foreach (Peak peak in _peaks)
                    if (peak.Value > strongest.Value)
                        strongest = peak;

                parts.Add(L.F("readout.peak", "Peak {0:F1} nm = {1} ({2} found)",
                    strongest.Wavelength, Format(strongest.Value), _peaks.Length));
            }

            string beer = DescribeBeerLambert();
            if (beer != null)
                parts.Add(beer);

            _lblReadout.Text = parts.Count == 0
                ? L.T("readout.hint", "Move the pointer over the plot to read a value.")
                : string.Join("     ", parts.ToArray());
        }

        private string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return "-";

            switch (_displayMode)
            {
                case DisplayMode.Transmittance:
                    return value.ToString("F2", CultureInfo.InvariantCulture) + " %T";
                case DisplayMode.Counts:
                    return value.ToString("F0", CultureInfo.InvariantCulture) + " counts";
                default:
                    return value.ToString("F4", CultureInfo.InvariantCulture) + " OD";
            }
        }

        /// <summary>
        /// e = A / (c l) at the wavelength being read, using the sample fields on the panel.
        /// Only shown on the absorbance scale: %T and raw counts are not what the law relates.
        /// </summary>
        private string DescribeBeerLambert()
        {
            if (_displayMode != DisplayMode.Absorbance || _displayedValues == null)
                return null;

            double wavelength = !double.IsNaN(_cursorWavelength)
                ? _cursorWavelength
                : (_peaks.Length > 0 ? _peaks[0].Wavelength : double.NaN);

            if (double.IsNaN(wavelength))
                return null;

            double absorbance = SpectrumProcessing.ValueAt(_displayedWavelengths, _displayedValues, wavelength);
            if (double.IsNaN(absorbance))
                return null;

            double concentration, path;
            if (!BeerLambert.TryParseConcentration(txtConcentration.Text, out concentration) ||
                !BeerLambert.TryParsePathLength(txtCellThickness.Text, out path))
            {
                return L.T("readout.beerHint",
                    "Type a concentration (e.g. 0.05 mM) and a cell thickness (e.g. 10 mm) for the molar absorptivity.");
            }

            double epsilon = BeerLambert.MolarAbsorptivity(absorbance, concentration, path);
            if (double.IsNaN(epsilon))
                return null;

            return L.F("readout.beer", "At {0:F1} nm: e = {1:G4} L/(mol cm)   (c = {2}, l = {3:G3} cm)",
                wavelength, epsilon, BeerLambert.DescribeConcentration(concentration), path);
        }

        // ---------------------------------------------------------------- overlays

        private void OpenOverlay()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = L.T("analysis.openSpectrumTitle", "Open a saved spectrum");
                dialog.Filter = L.T("filter.spectrum", "Spectrum CSV") + " (*.csv)|*.csv|" +
                                L.T("filter.all", "All files") + " (*.*)|*.*";
                dialog.Multiselect = true;
                if (Directory.Exists(txtSavePath.Text))
                    dialog.InitialDirectory = txtSavePath.Text;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                int loaded = 0;
                foreach (string path in dialog.FileNames)
                {
                    string error;
                    ImportedSpectrum spectrum = SpectrumImport.Load(path, out error);
                    if (spectrum == null)
                    {
                        MessageBox.Show(this, L.F("overlay.failed", "{0} could not be read: {1}",
                                Path.GetFileName(path), error),
                            L.T("analysis.openSpectrumTitle", "Open a saved spectrum"),
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    _overlays.Add(spectrum);
                    loaded++;
                }

                if (loaded == 0)
                    return;

                statusState.Text = L.F("overlay.loaded", "{0} spectrum/spectra loaded for comparison.", loaded);
                UpdateAnalysisEnabled();
                RedrawAbsorption();
            }
        }

        private void ClearOverlays()
        {
            if (_overlays.Count == 0)
                return;

            chartAbsorption.BeginDataUpdate();
            try
            {
                for (int i = 0; i < _overlays.Count; i++)
                {
                    Series series = chartAbsorption.Series.FindByName(OverlayPrefix + i);
                    if (series != null)
                        chartAbsorption.Series.Remove(series);
                }
            }
            finally
            {
                chartAbsorption.EndDataUpdate();
            }

            _overlays.Clear();
            UpdateAnalysisEnabled();
            statusState.Text = L.T("overlay.cleared", "Loaded spectra removed from the plot.");
            RedrawAbsorption();
        }

        // ---------------------------------------------------------------- export

        private Bitmap RenderPlot()
        {
            Control plot = CurrentPlotControl();
            var bitmap = new Bitmap(Math.Max(1, plot.Width), Math.Max(1, plot.Height));
            plot.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            return bitmap;
        }

        private Control CurrentPlotControl()
        {
            if (tabMain.SelectedTab == tabPageProbe)
                return chartProbe;
            if (tabMain.SelectedTab == tabPageTimed)
                return chartTimed;
            if (tabMain.SelectedTab == tabPageTimed2D)
                return heatmap;

            return tabInner.SelectedTab == tabPageFluorescence ? (Control)chartFluorescence : chartAbsorption;
        }

        private void ExportPlotImage()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = L.T("export.png", "Export plot as image");
                dialog.Filter = "PNG (*.png)|*.png";
                dialog.FileName = Path.GetFileName(CsvExporter.BuildPath(string.Empty, txtSample.Text, "plot"))
                    .Replace(".csv", ".png");
                if (Directory.Exists(txtSavePath.Text))
                    dialog.InitialDirectory = txtSavePath.Text;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    using (Bitmap plot = RenderPlot())
                        statusState.Text = L.F("status.saved", "Saved {0}",
                            Path.GetFileName(ReportExporter.SavePng(plot, dialog.FileName)));
                }
                catch (Exception ex)
                {
                    ShowSaveError(ex);
                }
            }
        }

        private void ExportReport()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = L.T("export.pdf", "Export measurement report");
                dialog.Filter = "PDF (*.pdf)|*.pdf";
                dialog.FileName = Path.GetFileName(CsvExporter.BuildPath(string.Empty, txtSample.Text, "report"))
                    .Replace(".csv", ".pdf");
                if (Directory.Exists(txtSavePath.Text))
                    dialog.InitialDirectory = txtSavePath.Text;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    using (Bitmap plot = RenderPlot())
                    using (Bitmap page = ReportExporter.RenderPage(BuildReport(plot), 150))
                    {
                        statusState.Text = L.F("status.saved", "Saved {0}",
                            Path.GetFileName(ReportExporter.SavePdf(page, dialog.FileName)));
                    }
                }
                catch (Exception ex)
                {
                    ShowSaveError(ex);
                }
            }
        }

        private ReportContent BuildReport(Bitmap plot)
        {
            ExportMetadata metadata = BuildMetadata();
            var content = new ReportContent
            {
                Title = L.T("report.title", "UV-VIS measurement report"),
                Subtitle = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                           (SpectrometerBackend.IsSimulated
                               ? "   [" + L.T("title.demo", "DEMO MODE - simulated spectrometers") + "]"
                               : string.Empty),
                Plot = plot,
                Footer = "UV-VIS meas " + Program.Version
            };

            content.Add(L.T("panel.sample", "Sample"), metadata.Sample);
            content.Add(L.T("panel.concentration", "Concentration (mM)"), metadata.Concentration);
            content.Add(L.T("panel.cellThickness", "Cell Thickness"), metadata.CellThickness);
            content.Add(L.T("report.device", "Spectrometer"), metadata.ActiveDevice);
            content.Add(L.T("panel.minInterval", "Set Min Interval (ms)"),
                metadata.MinIntervalMs.ToString(CultureInfo.InvariantCulture));
            content.Add(L.T("panel.average", "Average"),
                metadata.ScansToAverage.ToString(CultureInfo.InvariantCulture));
            content.Add(L.T("grid.integration", "Integration time (ms)"),
                metadata.IntegrationMs.ToString("F3", CultureInfo.InvariantCulture));
            content.Add(L.T("panel.triggerMode", "Trigger Mode"), TriggerModes.Label(metadata.TriggerMode));
            content.Add(L.T("report.display", "Display"), _cmbDisplay.Text);
            content.Add(L.T("report.processing", "Processing"), DescribeProcessing());

            // These three run long - a truncated reference state would hide whether the
            // spectrum was referenced at all - so they get the full page width.
            content.AddWide(L.T("report.triggerDelay", "Trigger delay"), metadata.AppliedTriggerDelay);
            content.AddWide(L.T("report.references", "Dark / White"), DescribeReferences(_lastReferences));
            content.AddWide(L.T("report.drift", "Live reference correction"),
                string.IsNullOrEmpty(metadata.DriftCorrection) ? "-" : TranslateDrift(metadata.DriftCorrection));

            if (_peaks.Length > 0)
            {
                var list = new List<string>();
                foreach (Peak peak in _peaks)
                    list.Add(peak.Wavelength.ToString("F1", CultureInfo.InvariantCulture));

                content.AddWide(L.T("report.peaks", "Peaks (nm)"), string.Join(", ", list.ToArray()));
            }

            return content;
        }

        private string DescribeProcessing()
        {
            var parts = new List<string>(2);

            if (_chkSmooth.Checked)
                parts.Add(L.F("report.smoothing", "smoothing {0} px", (int)_numSmoothWindow.Value));

            if (_baselineMode != BaselineMode.None)
                parts.Add(_cmbBaseline.Text);

            return parts.Count == 0 ? L.T("report.none", "none") : string.Join(", ", parts.ToArray());
        }

        // ---------------------------------------------------------------- method files

        private Method BuildMethod()
        {
            var method = new Method
            {
                Name = txtSample.Text,
                CreatedWithVersion = Program.Version,
                MinIntervalMs = (int)numMinInterval.Value,
                ScansToAverage = (int)numAverage.Value,
                Smoothing = _chkSmooth.Checked,
                SmoothingWindow = (int)_numSmoothWindow.Value,
                ShowPeaks = _chkPeaks.Checked,
                PeakProminence = (double)_numProminence.Value,
                BaselineFirstAnchor = (double)_numAnchorFirst.Value,
                BaselineSecondAnchor = (double)_numAnchorSecond.Value,
                TraceWavelengths = new List<double>(_traceWavelengths)
            };

            method.Display = _displayMode;
            method.Baseline = _baselineMode;

            for (int i = 0; i < _deviceSettings.Length && i < _devices.Length; i++)
            {
                DeviceSettings settings = _deviceSettings[i];
                method.Devices.Add(new MethodDevice
                {
                    Identity = _devices[i].Identity,
                    Index = settings.Index,
                    Model = _devices[i].ModelLabel,
                    IntegrationMs = settings.IntegrationMs,
                    TriggerDelayUs = settings.TriggerDelayUs,
                    Measured = settings.Enabled,
                    IsProbe = settings.IsProbe,
                    IsReference = settings.IsReference,
                    InCorrectionPair = settings.InCorrectionPair
                });
            }

            return method;
        }

        /// <summary>
        /// Applies a loaded method. Device values are clamped to what the spectrometer in
        /// front of us allows, so a method written for another model cannot push an
        /// out-of-range integration time at the driver.
        /// </summary>
        private void ApplyMethod(Method method)
        {
            _suppressSettingsPush = true;
            _suppressAnalysisEvents = true;
            try
            {
                if (method.MinIntervalMs >= 0)
                    numMinInterval.Value = ClampToSpinner(numMinInterval, method.MinIntervalMs);
                if (method.ScansToAverage >= 1)
                    numAverage.Value = ClampToSpinner(numAverage, method.ScansToAverage);

                _displayMode = method.Display;
                _baselineMode = method.Baseline;
                _cmbDisplay.SelectedIndex = (int)_displayMode;
                _cmbBaseline.SelectedIndex = (int)_baselineMode;
                _chkSmooth.Checked = method.Smoothing;
                _numSmoothWindow.Value = ClampToSpinner(_numSmoothWindow, method.SmoothingWindow);
                _chkPeaks.Checked = method.ShowPeaks;
                _numProminence.Value = ClampToSpinner(_numProminence, method.PeakProminence);
                _numAnchorFirst.Value = ClampToSpinner(_numAnchorFirst, method.BaselineFirstAnchor);
                _numAnchorSecond.Value = ClampToSpinner(_numAnchorSecond, method.BaselineSecondAnchor);

                _traceWavelengths.Clear();
                foreach (double wavelength in method.TraceWavelengths ?? new List<double>())
                    if (!double.IsNaN(wavelength) && !_traceWavelengths.Contains(wavelength))
                        _traceWavelengths.Add(wavelength);
                _traceWavelengths.Sort();
                RefreshTraceList();

                for (int row = 0; row < _deviceSettings.Length && row < _devices.Length; row++)
                {
                    MethodDevice saved = method.FindDevice(_devices[row].Identity, _deviceSettings[row].Index);
                    if (saved == null)
                        continue;

                    DeviceSettings settings = _deviceSettings[row];
                    settings.IntegrationMs = IntegrationLimitsFor(row).Clamp(saved.IntegrationMs);
                    settings.TriggerDelayUs = TriggerDelayLimitsFor(row).Clamp(saved.TriggerDelayUs);
                    settings.Enabled = saved.Measured;
                    settings.IsProbe = saved.IsProbe;
                    settings.IsReference = saved.IsReference;
                    settings.InCorrectionPair = saved.InCorrectionPair;
                }

                RebuildDeviceGrid();
            }
            finally
            {
                _suppressSettingsPush = false;
                _suppressAnalysisEvents = false;
            }

            UpdateAnalysisEnabled();
            UpdateRoleDerivedUi();
            ApplyChartTexts();
            RedrawAbsorption();
            _timedDirty = true;
            PushSettings();
        }

        private void SaveMethod()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = L.T("method.save", "Save measurement method");
                dialog.Filter = L.T("filter.method", "Measurement method") + " (*" + Method.Extension + ")|*" + Method.Extension;
                dialog.FileName = (string.IsNullOrWhiteSpace(txtSample.Text) ? "method" : txtSample.Text.Trim()) + Method.Extension;
                if (Directory.Exists(txtSavePath.Text))
                    dialog.InitialDirectory = txtSavePath.Text;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    MethodStore.Save(dialog.FileName, BuildMethod());
                    statusState.Text = L.F("method.saved", "Method saved: {0}", Path.GetFileName(dialog.FileName));
                }
                catch (Exception ex)
                {
                    ShowSaveError(ex);
                }
            }
        }

        private void LoadMethod()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = L.T("method.load", "Load measurement method");
                dialog.Filter = L.T("filter.method", "Measurement method") + " (*" + Method.Extension + ")|*" + Method.Extension;
                if (Directory.Exists(txtSavePath.Text))
                    dialog.InitialDirectory = txtSavePath.Text;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string error;
                Method method = MethodStore.Load(dialog.FileName, out error);
                if (method == null)
                {
                    MessageBox.Show(this, L.F("method.failed", "The method could not be read: {0}", error),
                        L.T("method.load", "Load measurement method"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ApplyMethod(method);
                statusState.Text = L.F("method.loaded", "Method loaded: {0}   (dark/white references are unchanged)",
                    Path.GetFileName(dialog.FileName));
            }
        }

        // ---------------------------------------------------------------- live recording

        /// <summary>
        /// Starts writing the timed series straight to disk. Called when recording is switched
        /// on, so a run that ends in a crash still leaves the rows that were measured.
        /// </summary>
        private void StartDiskRecording()
        {
            if (_recorder.IsRecording || _lastFrame == null || _lastFrame.Wavelengths == null)
                return;

            try
            {
                string stem = CsvExporter.BuildPath(txtSavePath.Text, txtSample.Text, "timed_live");
                _recorder.Start(stem, BuildMetadata(), _lastFrame.Wavelengths);
                statusState.Text = L.F("record.started", "Recording to {0} - rows are written as they arrive.",
                    Path.GetFileName(_recorder.AbsorbancePath));
            }
            catch (Exception ex)
            {
                statusState.Text = L.F("record.failed", "Recording to disk could not start: {0}", ex.Message);
            }
        }

        private void StopDiskRecording()
        {
            if (!_recorder.IsRecording)
                return;

            int rows = _recorder.RecordCount;
            string[] written = _recorder.Stop();
            if (written.Length > 0)
                txtLastSavedPath.Text = string.Join(" ; ", written);

            statusState.Text = L.F("record.stopped", "Recording stopped - {0} row(s) written to disk.", rows);
        }
    }
}
