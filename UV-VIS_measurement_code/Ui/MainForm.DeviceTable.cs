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
    // Spectrometer table: rows, roles (Use / Probe / Ref. / Corr.) and per-device limits.
    internal partial class MainForm
    {
        // ---------------------------------------------------------------- device table

        private void OnDevicesOpened(object sender, DevicesOpenedEventArgs e)
        {
            if (_splash != null)
            {
                _splash.Close();
                _splash.Dispose();
                _splash = null;
            }

            _devices = e.Devices ?? new DeviceInfo[0];
            _driverName = e.DriverName;
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
                statusDevice.Text = L.F("status.noDevice", "No spectrometer detected ({0})", e.DriverName);
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
            UpdateShellState();

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
                            L.T("grid.noTriggerDelay", "This spectrometer does not support an external trigger delay.");
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
                lblReferenceModeState.Text = L.T("role.noneSelected", "none selected");
                lblReferenceModeState.BackColor = Color.Khaki;
            }
            else if (!RolesAvailable)
            {
                lblReferenceModeState.Text = "[" + activeIndex + "] " + L.T("role.single", "single");
                lblReferenceModeState.BackColor = SystemColors.Control;
            }
            else if (probeIndex >= 0 && referenceIndex >= 0)
            {
                lblReferenceModeState.Text = string.Format(CultureInfo.InvariantCulture,
                    "[{0}] {1}", activeIndex,
                    activeIndex == probeIndex ? L.T("role.probe", "probe")
                        : (activeIndex == referenceIndex ? L.T("role.reference", "reference")
                            : L.T("role.unassigned", "unassigned")));
                lblReferenceModeState.BackColor = activeIndex == probeIndex
                    ? Color.MediumTurquoise
                    : SystemColors.Control;
            }
            else
            {
                lblReferenceModeState.Text = "[" + activeIndex + "] " + L.T("role.noBeam", "no beam assigned");
                lblReferenceModeState.BackColor = Color.Khaki;
            }

            UpdateCaptureCaptions();

            if (_lastReferences != null)
                lblReferenceState.Text = DescribeReferences(_lastReferences);

            // The roles and the measured device are shown on the Hardware Settings tab too,
            // and the trigger combo follows whichever spectrometer is now under Use.
            _hardware.ShowRoles(_deviceSettings);
            ShowActiveTriggerMode();

            UpdateDriftUi(_lastFrame);
            UpdateActiveDeviceUi();
        }

        private void UpdateCaptureCaptions()
        {
            int activeIndex = ActiveIndex();
            string suffix = activeIndex >= 0 ? " [" + activeIndex + "]" : string.Empty;

            btnCaptureDark.Text = L.T("action.captureDark", "Capture Dark") + suffix;
            btnCaptureReference.Text = L.T("action.captureWhite", "Capture White") + suffix;
            btnClearReference.Text = L.T("action.clearRefs", "Clear Dark / White") + suffix;
        }

        /// <summary>
        /// Translates <see cref="ReferenceSnapshot.Describe"/>, which the analysis layer
        /// writes in English because it has no view of the chosen language.
        /// </summary>
        private static string DescribeReferences(ReferenceSnapshot snapshot)
        {
            string text = snapshot.Describe();
            if (!L.IsKorean)
                return text;

            return text
                .Replace("Dark: none", L.T("ref.darkNone", "Dark: none"))
                .Replace("Dark: OK", L.T("ref.darkOk", "Dark: OK"))
                .Replace("White: none", L.T("ref.whiteNone", "White: none"))
                .Replace("White: OK", L.T("ref.whiteOk", "White: OK"))
                .Replace("- no white ref.", L.T("ref.noWhite", "- no white reference"));
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
    }
}
