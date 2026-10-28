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
    // Button handlers: references, saving, timed datasets, traces, heat-map scale.
    internal partial class MainForm
    {
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
    }
}
