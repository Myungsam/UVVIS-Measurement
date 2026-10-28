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
    // Engine events and every plot: spectra, timed traces, 2D map.
    internal partial class MainForm
    {
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
    }
}
