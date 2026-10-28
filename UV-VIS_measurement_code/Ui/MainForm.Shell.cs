using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Localization;

namespace UvVisMeasurement.Ui
{
    // Window frame: menu, toolbar, workflow steps, theme and the language switch.
    internal partial class MainForm
    {
        private MenuStrip _menu;
        private ToolStrip _toolbar;
        private WorkflowStrip _workflow;
        private Splitter _panelSplitter;

        private ToolStripMenuItem _menuFile, _menuMeasure, _menuView, _menuTools, _menuHelp;
        private ToolStripMenuItem _miSave, _miSaveTimed, _miChooseFolder, _miOpenFolder, _miExit;
        private ToolStripMenuItem _miOpenSpectrum, _miClearOverlays, _miExportPng, _miExportPdf, _miSaveMethod, _miLoadMethod;
        private ToolStripMenuItem _miRun, _miDark, _miWhite, _miClearRefs, _miRecord, _miClearTimed;
        private ToolStripMenuItem _miAbsorption, _miFluorescence, _miSampleTab, _miTimedTab, _miMapTab, _miHardwareTab, _miPanel;
        private ToolStripMenuItem _miLanguage, _miKorean, _miEnglish, _miOpenLog;
        private ToolStripMenuItem _miManual, _miAbout;

        private ToolStripButton _tbRun, _tbDark, _tbWhite, _tbClearRefs, _tbSave, _tbSaveTimed, _tbRecord, _tbPanel;

        private string _driverName = string.Empty;

        private void BuildShell()
        {
            int menuIcon = LogicalToDeviceUnits(16);
            int toolIcon = LogicalToDeviceUnits(20);

            BuildMenu(menuIcon);
            BuildToolbar(toolIcon);

            // The settings panel moves to the left, where the eye starts; the plot gets the
            // rest. A splitter lets the operator widen it, F9 hides it.
            panelRight.Dock = DockStyle.Left;
            panelRight.BackColor = Theme.Surface;
            _panelSplitter = new Splitter { Dock = DockStyle.Left, Width = LogicalToDeviceUnits(4), BackColor = Theme.Border };

            // The workflow steps take the place of the old title line above the parameters;
            // the product name and version are in the title bar and the About box.
            lblTitle.Visible = false;
            int stripHeight = LogicalToDeviceUnits(30);
            _workflow = new WorkflowStrip
            {
                Location = new Point(grpParameters.Left, LogicalToDeviceUnits(6)),
                Size = new Size(grpParameters.Width, stripHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Font = Font
            };
            _workflow.StepClicked += OnWorkflowStepClicked;
            panelRight.Controls.Add(_workflow);

            // Everything in the panel moves down by what the step strip took above it.
            int shift = _workflow.Bottom + LogicalToDeviceUnits(10) - grpParameters.Top;
            if (shift != 0)
            {
                foreach (Control control in new Control[] { grpParameters, grpSave, grpTimedDatasets })
                    control.Top += shift;
            }

            // Docking runs from the last control added, so these end up outermost: the menu on
            // top, the toolbar under it, then the splitter beside the settings panel.
            SuspendLayout();
            Controls.Add(_panelSplitter);
            _panelSplitter.BringToFront();
            tabMain.BringToFront();
            Controls.Add(_toolbar);
            Controls.Add(_menu);
            MainMenuStrip = _menu;
            ResumeLayout(true);

            Height += _menu.Height + _toolbar.Height;

            ApplyTheme();
            ApplyLanguage();
            L.LanguageChanged += (s, e) => ApplyLanguage();
        }

        private ToolStripMenuItem Item(IconKind? icon, int iconSize, Keys keys, EventHandler click)
        {
            var item = new ToolStripMenuItem();
            if (icon.HasValue)
                item.Image = Icons.Create(icon.Value, iconSize, Theme.Text);
            if (keys != Keys.None)
                item.ShortcutKeys = keys;
            if (click != null)
                item.Click += click;
            return item;
        }

        private void BuildMenu(int icon)
        {
            _menu = new MenuStrip
            {
                Renderer = Theme.CreateRenderer(),
                ImageScalingSize = new Size(icon, icon),
                BackColor = Theme.Card
            };

            _miSave = Item(IconKind.Save, icon, Keys.Control | Keys.S, (s, e) => btnSaveSpectra.PerformClick());
            _miSaveTimed = Item(IconKind.SaveTimed, icon, Keys.Control | Keys.Shift | Keys.S, (s, e) => btnSaveTimedSpectra.PerformClick());
            _miChooseFolder = Item(IconKind.Folder, icon, Keys.None, (s, e) => btnBrowse.PerformClick());
            _miOpenFolder = Item(null, icon, Keys.None, (s, e) => OpenFolder(txtSavePath.Text));
            _miExit = Item(null, icon, Keys.Alt | Keys.F4, (s, e) => Close());
            _miOpenSpectrum = Item(IconKind.Folder, icon, Keys.Control | Keys.O, (s, e) => OpenOverlay());
            _miClearOverlays = Item(IconKind.Clear, icon, Keys.None, (s, e) => ClearOverlays());
            _miExportPng = Item(IconKind.Save, icon, Keys.Control | Keys.P, (s, e) => ExportPlotImage());
            _miExportPdf = Item(IconKind.Manual, icon, Keys.Control | Keys.E, (s, e) => ExportReport());
            _miSaveMethod = Item(IconKind.Save, icon, Keys.None, (s, e) => SaveMethod());
            _miLoadMethod = Item(IconKind.Folder, icon, Keys.Control | Keys.M, (s, e) => LoadMethod());
            _menuFile = new ToolStripMenuItem();
            _menuFile.DropDownItems.AddRange(new ToolStripItem[]
            {
                _miSave, _miSaveTimed, new ToolStripSeparator(),
                _miExportPng, _miExportPdf, new ToolStripSeparator(),
                _miOpenSpectrum, _miClearOverlays, new ToolStripSeparator(),
                _miSaveMethod, _miLoadMethod, new ToolStripSeparator(),
                _miChooseFolder, _miOpenFolder, new ToolStripSeparator(), _miExit
            });

            _miRun = Item(IconKind.Start, icon, Keys.F5, (s, e) => ToggleRun());
            _miDark = Item(IconKind.Dark, icon, Keys.Control | Keys.D, (s, e) => btnCaptureDark.PerformClick());
            _miWhite = Item(IconKind.White, icon, Keys.Control | Keys.W, (s, e) => btnCaptureReference.PerformClick());
            _miClearRefs = Item(IconKind.Clear, icon, Keys.None, (s, e) => btnClearReference.PerformClick());
            _miRecord = Item(IconKind.Record, icon, Keys.Control | Keys.R, (s, e) => btnToggleTimed.PerformClick());
            _miClearTimed = Item(null, icon, Keys.None, (s, e) => btnClearDatasets.PerformClick());
            _menuMeasure = new ToolStripMenuItem();
            _menuMeasure.DropDownItems.AddRange(new ToolStripItem[]
            {
                _miRun, new ToolStripSeparator(), _miDark, _miWhite, _miClearRefs,
                new ToolStripSeparator(), _miRecord, _miClearTimed
            });

            _miAbsorption = Item(null, icon, Keys.Control | Keys.D1, (s, e) => ShowPlot(tabPageAbsorbanceFluorescence, tabPageAbsorption));
            _miFluorescence = Item(null, icon, Keys.Control | Keys.D2, (s, e) => ShowPlot(tabPageAbsorbanceFluorescence, tabPageFluorescence));
            _miSampleTab = Item(null, icon, Keys.Control | Keys.D3, (s, e) => ShowPlot(tabPageProbe, null));
            _miTimedTab = Item(null, icon, Keys.Control | Keys.D4, (s, e) => ShowPlot(tabPageTimed, null));
            _miMapTab = Item(null, icon, Keys.Control | Keys.D5, (s, e) => ShowPlot(tabPageTimed2D, null));
            _miHardwareTab = Item(null, icon, Keys.Control | Keys.D6, (s, e) => ShowPlot(tabPageHardware, null));
            _miPanel = Item(IconKind.Panel, icon, Keys.F9, (s, e) => TogglePanel());
            _menuView = new ToolStripMenuItem();
            _menuView.DropDownItems.AddRange(new ToolStripItem[]
            {
                _miAbsorption, _miFluorescence, _miSampleTab, _miTimedTab, _miMapTab, _miHardwareTab,
                new ToolStripSeparator(), _miPanel
            });

            _miKorean = Item(null, icon, Keys.None, (s, e) => ChangeLanguage(L.Korean));
            _miEnglish = Item(null, icon, Keys.None, (s, e) => ChangeLanguage(L.English));
            _miLanguage = new ToolStripMenuItem();
            _miLanguage.DropDownItems.AddRange(new ToolStripItem[] { _miKorean, _miEnglish });
            _miOpenLog = Item(IconKind.Folder, icon, Keys.None,
                (s, e) => OpenFolder(Path.GetDirectoryName(Program.ErrorLogPath)));
            _menuTools = new ToolStripMenuItem();
            _menuTools.DropDownItems.AddRange(new ToolStripItem[] { _miLanguage, new ToolStripSeparator(), _miOpenLog });

            _miManual = Item(IconKind.Manual, icon, Keys.F1, (s, e) => OpenManual());
            _miAbout = Item(IconKind.Info, icon, Keys.None, (s, e) => ShowAbout());
            _menuHelp = new ToolStripMenuItem();
            _menuHelp.DropDownItems.AddRange(new ToolStripItem[] { _miManual, new ToolStripSeparator(), _miAbout });

            _menu.Items.AddRange(new ToolStripItem[] { _menuFile, _menuMeasure, _menuView, _menuTools, _menuHelp });
        }

        private ToolStripButton Button(IconKind icon, int size, bool showText, EventHandler click)
        {
            var button = new ToolStripButton
            {
                Image = Icons.Create(icon, size, Theme.Text),
                DisplayStyle = showText ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                AutoToolTip = false,
                Padding = new Padding(LogicalToDeviceUnits(2))
            };
            button.Click += click;
            return button;
        }

        private void BuildToolbar(int icon)
        {
            _toolbar = new ToolStrip
            {
                Renderer = Theme.CreateRenderer(),
                ImageScalingSize = new Size(icon, icon),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(LogicalToDeviceUnits(6), LogicalToDeviceUnits(2), 0, LogicalToDeviceUnits(2)),
                BackColor = Theme.Card
            };

            _tbRun = Button(IconKind.Start, icon, true, (s, e) => ToggleRun());
            _tbDark = Button(IconKind.Dark, icon, true, (s, e) => btnCaptureDark.PerformClick());
            _tbWhite = Button(IconKind.White, icon, true, (s, e) => btnCaptureReference.PerformClick());
            _tbClearRefs = Button(IconKind.Clear, icon, false, (s, e) => btnClearReference.PerformClick());
            _tbSave = Button(IconKind.Save, icon, true, (s, e) => btnSaveSpectra.PerformClick());
            _tbSaveTimed = Button(IconKind.SaveTimed, icon, false, (s, e) => btnSaveTimedSpectra.PerformClick());
            _tbRecord = Button(IconKind.Record, icon, true, (s, e) => btnToggleTimed.PerformClick());
            _tbPanel = Button(IconKind.Panel, icon, false, (s, e) => TogglePanel());

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                _tbRun, new ToolStripSeparator(),
                _tbDark, _tbWhite, _tbClearRefs, new ToolStripSeparator(),
                _tbSave, _tbSaveTimed, _tbRecord, new ToolStripSeparator(),
                _tbPanel
            });
        }

        // ---------------------------------------------------------------- actions

        private void ToggleRun()
        {
            if (chkRun.Enabled)
                chkRun.Checked = !chkRun.Checked;
        }

        private void ShowPlot(TabPage page, TabPage inner)
        {
            tabMain.SelectedTab = page;
            if (inner != null)
                tabInner.SelectedTab = inner;
        }

        private void TogglePanel()
        {
            bool visible = !panelRight.Visible;
            panelRight.Visible = visible;
            _panelSplitter.Visible = visible;
            UpdateShellState();
        }

        private void OnWorkflowStepClicked(object sender, WorkflowStepEventArgs e)
        {
            switch (e.Step)
            {
                case WorkflowStep.Start:
                    if (!chkRun.Checked)
                        ToggleRun();
                    break;
                case WorkflowStep.Dark:
                    btnCaptureDark.PerformClick();
                    break;
                case WorkflowStep.White:
                    btnCaptureReference.PerformClick();
                    break;
                default:
                    btnSaveSpectra.PerformClick();
                    break;
            }
        }

        private void ChangeLanguage(string language)
        {
            L.SetLanguage(language);
            _userSettings.Language = language;
        }

        private void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    statusState.Text = L.F("status.folderMissing", "Folder not found: {0}", path);
                    return;
                }

                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex)
            {
                statusState.Text = L.F("status.folderOpenFailed", "Could not open the folder: {0}", ex.Message);
            }
        }

        /// <summary>
        /// The manual ships as docs\README.md next to the program (publish.ps1) and sits at the
        /// repository root during development; the first one found is opened.
        /// </summary>
        private void OpenManual()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "docs", "README.md"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "docs", "README.md")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "docs", "README.md"))
            };

            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    Process.Start(new ProcessStartInfo(candidate) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    statusState.Text = L.F("status.manualOpenFailed", "Could not open the manual: {0}", ex.Message);
                }
                return;
            }

            statusState.Text = L.T("status.manualMissing", "The user manual (docs\\README.md) was not found next to the program.");
        }

        private void ShowAbout()
        {
            using (var about = new AboutForm(_driverName))
                about.ShowDialog(this);
        }

        // ---------------------------------------------------------------- state

        /// <summary>Mirrors the panel's buttons into the menu, toolbar and workflow steps.</summary>
        private void UpdateShellState()
        {
            if (_toolbar == null)
                return;

            bool running = chkRun.Checked;
            bool canCapture = btnCaptureDark.Enabled;

            _tbRun.Enabled = _miRun.Enabled = chkRun.Enabled;
            _tbRun.Checked = running;
            _tbRun.Image = Icons.Create(running ? IconKind.Stop : IconKind.Start, _toolbar.ImageScalingSize.Width, Theme.Text);
            _tbRun.Text = _miRun.Text = running
                ? L.T("action.stop", "Stop measurement")
                : L.T("action.start", "Start measurement");

            _tbDark.Enabled = _miDark.Enabled = canCapture;
            _tbWhite.Enabled = _miWhite.Enabled = btnCaptureReference.Enabled;
            _tbClearRefs.Enabled = _miClearRefs.Enabled = btnClearReference.Enabled;
            _tbRecord.Checked = _miRecord.Checked = _accumulateTimed;
            _tbPanel.Checked = _miPanel.Checked = panelRight.Visible;

            bool hasDark = _lastReferences != null && _lastReferences.HasDark;
            bool hasWhite = _lastReferences != null && _lastReferences.HasWhiteReference;
            _workflow.SetProgress(running, hasDark, hasWhite);
        }

        private void ApplyTheme()
        {
            BackColor = Theme.Surface;
            statusStrip.Renderer = Theme.CreateRenderer();
            statusStrip.BackColor = Theme.Surface;

            foreach (GroupBox group in new[] { grpParameters, grpSave, grpTimedDatasets })
                Theme.StyleGroup(group);

            foreach (Button button in new[]
                     {
                         btnCaptureDark, btnCaptureReference, btnClearReference, btnBrowse,
                         btnSaveTimedSpectra, btnToggleTimed, btnClearDatasets, btnAddTrace, btnRemoveTrace
                     })
            {
                Theme.StyleButton(button);
            }

            Theme.StylePrimary(btnSaveSpectra);

            // The run toggle keeps its own colours (accent idle, green running) - see ApplyRunToggleStyle.
            chkRun.FlatAppearance.BorderColor = Theme.Accent;
        }

        // ---------------------------------------------------------------- language

        private void ApplyLanguage()
        {
            _miKorean.Checked = L.IsKorean;
            _miEnglish.Checked = !L.IsKorean;

            _menuFile.Text = L.T("menu.file", "&File");
            _miSave.Text = L.T("action.saveSpectrum", "Save spectrum");
            _miSaveTimed.Text = L.T("action.saveTimed", "Save timed spectra");
            _miChooseFolder.Text = L.T("menu.chooseFolder", "Choose save folder...");
            _miOpenFolder.Text = L.T("menu.openFolder", "Open save folder");
            _miExit.Text = L.T("menu.exit", "E&xit");
            _miOpenSpectrum.Text = L.T("analysis.openSpectrum", "Open spectrum...");
            _miClearOverlays.Text = L.T("analysis.clearOverlays", "Clear overlays");
            _miExportPng.Text = L.T("export.png", "Export plot as image");
            _miExportPdf.Text = L.T("export.pdf", "Export measurement report");
            _miSaveMethod.Text = L.T("method.save", "Save measurement method");
            _miLoadMethod.Text = L.T("method.load", "Load measurement method");

            _menuMeasure.Text = L.T("menu.measure", "&Measure");
            _miDark.Text = L.T("action.captureDark", "Capture Dark");
            _miWhite.Text = L.T("action.captureWhite", "Capture White");
            _miClearRefs.Text = L.T("action.clearRefs", "Clear Dark / White");
            _miRecord.Text = L.T("action.recordTimed", "Record timed datasets");
            _miClearTimed.Text = L.T("action.clearTimed", "Clear timed datasets");

            _menuView.Text = L.T("menu.view", "&View");
            _miAbsorption.Text = L.T("tab.absorption", "Absorption");
            _miFluorescence.Text = L.T("tab.fluorescence", "Fluorescence");
            _miSampleTab.Text = L.T("tab.sample", "Sample / Dark / White Ref.");
            _miTimedTab.Text = L.T("tab.timed", "Timed Absorption Series");
            _miMapTab.Text = L.T("tab.map", "Timed Absorption Series (2D plot)");
            _miHardwareTab.Text = L.T("tab.hardware", "Hardware Settings");
            _miPanel.Text = L.T("menu.panel", "Settings panel");

            _menuTools.Text = L.T("menu.tools", "&Tools");
            _miLanguage.Text = L.T("menu.language", "Language / 언어");
            _miKorean.Text = "한국어";
            _miEnglish.Text = "English";
            _miOpenLog.Text = L.T("menu.openLog", "Open log folder");

            _menuHelp.Text = L.T("menu.help", "&Help");
            _miManual.Text = L.T("menu.manual", "User manual");
            _miAbout.Text = L.T("menu.about", "About UV-VIS meas");

            _tbDark.Text = L.T("tool.dark", "Dark");
            _tbWhite.Text = L.T("tool.white", "White");
            _tbSave.Text = L.T("tool.save", "Save");
            _tbRecord.Text = L.T("tool.record", "Record");
            _tbDark.ToolTipText = _miDark.Text + " (Ctrl+D)";
            _tbWhite.ToolTipText = _miWhite.Text + " (Ctrl+W)";
            _tbClearRefs.ToolTipText = _miClearRefs.Text;
            _tbSave.ToolTipText = _miSave.Text + " (Ctrl+S)";
            _tbSaveTimed.ToolTipText = _miSaveTimed.Text + " (Ctrl+Shift+S)";
            _tbRecord.ToolTipText = _miRecord.Text + " (Ctrl+R)";
            _tbPanel.ToolTipText = _miPanel.Text + " (F9)";
            _tbRun.ToolTipText = L.T("tool.runTip", "Start / stop the measurement (F5)");

            _workflow.SetLabels(
                L.T("step.start", "Start"),
                L.T("step.dark", "Dark"),
                L.T("step.white", "White"),
                L.T("step.measure", "Measure / Save"));

            ApplyProductTitle();
            ApplyDesignerTexts();
            ApplyAnalysisTexts();
            ApplyRunToggleStyle();
            ApplyTimedToggleStyle();
            ApplyChartTexts();
            UpdateRoleDerivedUi();
            if (_lastReferences != null)
                lblReferenceState.Text = DescribeReferences(_lastReferences);
            _hardware.Build(_devices, _deviceSettings);
            UpdateShellState();
        }

        private void ApplyDesignerTexts()
        {
            tabPageAbsorbanceFluorescence.Text = L.T("tab.absFluo", "Absorbance/Fluorescence");
            tabPageAbsorption.Text = L.T("tab.absorption", "Absorption");
            tabPageFluorescence.Text = L.T("tab.fluorescence", "Fluorescence");
            tabPageProbe.Text = L.T("tab.sample", "Sample / Dark / White Ref.");
            tabPageTimed.Text = L.T("tab.timed", "Timed Absorption Series");
            tabPageTimed2D.Text = L.T("tab.map", "Timed Absorption Series (2D plot)");
            tabPageHardware.Text = L.T("tab.hardware", "Hardware Settings");

            lblTraceWavelength.Text = L.T("timed.traceWavelength", "Trace wavelength (nm)");
            btnAddTrace.Text = L.T("timed.add", "Add");
            btnRemoveTrace.Text = L.T("timed.remove", "Remove");
            chkHeatAutoScale.Text = L.T("map.autoScale", "Auto scale");

            grpParameters.Text = L.T("panel.parameters", "Set parameters");
            lblMinInterval.Text = L.T("panel.minInterval", "Set Min Interval (ms)");
            lblAverage.Text = L.T("panel.average", "Average");
            lblReferenceMode.Text = L.T("panel.measuring", "Measuring");
            lblTriggerMode.Text = L.T("panel.triggerMode", "Trigger Mode");
            lblDevices.Text = L.T("panel.devices", "Detected spectrometers - tick the one to measure under Use");

            colUse.HeaderText = L.T("grid.use", "Use");
            colIndex.HeaderText = L.T("grid.index", "Index");
            colModel.HeaderText = L.T("grid.model", "Spectrometer model");
            colIntegration.HeaderText = L.T("grid.integration", "Integration time (ms)");
            colTriggerDelay.HeaderText = L.T("grid.triggerDelay", "Trigger delay (us)");
            colProbe.HeaderText = L.T("grid.probe", "Probe");
            colReference.HeaderText = L.T("grid.reference", "Ref.");
            colCorrection.HeaderText = L.T("grid.correction", "Corr.");

            grpSave.Text = L.T("panel.save", "Save datasets");
            lblSample.Text = L.T("panel.sample", "Sample");
            lblConcentration.Text = L.T("panel.concentration", "Concentration (mM)");
            lblCellThickness.Text = L.T("panel.cellThickness", "Cell Thickness");
            lblSavePath.Text = L.T("panel.savePath", "Save path (folder)");
            lblGetFilePath.Text = L.T("panel.lastSaved", "Last saved file");
            btnSaveSpectra.Text = L.T("action.saveSpectrum", "Save spectrum");
            btnSaveTimedSpectra.Text = L.T("action.saveTimed", "Save timed spectra");

            grpTimedDatasets.Text = L.T("panel.timed", "Timed datasets");
            btnClearDatasets.Text = L.T("action.clearTimed", "Clear timed datasets");

            UpdateCaptureCaptions();
        }

        private void ApplyChartTexts()
        {
            string wavelength = L.T("axis.wavelength", "Wavelength (nm)");
            SetAxisTitles(chartAbsorption, wavelength, AbsorptionAxisTitle());
            SetAxisTitles(chartFluorescence, wavelength, L.T("axis.intensity", "Intensity (counts)"));
            SetAxisTitles(chartProbe, wavelength, L.T("axis.intensity", "Intensity (counts)"));
            SetAxisTitles(chartTimed, L.T("axis.time", "Time (s)"), L.T("axis.absorbance", "Absorbance (OD)"));

            // Series names stay English: they head the columns of "Copy Spectrum Data", which
            // is machine input like the CSV files. Only the legend is translated.
            foreach (Chart chart in new Chart[] { chartAbsorption, chartFluorescence, chartProbe })
            {
                foreach (Series series in chart.Series)
                    series.LegendText = LegendTextFor(series.Name);
            }

            heatmap.SetAxisTitles(L.T("axis.time", "Time (s)"), wavelength);
            heatmap.Invalidate();
        }

        /// <summary>The Y axis of the absorption plot names whichever quantity is on display.</summary>
        private string AbsorptionAxisTitle()
        {
            switch (_displayMode)
            {
                case Analysis.DisplayMode.Transmittance:
                    return L.T("axis.transmittance", "Transmittance (%T)");
                case Analysis.DisplayMode.Counts:
                    return L.T("axis.intensity", "Intensity (counts)");
                default:
                    return L.T("axis.absorbance", "Absorbance (OD)");
            }
        }

        private static void SetAxisTitles(Chart chart, string x, string y)
        {
            if (chart.ChartAreas.Count == 0)
                return;

            chart.ChartAreas[0].AxisX.Title = x;
            chart.ChartAreas[0].AxisY.Title = y;
        }

        private static string LegendTextFor(string seriesName)
        {
            switch (seriesName)
            {
                case "Absorbance": return L.T("legend.absorbance", "Absorbance");
                case "Sample": return L.T("legend.sample", "Sample");
                case "Dark": return L.T("legend.dark", "Dark");
                case "White reference": return L.T("legend.white", "White reference");
                case "Intensity": return L.T("legend.intensity", "Intensity");
                default: return seriesName;
            }
        }
    }
}
