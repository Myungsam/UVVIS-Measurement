using System;
using System.Collections.Generic;
using System.Text;

namespace UvVisMeasurement.Analysis
{
    /// <summary>Dark and white-reference spectra captured for one spectrometer.</summary>
    internal sealed class DeviceReference
    {
        public int Index;

        /// <summary>Dark spectrum D: shutter closed / lamp blocked.</summary>
        public double[] Dark;

        /// <summary>White reference spectrum W (I0): blank sample in the beam.</summary>
        public double[] WhiteReference;

        /// <summary>Pixel count the stored spectra belong to; a change invalidates them.</summary>
        public int PixelCount;

        public bool HasDark { get { return Dark != null; } }
        public bool HasWhiteReference { get { return WhiteReference != null; } }

        public void Clear()
        {
            Dark = null;
            WhiteReference = null;
        }

        /// <summary>Drops the stored spectra when the detector geometry changed.</summary>
        public void EnsurePixelCount(int pixelCount)
        {
            if (PixelCount == pixelCount)
                return;

            PixelCount = pixelCount;
            Clear();
        }
    }

    /// <summary>
    /// Dark and white-reference spectra, kept per spectrometer.
    ///
    /// Each spectrometer is calibrated on its own: the operator selects it in the table,
    /// captures its dark and its white reference, and those stay stored while another
    /// spectrometer is selected and calibrated in turn.
    ///
    /// Owned by the acquisition thread; the UI only receives immutable snapshots.
    /// </summary>
    internal sealed class ReferenceStore
    {
        private readonly Dictionary<int, DeviceReference> _devices = new Dictionary<int, DeviceReference>();

        /// <summary>Returns the entry for a spectrometer, creating it on first use.</summary>
        public DeviceReference For(int index)
        {
            DeviceReference device;
            if (!_devices.TryGetValue(index, out device))
            {
                device = new DeviceReference { Index = index };
                _devices[index] = device;
            }
            return device;
        }

        /// <summary>Returns the entry for a spectrometer, or null when it has none yet.</summary>
        public DeviceReference Find(int index)
        {
            DeviceReference device;
            return _devices.TryGetValue(index, out device) ? device : null;
        }

        public void Clear(int index)
        {
            DeviceReference device = Find(index);
            if (device != null)
                device.Clear();
        }

        public void ClearAll()
        {
            foreach (DeviceReference device in _devices.Values)
                device.Clear();
        }

        /// <summary>Read-only view of every spectrometer, with the active one's spectra attached.</summary>
        public ReferenceSnapshot Snapshot(int activeIndex)
        {
            var states = new List<DeviceReferenceState>(_devices.Count);
            foreach (KeyValuePair<int, DeviceReference> pair in _devices)
            {
                states.Add(new DeviceReferenceState
                {
                    Index = pair.Key,
                    HasDark = pair.Value.HasDark,
                    HasWhiteReference = pair.Value.HasWhiteReference
                });
            }
            states.Sort((a, b) => a.Index.CompareTo(b.Index));

            DeviceReference active = Find(activeIndex);

            return new ReferenceSnapshot
            {
                Devices = states.ToArray(),
                ActiveIndex = activeIndex,
                Dark = active == null ? null : Copy(active.Dark),
                WhiteReference = active == null ? null : Copy(active.WhiteReference)
            };
        }

        private static double[] Copy(double[] source)
        {
            return source == null ? null : (double[])source.Clone();
        }
    }

    internal sealed class DeviceReferenceState
    {
        public int Index;
        public bool HasDark;
        public bool HasWhiteReference;
    }

    /// <summary>Read-only view handed to the UI thread for status display and CSV export.</summary>
    internal sealed class ReferenceSnapshot
    {
        public DeviceReferenceState[] Devices = new DeviceReferenceState[0];

        public int ActiveIndex = -1;

        /// <summary>Dark spectrum of the active spectrometer, or null.</summary>
        public double[] Dark;

        /// <summary>White reference of the active spectrometer, or null.</summary>
        public double[] WhiteReference;

        public bool HasDark { get { return Dark != null; } }
        public bool HasWhiteReference { get { return WhiteReference != null; } }

        public string Describe()
        {
            if (Devices.Length == 0)
                return "Dark: none   White reference: none";

            var builder = new StringBuilder();
            for (int i = 0; i < Devices.Length; i++)
            {
                if (i > 0)
                    builder.Append("    ");

                DeviceReferenceState state = Devices[i];
                builder.Append('[').Append(state.Index).Append(']');
                if (state.Index == ActiveIndex)
                    builder.Append('*');

                builder.Append(" Dark: ").Append(state.HasDark ? "OK" : "none");
                builder.Append("  White: ").Append(state.HasWhiteReference ? "OK" : "none");
            }

            // Kept short: this shares one 460 px line with the drift-correction state.
            if (!HasWhiteReference)
                builder.Append("   - no white ref.");

            return builder.ToString();
        }
    }
}
