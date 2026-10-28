using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using UvVisMeasurement.Analysis;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Localization;
using UvVisMeasurement.Io;

namespace UvVisMeasurement.Ui
{
    // Settings push to the engine and the stop / apply / restart hardware path.
    internal partial class MainForm
    {
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
                statusState.Text = L.T("status.pickDevice", "Tick the spectrometer to configure in the Use column first.");
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
                statusState.Text = L.T("problem.noDevice", "No spectrometer is connected.");
                return;
            }

            // A state read is not a change and should not narrate itself.
            if (change.Kind != HardwareChangeKind.ReadGpio)
                statusState.Text = L.F("status.applying", "Applying {0}...", change.Describe());

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
                MessageBox.Show(this, e.Describe(), L.T("dialog.hardwareFailed", "Hardware setting failed"),
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
                lblDriftState.Text = L.F("drift.blocked", "Live reference correction: {0}", blocker);
                lblDriftState.ForeColor = SystemColors.GrayText;
                return;
            }

            if (frame != null && frame.DriftCorrected)
            {
                lblDriftState.Text = string.Format(CultureInfo.InvariantCulture,
                    L.T("drift.active", "Live reference correction active: lamp x{0:F4} from [{1}]"),
                    frame.DriftMean, frame.ReferenceDeviceIndex);
                lblDriftState.ForeColor = Color.DarkGreen;
                return;
            }

            lblDriftState.Text = L.F("drift.armed",
                "Live reference correction: armed - capture dark/white on [{0}] too", ReferenceIndex());
            lblDriftState.ForeColor = SystemColors.GrayText;
        }

        private void PushSettings()
        {
            if (_devices.Length == 0)
                return;

            _engine.ApplySettings(BuildSettings());
        }
    }
}
