using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using UvVisMeasurement.Analysis;

namespace UvVisMeasurement.Io
{
    /// <summary>
    /// A saved measurement method: the settings that define how a run is taken, so the same
    /// measurement can be repeated next week or on another PC.
    ///
    /// Deliberately not part of it: anything that belongs to one particular run rather than
    /// to the method - dark and white references, the sample name, the save folder, and the
    /// window layout. Loading a method must never make a spectrum look calibrated when it is
    /// not.
    ///
    /// Per-device settings are keyed by serial number, with the index only as a fallback, so
    /// a method still finds the right spectrometer when the driver lists them in another
    /// order. A device the method does not mention is left exactly as it is.
    /// </summary>
    [DataContract(Name = "Method", Namespace = "")]
    internal sealed class Method
    {
        public const string Extension = ".uvmethod";

        [DataMember(Order = 0)] public int FormatVersion = 1;
        [DataMember(Order = 1)] public string Name = string.Empty;
        [DataMember(Order = 2)] public string Note = string.Empty;
        [DataMember(Order = 3)] public string SavedUtc = string.Empty;
        [DataMember(Order = 4)] public string CreatedWithVersion = string.Empty;

        [DataMember(Order = 5)] public int MinIntervalMs = -1;
        [DataMember(Order = 6)] public int ScansToAverage = -1;

        [DataMember(Order = 7)] public List<double> TraceWavelengths = new List<double>();

        [DataMember(Order = 8)] public string DisplayMode = string.Empty;
        [DataMember(Order = 9)] public bool Smoothing;
        [DataMember(Order = 10)] public int SmoothingWindow = 9;
        [DataMember(Order = 11)] public string BaselineMode = string.Empty;
        [DataMember(Order = 12)] public double BaselineFirstAnchor;
        [DataMember(Order = 13)] public double BaselineSecondAnchor;
        [DataMember(Order = 14)] public bool ShowPeaks;
        [DataMember(Order = 15)] public double PeakProminence = 0.02;

        [DataMember(Order = 16)] public List<MethodDevice> Devices = new List<MethodDevice>();

        public DisplayMode Display
        {
            get { return Parse(DisplayMode, Analysis.DisplayMode.Absorbance); }
            set { DisplayMode = value.ToString(); }
        }

        public BaselineMode Baseline
        {
            get { return Parse(BaselineMode, Analysis.BaselineMode.None); }
            set { BaselineMode = value.ToString(); }
        }

        private static T Parse<T>(string text, T fallback) where T : struct
        {
            T parsed;
            return Enum.TryParse(text, true, out parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;
        }

        /// <summary>The entry for a spectrometer: by serial number, or by index when it has none.</summary>
        public MethodDevice FindDevice(string identity, int index)
        {
            if (Devices == null)
                return null;

            foreach (MethodDevice device in Devices)
            {
                if (device != null && !string.IsNullOrEmpty(identity) &&
                    string.Equals(device.Identity, identity, StringComparison.Ordinal))
                {
                    return device;
                }
            }

            foreach (MethodDevice device in Devices)
            {
                if (device != null && string.IsNullOrEmpty(device.Identity) && device.Index == index)
                    return device;
            }

            return null;
        }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            FormatVersion = 1;
            MinIntervalMs = -1;
            ScansToAverage = -1;
            SmoothingWindow = 9;
            PeakProminence = 0.02;
            TraceWavelengths = new List<double>();
            Devices = new List<MethodDevice>();
            Name = string.Empty;
            Note = string.Empty;
            SavedUtc = string.Empty;
            CreatedWithVersion = string.Empty;
            DisplayMode = string.Empty;
            BaselineMode = string.Empty;
        }
    }

    [DataContract(Name = "Device", Namespace = "")]
    internal sealed class MethodDevice
    {
        [DataMember(Order = 0)] public string Identity = string.Empty;
        [DataMember(Order = 1)] public int Index = -1;
        [DataMember(Order = 2)] public string Model = string.Empty;
        [DataMember(Order = 3)] public double IntegrationMs;
        [DataMember(Order = 4)] public double TriggerDelayUs;
        [DataMember(Order = 5)] public bool Measured;
        [DataMember(Order = 6)] public bool IsProbe;
        [DataMember(Order = 7)] public bool IsReference;
        [DataMember(Order = 8)] public bool InCorrectionPair;
    }

    internal static class MethodStore
    {
        private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(Method));

        public static void Save(string path, Method method)
        {
            if (method == null)
                throw new ArgumentNullException("method");

            method.SavedUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (FileStream stream = File.Create(path))
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                Serializer.WriteObject(writer, method);
            }
        }

        /// <summary>
        /// Reads a method file. A file that is not one, or is damaged, is reported through
        /// <paramref name="error"/> rather than thrown: picking the wrong file in a dialog is
        /// an everyday slip, not a crash.
        /// </summary>
        public static Method Load(string path, out string error)
        {
            error = null;

            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(stream, new XmlDictionaryReaderQuotas()))
                {
                    var method = (Method)Serializer.ReadObject(reader);
                    if (method == null)
                    {
                        error = "The file does not contain a measurement method.";
                        return null;
                    }

                    return method;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }
    }
}
