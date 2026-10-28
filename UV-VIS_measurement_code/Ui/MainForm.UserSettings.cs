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
    // Remembered settings and window placement (settings.xml).
    internal partial class MainForm
    {
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
    }
}
