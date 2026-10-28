using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace UvVisMeasurement.Io
{
    /// <summary>
    /// What the operator set up last time, restored at the next start. Device rows are
    /// keyed by serial number (<see cref="Driver.DeviceInfo.Identity"/>) so the values
    /// follow the physical spectrometer, not the order the driver happens to list it in.
    ///
    /// Deliberately not stored: trigger modes and GPIO (only ever written after the driver
    /// confirms them, so a stale file must not replay them), dark / white references
    /// (only valid for the lamp and sample of the moment), and axis ranges (every run
    /// starts in autoscale, see docs/README.md section 7).
    /// </summary>
    [DataContract(Name = "UserSettings", Namespace = "")]
    internal sealed class UserSettings
    {
        [DataMember(Order = 0)] public int FormatVersion = 1;

        [DataMember(Order = 1)] public string SavePath;
        [DataMember(Order = 2)] public string Sample;
        [DataMember(Order = 3)] public string Concentration;
        [DataMember(Order = 4)] public string CellThickness;

        [DataMember(Order = 5)] public int MinIntervalMs = -1;
        [DataMember(Order = 6)] public int ScansToAverage = -1;

        [DataMember(Order = 7)] public List<double> TraceWavelengths = new List<double>();

        [DataMember(Order = 8)] public int WindowLeft;
        [DataMember(Order = 9)] public int WindowTop;
        [DataMember(Order = 10)] public int WindowWidth;
        [DataMember(Order = 11)] public int WindowHeight;
        [DataMember(Order = 12)] public bool WindowMaximized;

        [DataMember(Order = 13)] public int MainTabIndex = -1;
        [DataMember(Order = 14)] public int InnerTabIndex = -1;

        [DataMember(Order = 15)] public List<DeviceUserSettings> Devices = new List<DeviceUserSettings>();

        public bool HasWindowBounds
        {
            get { return WindowWidth > 0 && WindowHeight > 0; }
        }

        public DeviceUserSettings FindDevice(string identity)
        {
            if (Devices == null || string.IsNullOrEmpty(identity))
                return null;

            foreach (DeviceUserSettings device in Devices)
            {
                if (device != null && string.Equals(device.Identity, identity, StringComparison.Ordinal))
                    return device;
            }

            return null;
        }

        public void Remember(DeviceUserSettings device)
        {
            if (device == null || string.IsNullOrEmpty(device.Identity))
                return;

            if (Devices == null)
                Devices = new List<DeviceUserSettings>();

            Devices.RemoveAll(d => d == null || string.Equals(d.Identity, device.Identity, StringComparison.Ordinal));
            Devices.Add(device);
        }

        /// <summary>DataContractSerializer skips constructors and initialisers; this restores the defaults.</summary>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            FormatVersion = 1;
            MinIntervalMs = -1;
            ScansToAverage = -1;
            MainTabIndex = -1;
            InnerTabIndex = -1;
            TraceWavelengths = new List<double>();
            Devices = new List<DeviceUserSettings>();
        }
    }

    [DataContract(Name = "Device", Namespace = "")]
    internal sealed class DeviceUserSettings
    {
        [DataMember(Order = 0)] public string Identity;
        [DataMember(Order = 1)] public string Model;
        [DataMember(Order = 2)] public double IntegrationMs;
        [DataMember(Order = 3)] public double TriggerDelayUs;
    }

    internal static class UserSettingsStore
    {
        public const string FileName = "settings.xml";

        private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(UserSettings));

        /// <summary>
        /// Returns the stored settings, or fresh defaults when there is no file yet. A file
        /// that cannot be read is reported through <paramref name="error"/> rather than
        /// thrown: a damaged settings file must never stop the program from starting.
        /// </summary>
        public static UserSettings Load(string path, out string error)
        {
            error = null;
            if (!File.Exists(path))
                return new UserSettings();

            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(stream, new XmlDictionaryReaderQuotas()))
                {
                    return (UserSettings)Serializer.ReadObject(reader) ?? new UserSettings();
                }
            }
            catch (Exception ex)
            {
                error = "Saved settings could not be read and were reset (" + ex.Message + ").";
                return new UserSettings();
            }
        }

        /// <summary>
        /// Writes to a temporary file first and swaps it in, so a crash or power cut in the
        /// middle of a save leaves the previous settings intact instead of a truncated file.
        /// </summary>
        public static void Save(string path, UserSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";

            var xml = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (FileStream stream = File.Create(temporary))
            using (XmlWriter writer = XmlWriter.Create(stream, xml))
            {
                Serializer.WriteObject(writer, settings);
            }

            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
    }
}
