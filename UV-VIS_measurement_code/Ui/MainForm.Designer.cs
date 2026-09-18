namespace UvVisMeasurement.Ui
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabPageAbsorbanceFluorescence = new System.Windows.Forms.TabPage();
            this.tabInner = new System.Windows.Forms.TabControl();
            this.tabPageAbsorption = new System.Windows.Forms.TabPage();
            this.chartAbsorption = new UvVisMeasurement.Ui.SpectrumChart();
            this.tabPageFluorescence = new System.Windows.Forms.TabPage();
            this.chartFluorescence = new UvVisMeasurement.Ui.SpectrumChart();
            this.tabPageProbe = new System.Windows.Forms.TabPage();
            this.chartProbe = new UvVisMeasurement.Ui.SpectrumChart();
            this.tabPageTimed = new System.Windows.Forms.TabPage();
            this.chartTimed = new UvVisMeasurement.Ui.SpectrumChart();
            this.listTraces = new System.Windows.Forms.ListBox();
            this.panelTimedControls = new System.Windows.Forms.Panel();
            this.lblTraceWavelength = new System.Windows.Forms.Label();
            this.numTraceWavelength = new System.Windows.Forms.NumericUpDown();
            this.btnAddTrace = new System.Windows.Forms.Button();
            this.btnRemoveTrace = new System.Windows.Forms.Button();
            this.lblTraceInfo = new System.Windows.Forms.Label();
            this.tabPageTimed2D = new System.Windows.Forms.TabPage();
            this.heatmap = new UvVisMeasurement.Ui.HeatmapControl();
            this.panelHeatControls = new System.Windows.Forms.Panel();
            this.chkHeatAutoScale = new System.Windows.Forms.CheckBox();
            this.lblHeatMin = new System.Windows.Forms.Label();
            this.numHeatMin = new System.Windows.Forms.NumericUpDown();
            this.lblHeatMax = new System.Windows.Forms.Label();
            this.numHeatMax = new System.Windows.Forms.NumericUpDown();
            this.lblHeatInfo = new System.Windows.Forms.Label();
            this.tabPageHardware = new System.Windows.Forms.TabPage();
            this.panelHardware = new System.Windows.Forms.Panel();

            this.panelRight = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();

            this.grpParameters = new System.Windows.Forms.GroupBox();
            this.chkRun = new System.Windows.Forms.CheckBox();
            this.lblMinInterval = new System.Windows.Forms.Label();
            this.numMinInterval = new System.Windows.Forms.NumericUpDown();
            this.lblAverage = new System.Windows.Forms.Label();
            this.numAverage = new System.Windows.Forms.NumericUpDown();
            this.lblReferenceMode = new System.Windows.Forms.Label();
            this.lblReferenceModeState = new System.Windows.Forms.Label();
            this.lblTriggerMode = new System.Windows.Forms.Label();
            this.cmbTriggerMode = new System.Windows.Forms.ComboBox();
            this.lblDevices = new System.Windows.Forms.Label();
            this.gridDevices = new System.Windows.Forms.DataGridView();
            this.colUse = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colIndex = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colModel = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIntegration = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTriggerDelay = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProbe = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colReference = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colCorrection = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.lblAppliedDelay = new System.Windows.Forms.Label();
            this.btnCaptureDark = new System.Windows.Forms.Button();
            this.btnCaptureReference = new System.Windows.Forms.Button();
            this.btnClearReference = new System.Windows.Forms.Button();
            this.lblReferenceState = new System.Windows.Forms.Label();
            this.lblDriftState = new System.Windows.Forms.Label();

            this.grpSave = new System.Windows.Forms.GroupBox();
            this.lblSample = new System.Windows.Forms.Label();
            this.txtSample = new System.Windows.Forms.TextBox();
            this.lblConcentration = new System.Windows.Forms.Label();
            this.txtConcentration = new System.Windows.Forms.TextBox();
            this.lblCellThickness = new System.Windows.Forms.Label();
            this.txtCellThickness = new System.Windows.Forms.TextBox();
            this.lblSavePath = new System.Windows.Forms.Label();
            this.txtSavePath = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblGetFilePath = new System.Windows.Forms.Label();
            this.txtLastSavedPath = new System.Windows.Forms.TextBox();
            this.btnSaveSpectra = new System.Windows.Forms.Button();
            this.btnSaveTimedSpectra = new System.Windows.Forms.Button();

            this.grpTimedDatasets = new System.Windows.Forms.GroupBox();
            this.btnToggleTimed = new System.Windows.Forms.Button();
            this.btnClearDatasets = new System.Windows.Forms.Button();

            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusDevice = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusState = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusRate = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusDatasets = new System.Windows.Forms.ToolStripStatusLabel();

            ((System.ComponentModel.ISupportInitialize)(this.chartAbsorption)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartFluorescence)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartProbe)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartTimed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTraceWavelength)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHeatMin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHeatMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMinInterval)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numAverage)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridDevices)).BeginInit();

            this.tabMain.SuspendLayout();
            this.tabPageAbsorbanceFluorescence.SuspendLayout();
            this.tabInner.SuspendLayout();
            this.tabPageAbsorption.SuspendLayout();
            this.tabPageFluorescence.SuspendLayout();
            this.tabPageProbe.SuspendLayout();
            this.tabPageTimed.SuspendLayout();
            this.panelTimedControls.SuspendLayout();
            this.tabPageTimed2D.SuspendLayout();
            this.panelHeatControls.SuspendLayout();
            this.tabPageHardware.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.grpParameters.SuspendLayout();
            this.grpSave.SuspendLayout();
            this.grpTimedDatasets.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();

            //
            // tabMain
            //
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(884, 780);
            this.tabMain.TabIndex = 0;
            this.tabMain.Controls.Add(this.tabPageAbsorbanceFluorescence);
            this.tabMain.Controls.Add(this.tabPageProbe);
            this.tabMain.Controls.Add(this.tabPageTimed);
            this.tabMain.Controls.Add(this.tabPageTimed2D);
            this.tabMain.Controls.Add(this.tabPageHardware);

            //
            // tabPageAbsorbanceFluorescence
            //
            this.tabPageAbsorbanceFluorescence.Location = new System.Drawing.Point(4, 24);
            this.tabPageAbsorbanceFluorescence.Name = "tabPageAbsorbanceFluorescence";
            this.tabPageAbsorbanceFluorescence.Padding = new System.Windows.Forms.Padding(4);
            this.tabPageAbsorbanceFluorescence.Text = "Absorbance/Fluorescence";
            this.tabPageAbsorbanceFluorescence.UseVisualStyleBackColor = true;
            this.tabPageAbsorbanceFluorescence.Controls.Add(this.tabInner);

            //
            // tabInner
            //
            this.tabInner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabInner.Name = "tabInner";
            this.tabInner.SelectedIndex = 0;
            this.tabInner.TabIndex = 0;
            this.tabInner.Controls.Add(this.tabPageAbsorption);
            this.tabInner.Controls.Add(this.tabPageFluorescence);

            //
            // tabPageAbsorption
            //
            this.tabPageAbsorption.Name = "tabPageAbsorption";
            this.tabPageAbsorption.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageAbsorption.Text = "Absorption";
            this.tabPageAbsorption.UseVisualStyleBackColor = true;
            this.tabPageAbsorption.Controls.Add(this.chartAbsorption);

            //
            // chartAbsorption
            //
            this.chartAbsorption.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartAbsorption.Name = "chartAbsorption";
            this.chartAbsorption.TabIndex = 0;

            //
            // tabPageFluorescence
            //
            this.tabPageFluorescence.Name = "tabPageFluorescence";
            this.tabPageFluorescence.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageFluorescence.Text = "Fluorescence";
            this.tabPageFluorescence.UseVisualStyleBackColor = true;
            this.tabPageFluorescence.Controls.Add(this.chartFluorescence);

            //
            // chartFluorescence
            //
            this.chartFluorescence.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartFluorescence.Name = "chartFluorescence";
            this.chartFluorescence.TabIndex = 0;

            //
            // tabPageProbe
            //
            this.tabPageProbe.Location = new System.Drawing.Point(4, 24);
            this.tabPageProbe.Name = "tabPageProbe";
            this.tabPageProbe.Padding = new System.Windows.Forms.Padding(4);
            this.tabPageProbe.Text = "Sample / Dark / White Ref.";
            this.tabPageProbe.UseVisualStyleBackColor = true;
            this.tabPageProbe.Controls.Add(this.chartProbe);

            //
            // chartProbe
            //
            this.chartProbe.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartProbe.Name = "chartProbe";
            this.chartProbe.TabIndex = 0;

            //
            // tabPageTimed
            //
            this.tabPageTimed.Location = new System.Drawing.Point(4, 24);
            this.tabPageTimed.Name = "tabPageTimed";
            this.tabPageTimed.Padding = new System.Windows.Forms.Padding(4);
            this.tabPageTimed.Text = "Timed Absorption Series";
            this.tabPageTimed.UseVisualStyleBackColor = true;
            // Dock order: the Fill control is added first so the docked edges win.
            this.tabPageTimed.Controls.Add(this.chartTimed);
            this.tabPageTimed.Controls.Add(this.listTraces);
            this.tabPageTimed.Controls.Add(this.panelTimedControls);

            //
            // chartTimed
            //
            this.chartTimed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chartTimed.Name = "chartTimed";
            this.chartTimed.TabIndex = 2;

            //
            // listTraces
            //
            this.listTraces.Dock = System.Windows.Forms.DockStyle.Right;
            this.listTraces.FormattingEnabled = true;
            this.listTraces.IntegralHeight = false;
            this.listTraces.Name = "listTraces";
            this.listTraces.Size = new System.Drawing.Size(150, 100);
            this.listTraces.TabIndex = 1;

            //
            // panelTimedControls
            //
            this.panelTimedControls.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTimedControls.Name = "panelTimedControls";
            this.panelTimedControls.Size = new System.Drawing.Size(100, 40);
            this.panelTimedControls.TabIndex = 0;
            this.panelTimedControls.Controls.Add(this.lblTraceWavelength);
            this.panelTimedControls.Controls.Add(this.numTraceWavelength);
            this.panelTimedControls.Controls.Add(this.btnAddTrace);
            this.panelTimedControls.Controls.Add(this.btnRemoveTrace);
            this.panelTimedControls.Controls.Add(this.lblTraceInfo);

            //
            // lblTraceWavelength
            //
            this.lblTraceWavelength.AutoSize = true;
            this.lblTraceWavelength.Location = new System.Drawing.Point(8, 12);
            this.lblTraceWavelength.Name = "lblTraceWavelength";
            this.lblTraceWavelength.Text = "Trace wavelength (nm)";

            //
            // numTraceWavelength
            //
            this.numTraceWavelength.Location = new System.Drawing.Point(152, 9);
            this.numTraceWavelength.Name = "numTraceWavelength";
            this.numTraceWavelength.Size = new System.Drawing.Size(96, 23);
            this.numTraceWavelength.TabIndex = 0;

            //
            // btnAddTrace
            //
            this.btnAddTrace.Location = new System.Drawing.Point(258, 8);
            this.btnAddTrace.Name = "btnAddTrace";
            this.btnAddTrace.Size = new System.Drawing.Size(72, 25);
            this.btnAddTrace.TabIndex = 1;
            this.btnAddTrace.Text = "Add";
            this.btnAddTrace.UseVisualStyleBackColor = true;
            this.btnAddTrace.Click += new System.EventHandler(this.OnAddTraceClick);

            //
            // btnRemoveTrace
            //
            this.btnRemoveTrace.Location = new System.Drawing.Point(336, 8);
            this.btnRemoveTrace.Name = "btnRemoveTrace";
            this.btnRemoveTrace.Size = new System.Drawing.Size(80, 25);
            this.btnRemoveTrace.TabIndex = 2;
            this.btnRemoveTrace.Text = "Remove";
            this.btnRemoveTrace.UseVisualStyleBackColor = true;
            this.btnRemoveTrace.Click += new System.EventHandler(this.OnRemoveTraceClick);

            //
            // lblTraceInfo
            //
            this.lblTraceInfo.AutoSize = true;
            this.lblTraceInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblTraceInfo.Location = new System.Drawing.Point(432, 12);
            this.lblTraceInfo.Name = "lblTraceInfo";
            this.lblTraceInfo.Text = "";

            //
            // tabPageTimed2D
            //
            this.tabPageTimed2D.Location = new System.Drawing.Point(4, 24);
            this.tabPageTimed2D.Name = "tabPageTimed2D";
            this.tabPageTimed2D.Padding = new System.Windows.Forms.Padding(4);
            this.tabPageTimed2D.Text = "Timed Absorption Series (2D plot)";
            this.tabPageTimed2D.UseVisualStyleBackColor = true;
            this.tabPageTimed2D.Controls.Add(this.heatmap);
            this.tabPageTimed2D.Controls.Add(this.panelHeatControls);

            //
            // heatmap
            //
            this.heatmap.Dock = System.Windows.Forms.DockStyle.Fill;
            this.heatmap.Name = "heatmap";
            this.heatmap.TabIndex = 1;

            //
            // panelHeatControls
            //
            this.panelHeatControls.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeatControls.Name = "panelHeatControls";
            this.panelHeatControls.Size = new System.Drawing.Size(100, 40);
            this.panelHeatControls.TabIndex = 0;
            this.panelHeatControls.Controls.Add(this.chkHeatAutoScale);
            this.panelHeatControls.Controls.Add(this.lblHeatMin);
            this.panelHeatControls.Controls.Add(this.numHeatMin);
            this.panelHeatControls.Controls.Add(this.lblHeatMax);
            this.panelHeatControls.Controls.Add(this.numHeatMax);
            this.panelHeatControls.Controls.Add(this.lblHeatInfo);

            //
            // chkHeatAutoScale
            //
            this.chkHeatAutoScale.AutoSize = true;
            this.chkHeatAutoScale.Checked = true;
            this.chkHeatAutoScale.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkHeatAutoScale.Location = new System.Drawing.Point(8, 11);
            this.chkHeatAutoScale.Name = "chkHeatAutoScale";
            this.chkHeatAutoScale.TabIndex = 0;
            this.chkHeatAutoScale.Text = "Auto scale";
            this.chkHeatAutoScale.UseVisualStyleBackColor = true;
            this.chkHeatAutoScale.CheckedChanged += new System.EventHandler(this.OnHeatScaleChanged);

            //
            // lblHeatMin
            //
            this.lblHeatMin.AutoSize = true;
            this.lblHeatMin.Location = new System.Drawing.Point(112, 12);
            this.lblHeatMin.Name = "lblHeatMin";
            this.lblHeatMin.Text = "Min (OD)";

            //
            // numHeatMin
            //
            this.numHeatMin.Location = new System.Drawing.Point(180, 9);
            this.numHeatMin.Name = "numHeatMin";
            this.numHeatMin.Size = new System.Drawing.Size(84, 23);
            this.numHeatMin.TabIndex = 1;
            // Absorbance runs negative on a scattering sample, and the default 0..100
            // integer spinner could not express an OD range at all.
            this.numHeatMin.DecimalPlaces = 3;
            this.numHeatMin.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numHeatMin.Minimum = new decimal(new int[] { 100, 0, 0, -2147483648 });
            this.numHeatMin.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numHeatMin.ValueChanged += new System.EventHandler(this.OnHeatScaleChanged);

            //
            // lblHeatMax
            //
            this.lblHeatMax.AutoSize = true;
            this.lblHeatMax.Location = new System.Drawing.Point(280, 12);
            this.lblHeatMax.Name = "lblHeatMax";
            this.lblHeatMax.Text = "Max (OD)";

            //
            // numHeatMax
            //
            this.numHeatMax.Location = new System.Drawing.Point(350, 9);
            this.numHeatMax.Name = "numHeatMax";
            this.numHeatMax.Size = new System.Drawing.Size(84, 23);
            this.numHeatMax.TabIndex = 2;
            this.numHeatMax.DecimalPlaces = 3;
            this.numHeatMax.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numHeatMax.Minimum = new decimal(new int[] { 100, 0, 0, -2147483648 });
            this.numHeatMax.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.numHeatMax.Value = new decimal(new int[] { 1, 0, 0, 0 });
            this.numHeatMax.ValueChanged += new System.EventHandler(this.OnHeatScaleChanged);

            //
            // lblHeatInfo
            //
            this.lblHeatInfo.AutoSize = true;
            this.lblHeatInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHeatInfo.Location = new System.Drawing.Point(452, 12);
            this.lblHeatInfo.Name = "lblHeatInfo";
            this.lblHeatInfo.Text = "";

            //
            // tabPageHardware
            //
            this.tabPageHardware.Location = new System.Drawing.Point(4, 24);
            this.tabPageHardware.Name = "tabPageHardware";
            this.tabPageHardware.Padding = new System.Windows.Forms.Padding(4);
            this.tabPageHardware.Text = "Hardware Settings";
            this.tabPageHardware.UseVisualStyleBackColor = true;
            this.tabPageHardware.Controls.Add(this.panelHardware);

            //
            // panelHardware
            //
            // One section per spectrometer is built into this panel at run time, the same
            // way the device table is: how many there are is only known once the driver
            // has opened them.
            this.panelHardware.AutoScroll = true;
            this.panelHardware.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelHardware.Name = "panelHardware";
            this.panelHardware.TabIndex = 0;

            //
            // panelRight
            //
            this.panelRight.AutoScroll = true;
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(520, 780);
            this.panelRight.TabIndex = 1;
            this.panelRight.Controls.Add(this.lblTitle);
            this.panelRight.Controls.Add(this.grpParameters);
            this.panelRight.Controls.Add(this.grpSave);
            this.panelRight.Controls.Add(this.grpTimedDatasets);

            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline);
            this.lblTitle.Location = new System.Drawing.Point(10, 10);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "UV-VIS Measurement v2.0";

            //
            // grpParameters
            //
            this.grpParameters.Location = new System.Drawing.Point(8, 38);
            this.grpParameters.Name = "grpParameters";
            this.grpParameters.Size = new System.Drawing.Size(484, 345);
            this.grpParameters.TabIndex = 1;
            this.grpParameters.TabStop = false;
            this.grpParameters.Text = "Set parameters";
            this.grpParameters.Controls.Add(this.chkRun);
            this.grpParameters.Controls.Add(this.lblMinInterval);
            this.grpParameters.Controls.Add(this.numMinInterval);
            this.grpParameters.Controls.Add(this.lblAverage);
            this.grpParameters.Controls.Add(this.numAverage);
            this.grpParameters.Controls.Add(this.lblReferenceMode);
            this.grpParameters.Controls.Add(this.lblReferenceModeState);
            this.grpParameters.Controls.Add(this.lblTriggerMode);
            this.grpParameters.Controls.Add(this.cmbTriggerMode);
            this.grpParameters.Controls.Add(this.lblDevices);
            this.grpParameters.Controls.Add(this.gridDevices);
            this.grpParameters.Controls.Add(this.lblAppliedDelay);
            this.grpParameters.Controls.Add(this.btnCaptureDark);
            this.grpParameters.Controls.Add(this.btnCaptureReference);
            this.grpParameters.Controls.Add(this.btnClearReference);
            this.grpParameters.Controls.Add(this.lblReferenceState);
            this.grpParameters.Controls.Add(this.lblDriftState);

            //
            // chkRun
            //
            // A latching button: held in means "acquire continuously", released means stop.
            this.chkRun.Appearance = System.Windows.Forms.Appearance.Button;
            this.chkRun.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.chkRun.Location = new System.Drawing.Point(10, 22);
            this.chkRun.Name = "chkRun";
            this.chkRun.Size = new System.Drawing.Size(140, 78);
            this.chkRun.TabIndex = 0;
            this.chkRun.Text = "Measurement\r\nSTART";
            this.chkRun.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkRun.UseVisualStyleBackColor = true;
            this.chkRun.CheckedChanged += new System.EventHandler(this.OnRunToggled);

            //
            // lblMinInterval
            //
            this.lblMinInterval.AutoSize = true;
            this.lblMinInterval.Location = new System.Drawing.Point(162, 25);
            this.lblMinInterval.Name = "lblMinInterval";
            this.lblMinInterval.Text = "Set Min Interval (ms)";

            //
            // numMinInterval
            //
            this.numMinInterval.Location = new System.Drawing.Point(320, 22);
            this.numMinInterval.Name = "numMinInterval";
            this.numMinInterval.Size = new System.Drawing.Size(150, 23);
            this.numMinInterval.TabIndex = 1;

            //
            // lblAverage
            //
            this.lblAverage.AutoSize = true;
            this.lblAverage.Location = new System.Drawing.Point(162, 53);
            this.lblAverage.Name = "lblAverage";
            this.lblAverage.Text = "Average";

            //
            // numAverage
            //
            this.numAverage.Location = new System.Drawing.Point(320, 50);
            this.numAverage.Name = "numAverage";
            this.numAverage.Size = new System.Drawing.Size(150, 23);
            this.numAverage.TabIndex = 2;

            //
            // lblReferenceMode
            //
            this.lblReferenceMode.AutoSize = true;
            this.lblReferenceMode.Location = new System.Drawing.Point(162, 81);
            this.lblReferenceMode.Name = "lblReferenceMode";
            this.lblReferenceMode.Text = "Measuring";

            //
            // lblReferenceModeState
            //
            // Derived from the table, not set by the user: which spectrometer is ticked
            // under Use, and which beam it carries once two spectrometers are connected.
            this.lblReferenceModeState.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblReferenceModeState.Location = new System.Drawing.Point(320, 78);
            this.lblReferenceModeState.Name = "lblReferenceModeState";
            this.lblReferenceModeState.Size = new System.Drawing.Size(150, 23);
            this.lblReferenceModeState.Text = "OFF (stored)";
            this.lblReferenceModeState.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            //
            // lblTriggerMode
            //
            this.lblTriggerMode.AutoSize = true;
            this.lblTriggerMode.Location = new System.Drawing.Point(10, 111);
            this.lblTriggerMode.Name = "lblTriggerMode";
            this.lblTriggerMode.Text = "Trigger Mode";

            //
            // cmbTriggerMode
            //
            this.cmbTriggerMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTriggerMode.FormattingEnabled = true;
            this.cmbTriggerMode.Location = new System.Drawing.Point(166, 108);
            this.cmbTriggerMode.Name = "cmbTriggerMode";
            this.cmbTriggerMode.Size = new System.Drawing.Size(304, 23);
            this.cmbTriggerMode.TabIndex = 3;

            //
            // lblDevices
            //
            this.lblDevices.AutoSize = true;
            this.lblDevices.Location = new System.Drawing.Point(10, 140);
            this.lblDevices.Name = "lblDevices";
            this.lblDevices.Text = "Detected spectrometers - tick the one to measure under Use";

            //
            // gridDevices
            //
            this.gridDevices.AllowUserToAddRows = false;
            this.gridDevices.AllowUserToDeleteRows = false;
            this.gridDevices.AllowUserToResizeRows = false;
            this.gridDevices.AllowUserToOrderColumns = false;
            this.gridDevices.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridDevices.BackgroundColor = System.Drawing.SystemColors.Window;
            this.gridDevices.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.gridDevices.ColumnHeadersHeight = 42;
            this.gridDevices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.gridDevices.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridDevices.Location = new System.Drawing.Point(10, 159);
            this.gridDevices.MultiSelect = false;
            this.gridDevices.Name = "gridDevices";
            this.gridDevices.RowHeadersVisible = false;
            this.gridDevices.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.gridDevices.Size = new System.Drawing.Size(460, 92);
            this.gridDevices.TabIndex = 4;
            this.gridDevices.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colUse, this.colIndex, this.colModel, this.colIntegration,
                this.colTriggerDelay, this.colProbe, this.colReference, this.colCorrection});

            //
            // colUse
            //
            this.colUse.HeaderText = "Use";
            this.colUse.Name = "colUse";
            this.colUse.FillWeight = 40F;
            this.colUse.MinimumWidth = 34;
            this.colUse.ToolTipText = "Measure this spectrometer. Only one at a time; Capture Dark / White Reference store against it.";

            //
            // colIndex
            //
            this.colIndex.HeaderText = "Index";
            this.colIndex.Name = "colIndex";
            this.colIndex.ReadOnly = true;
            this.colIndex.FillWeight = 38F;
            this.colIndex.MinimumWidth = 34;

            //
            // colModel
            //
            this.colModel.HeaderText = "Spectrometer model";
            this.colModel.Name = "colModel";
            this.colModel.ReadOnly = true;
            this.colModel.FillWeight = 102F;
            this.colModel.MinimumWidth = 60;

            //
            // colIntegration
            //
            this.colIntegration.HeaderText = "Integration time (ms)";
            this.colIntegration.Name = "colIntegration";
            this.colIntegration.FillWeight = 78F;
            this.colIntegration.MinimumWidth = 56;

            //
            // colTriggerDelay
            //
            this.colTriggerDelay.HeaderText = "Trigger delay (us)";
            this.colTriggerDelay.Name = "colTriggerDelay";
            this.colTriggerDelay.FillWeight = 78F;
            this.colTriggerDelay.MinimumWidth = 56;

            //
            // colProbe
            //
            this.colProbe.HeaderText = "Probe";
            this.colProbe.Name = "colProbe";
            this.colProbe.FillWeight = 44F;
            this.colProbe.MinimumWidth = 42;

            //
            // colReference
            //
            this.colReference.HeaderText = "Ref.";
            this.colReference.Name = "colReference";
            this.colReference.FillWeight = 40F;
            this.colReference.MinimumWidth = 36;
            this.colReference.ToolTipText = "Reference beam: watches the lamp so the probe's white reference can be rescaled live. At most one, and only while exactly one probe is assigned.";

            //
            // colCorrection
            //
            this.colCorrection.HeaderText = "Corr.";
            this.colCorrection.Name = "colCorrection";
            this.colCorrection.FillWeight = 40F;
            this.colCorrection.MinimumWidth = 36;
            this.colCorrection.ToolTipText = "The two spectrometers that form the live reference correction pair. Editable from three spectrometers upwards; below that every device belongs to it.";

            //
            // lblAppliedDelay
            //
            this.lblAppliedDelay.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblAppliedDelay.Location = new System.Drawing.Point(10, 253);
            this.lblAppliedDelay.Name = "lblAppliedDelay";
            this.lblAppliedDelay.Size = new System.Drawing.Size(460, 17);
            this.lblAppliedDelay.Text = "";

            //
            // btnCaptureDark
            //
            this.btnCaptureDark.Location = new System.Drawing.Point(10, 274);
            this.btnCaptureDark.Name = "btnCaptureDark";
            this.btnCaptureDark.Size = new System.Drawing.Size(148, 27);
            this.btnCaptureDark.TabIndex = 5;
            this.btnCaptureDark.Text = "Capture Dark";
            this.btnCaptureDark.UseVisualStyleBackColor = true;
            this.btnCaptureDark.Click += new System.EventHandler(this.OnCaptureDarkClick);

            //
            // btnCaptureReference
            //
            this.btnCaptureReference.Location = new System.Drawing.Point(164, 274);
            this.btnCaptureReference.Name = "btnCaptureReference";
            this.btnCaptureReference.Size = new System.Drawing.Size(162, 27);
            this.btnCaptureReference.TabIndex = 6;
            this.btnCaptureReference.Text = "Capture White Ref.";
            this.btnCaptureReference.UseVisualStyleBackColor = true;
            this.btnCaptureReference.Click += new System.EventHandler(this.OnCaptureReferenceClick);

            //
            // btnClearReference
            //
            this.btnClearReference.Location = new System.Drawing.Point(332, 274);
            this.btnClearReference.Name = "btnClearReference";
            this.btnClearReference.Size = new System.Drawing.Size(138, 27);
            this.btnClearReference.TabIndex = 7;
            this.btnClearReference.Text = "Clear Dark/White";
            this.btnClearReference.UseVisualStyleBackColor = true;
            this.btnClearReference.Click += new System.EventHandler(this.OnClearReferenceClick);

            //
            // lblReferenceState
            //
            this.lblReferenceState.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblReferenceState.Location = new System.Drawing.Point(10, 304);
            this.lblReferenceState.Name = "lblReferenceState";
            this.lblReferenceState.Size = new System.Drawing.Size(460, 16);
            // One line only: wrapping would run into the drift-correction row below.
            this.lblReferenceState.AutoEllipsis = true;
            this.lblReferenceState.Text = "";

            //
            // lblDriftState
            //
            this.lblDriftState.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblDriftState.Location = new System.Drawing.Point(10, 322);
            this.lblDriftState.Name = "lblDriftState";
            this.lblDriftState.Size = new System.Drawing.Size(460, 16);
            this.lblDriftState.AutoEllipsis = true;
            this.lblDriftState.Text = "";

            //
            // grpSave
            //
            this.grpSave.Location = new System.Drawing.Point(8, 388);
            this.grpSave.Name = "grpSave";
            this.grpSave.Size = new System.Drawing.Size(484, 202);
            this.grpSave.TabIndex = 3;
            this.grpSave.TabStop = false;
            this.grpSave.Text = "Save datasets";
            this.grpSave.Controls.Add(this.lblSample);
            this.grpSave.Controls.Add(this.txtSample);
            this.grpSave.Controls.Add(this.lblConcentration);
            this.grpSave.Controls.Add(this.txtConcentration);
            this.grpSave.Controls.Add(this.lblCellThickness);
            this.grpSave.Controls.Add(this.txtCellThickness);
            this.grpSave.Controls.Add(this.lblSavePath);
            this.grpSave.Controls.Add(this.txtSavePath);
            this.grpSave.Controls.Add(this.btnBrowse);
            this.grpSave.Controls.Add(this.lblGetFilePath);
            this.grpSave.Controls.Add(this.txtLastSavedPath);
            this.grpSave.Controls.Add(this.btnSaveSpectra);
            this.grpSave.Controls.Add(this.btnSaveTimedSpectra);

            //
            // lblSample
            //
            this.lblSample.AutoSize = true;
            this.lblSample.Location = new System.Drawing.Point(10, 20);
            this.lblSample.Name = "lblSample";
            this.lblSample.Text = "Sample";

            //
            // txtSample
            //
            this.txtSample.Location = new System.Drawing.Point(10, 38);
            this.txtSample.Name = "txtSample";
            this.txtSample.Size = new System.Drawing.Size(132, 23);
            this.txtSample.TabIndex = 0;
            this.txtSample.Text = "test";

            //
            // lblConcentration
            //
            this.lblConcentration.AutoSize = true;
            this.lblConcentration.Location = new System.Drawing.Point(150, 20);
            this.lblConcentration.Name = "lblConcentration";
            this.lblConcentration.Text = "Concentration (mM)";

            //
            // txtConcentration
            //
            this.txtConcentration.Location = new System.Drawing.Point(150, 38);
            this.txtConcentration.Name = "txtConcentration";
            this.txtConcentration.Size = new System.Drawing.Size(140, 23);
            this.txtConcentration.TabIndex = 1;
            this.txtConcentration.Text = "TBA";

            //
            // lblCellThickness
            //
            this.lblCellThickness.AutoSize = true;
            this.lblCellThickness.Location = new System.Drawing.Point(320, 20);
            this.lblCellThickness.Name = "lblCellThickness";
            this.lblCellThickness.Text = "Cell Thickness";

            //
            // txtCellThickness
            //
            this.txtCellThickness.Location = new System.Drawing.Point(320, 38);
            this.txtCellThickness.Name = "txtCellThickness";
            this.txtCellThickness.Size = new System.Drawing.Size(150, 23);
            this.txtCellThickness.TabIndex = 2;
            this.txtCellThickness.Text = "TBA";

            //
            // lblSavePath
            //
            this.lblSavePath.AutoSize = true;
            this.lblSavePath.Location = new System.Drawing.Point(10, 66);
            this.lblSavePath.Name = "lblSavePath";
            this.lblSavePath.Text = "Save path (folder)";

            //
            // txtSavePath
            //
            this.txtSavePath.Location = new System.Drawing.Point(10, 84);
            this.txtSavePath.Name = "txtSavePath";
            this.txtSavePath.Size = new System.Drawing.Size(428, 23);
            this.txtSavePath.TabIndex = 3;

            //
            // btnBrowse
            //
            this.btnBrowse.Location = new System.Drawing.Point(444, 83);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(28, 25);
            this.btnBrowse.TabIndex = 4;
            this.btnBrowse.Text = "...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.OnBrowseClick);

            //
            // lblGetFilePath
            //
            this.lblGetFilePath.AutoSize = true;
            this.lblGetFilePath.Location = new System.Drawing.Point(10, 112);
            this.lblGetFilePath.Name = "lblGetFilePath";
            this.lblGetFilePath.Text = "Get File Path";

            //
            // txtLastSavedPath
            //
            this.txtLastSavedPath.Location = new System.Drawing.Point(10, 130);
            this.txtLastSavedPath.Name = "txtLastSavedPath";
            this.txtLastSavedPath.ReadOnly = true;
            this.txtLastSavedPath.Size = new System.Drawing.Size(462, 23);
            this.txtLastSavedPath.TabIndex = 5;

            //
            // btnSaveSpectra
            //
            this.btnSaveSpectra.Location = new System.Drawing.Point(10, 164);
            this.btnSaveSpectra.Name = "btnSaveSpectra";
            this.btnSaveSpectra.Size = new System.Drawing.Size(216, 32);
            this.btnSaveSpectra.TabIndex = 6;
            this.btnSaveSpectra.Text = "Save Spectra";
            this.btnSaveSpectra.UseVisualStyleBackColor = true;
            this.btnSaveSpectra.Click += new System.EventHandler(this.OnSaveSpectraClick);

            //
            // btnSaveTimedSpectra
            //
            this.btnSaveTimedSpectra.Location = new System.Drawing.Point(256, 164);
            this.btnSaveTimedSpectra.Name = "btnSaveTimedSpectra";
            this.btnSaveTimedSpectra.Size = new System.Drawing.Size(216, 32);
            this.btnSaveTimedSpectra.TabIndex = 7;
            this.btnSaveTimedSpectra.Text = "Save Timed Spectra";
            this.btnSaveTimedSpectra.UseVisualStyleBackColor = true;
            this.btnSaveTimedSpectra.Click += new System.EventHandler(this.OnSaveTimedSpectraClick);

            //
            // grpTimedDatasets
            //
            this.grpTimedDatasets.Location = new System.Drawing.Point(8, 595);
            this.grpTimedDatasets.Name = "grpTimedDatasets";
            this.grpTimedDatasets.Size = new System.Drawing.Size(484, 72);
            this.grpTimedDatasets.TabIndex = 4;
            this.grpTimedDatasets.TabStop = false;
            this.grpTimedDatasets.Text = "Set timed datasets";
            this.grpTimedDatasets.Controls.Add(this.btnToggleTimed);
            this.grpTimedDatasets.Controls.Add(this.btnClearDatasets);

            //
            // btnToggleTimed
            //
            this.btnToggleTimed.Location = new System.Drawing.Point(10, 26);
            this.btnToggleTimed.Name = "btnToggleTimed";
            this.btnToggleTimed.Size = new System.Drawing.Size(216, 34);
            this.btnToggleTimed.TabIndex = 0;
            this.btnToggleTimed.Text = "Save timed datasets";
            this.btnToggleTimed.UseVisualStyleBackColor = true;
            this.btnToggleTimed.Click += new System.EventHandler(this.OnToggleTimedClick);

            //
            // btnClearDatasets
            //
            this.btnClearDatasets.Location = new System.Drawing.Point(256, 26);
            this.btnClearDatasets.Name = "btnClearDatasets";
            this.btnClearDatasets.Size = new System.Drawing.Size(216, 34);
            this.btnClearDatasets.TabIndex = 1;
            this.btnClearDatasets.Text = "Clear datasets";
            this.btnClearDatasets.UseVisualStyleBackColor = true;
            this.btnClearDatasets.Click += new System.EventHandler(this.OnClearDatasetsClick);

            //
            // statusStrip
            //
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.statusDevice, this.statusState, this.statusRate, this.statusDatasets});

            this.statusDevice.Name = "statusDevice";
            this.statusDevice.Text = "Driver not initialised";
            this.statusDevice.Spring = true;
            this.statusDevice.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.statusState.Name = "statusState";
            this.statusState.Text = "Idle";
            this.statusState.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;

            this.statusRate.Name = "statusRate";
            this.statusRate.Text = "-";
            this.statusRate.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;

            this.statusDatasets.Name = "statusDatasets";
            this.statusDatasets.Text = "0 datasets";
            this.statusDatasets.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Left;

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            // Pinned so the hand-placed layout is not rescaled by the system UI font.
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.ClientSize = new System.Drawing.Size(1374, 780);
            this.MinimumSize = new System.Drawing.Size(1000, 540);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Ocean Optics - UV-VIS Measurement v2.0";
            // Fill first, then the docked edges, so the tab control receives the remainder.
            this.Controls.Add(this.tabMain);
            this.Controls.Add(this.panelRight);
            this.Controls.Add(this.statusStrip);

            ((System.ComponentModel.ISupportInitialize)(this.chartAbsorption)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartFluorescence)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartProbe)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.chartTimed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numTraceWavelength)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHeatMin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHeatMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMinInterval)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numAverage)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridDevices)).EndInit();

            this.tabMain.ResumeLayout(false);
            this.tabPageAbsorbanceFluorescence.ResumeLayout(false);
            this.tabInner.ResumeLayout(false);
            this.tabPageAbsorption.ResumeLayout(false);
            this.tabPageFluorescence.ResumeLayout(false);
            this.tabPageProbe.ResumeLayout(false);
            this.tabPageTimed.ResumeLayout(false);
            this.panelTimedControls.ResumeLayout(false);
            this.panelTimedControls.PerformLayout();
            this.tabPageTimed2D.ResumeLayout(false);
            this.panelHeatControls.ResumeLayout(false);
            this.panelHeatControls.PerformLayout();
            this.tabPageHardware.ResumeLayout(false);
            this.panelRight.ResumeLayout(false);
            this.panelRight.PerformLayout();
            this.grpParameters.ResumeLayout(false);
            this.grpParameters.PerformLayout();
            this.grpSave.ResumeLayout(false);
            this.grpSave.PerformLayout();
            this.grpTimedDatasets.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabPageAbsorbanceFluorescence;
        private System.Windows.Forms.TabControl tabInner;
        private System.Windows.Forms.TabPage tabPageAbsorption;
        private UvVisMeasurement.Ui.SpectrumChart chartAbsorption;
        private System.Windows.Forms.TabPage tabPageFluorescence;
        private UvVisMeasurement.Ui.SpectrumChart chartFluorescence;
        private System.Windows.Forms.TabPage tabPageProbe;
        private UvVisMeasurement.Ui.SpectrumChart chartProbe;
        private System.Windows.Forms.TabPage tabPageTimed;
        private UvVisMeasurement.Ui.SpectrumChart chartTimed;
        private System.Windows.Forms.ListBox listTraces;
        private System.Windows.Forms.Panel panelTimedControls;
        private System.Windows.Forms.Label lblTraceWavelength;
        private System.Windows.Forms.NumericUpDown numTraceWavelength;
        private System.Windows.Forms.Button btnAddTrace;
        private System.Windows.Forms.Button btnRemoveTrace;
        private System.Windows.Forms.Label lblTraceInfo;
        private System.Windows.Forms.TabPage tabPageTimed2D;
        private UvVisMeasurement.Ui.HeatmapControl heatmap;
        private System.Windows.Forms.Panel panelHeatControls;
        private System.Windows.Forms.CheckBox chkHeatAutoScale;
        private System.Windows.Forms.Label lblHeatMin;
        private System.Windows.Forms.NumericUpDown numHeatMin;
        private System.Windows.Forms.Label lblHeatMax;
        private System.Windows.Forms.NumericUpDown numHeatMax;
        private System.Windows.Forms.Label lblHeatInfo;
        private System.Windows.Forms.TabPage tabPageHardware;
        private System.Windows.Forms.Panel panelHardware;

        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.Label lblTitle;

        private System.Windows.Forms.GroupBox grpParameters;
        private System.Windows.Forms.CheckBox chkRun;
        private System.Windows.Forms.Label lblMinInterval;
        private System.Windows.Forms.NumericUpDown numMinInterval;
        private System.Windows.Forms.Label lblAverage;
        private System.Windows.Forms.NumericUpDown numAverage;
        private System.Windows.Forms.Label lblReferenceMode;
        private System.Windows.Forms.Label lblReferenceModeState;
        private System.Windows.Forms.Label lblTriggerMode;
        private System.Windows.Forms.ComboBox cmbTriggerMode;
        private System.Windows.Forms.Label lblDevices;
        private System.Windows.Forms.DataGridView gridDevices;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colUse;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIndex;
        private System.Windows.Forms.DataGridViewTextBoxColumn colModel;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIntegration;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTriggerDelay;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colProbe;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colReference;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colCorrection;
        private System.Windows.Forms.Label lblAppliedDelay;
        private System.Windows.Forms.Button btnCaptureDark;
        private System.Windows.Forms.Button btnCaptureReference;
        private System.Windows.Forms.Button btnClearReference;
        private System.Windows.Forms.Label lblReferenceState;
        private System.Windows.Forms.Label lblDriftState;

        private System.Windows.Forms.GroupBox grpSave;
        private System.Windows.Forms.Label lblSample;
        private System.Windows.Forms.TextBox txtSample;
        private System.Windows.Forms.Label lblConcentration;
        private System.Windows.Forms.TextBox txtConcentration;
        private System.Windows.Forms.Label lblCellThickness;
        private System.Windows.Forms.TextBox txtCellThickness;
        private System.Windows.Forms.Label lblSavePath;
        private System.Windows.Forms.TextBox txtSavePath;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label lblGetFilePath;
        private System.Windows.Forms.TextBox txtLastSavedPath;
        private System.Windows.Forms.Button btnSaveSpectra;
        private System.Windows.Forms.Button btnSaveTimedSpectra;

        private System.Windows.Forms.GroupBox grpTimedDatasets;
        private System.Windows.Forms.Button btnToggleTimed;
        private System.Windows.Forms.Button btnClearDatasets;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusDevice;
        private System.Windows.Forms.ToolStripStatusLabel statusState;
        private System.Windows.Forms.ToolStripStatusLabel statusRate;
        private System.Windows.Forms.ToolStripStatusLabel statusDatasets;
    }
}
