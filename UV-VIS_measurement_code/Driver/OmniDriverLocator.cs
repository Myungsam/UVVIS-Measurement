using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace UvVisMeasurement.Driver
{
    /// <summary>
    /// Finds the OmniDriver OOI_HOME folder and makes NETOmniDriver-NET40.dll loadable.
    ///
    /// The install path differs from machine to machine, so the folder is discovered at
    /// run time instead of being hard coded. Because the .NET assembly is a mixed-mode
    /// (C++/CLI) binary that P/Invokes into OmniDriver32.dll living in the same folder,
    /// two things have to happen before any OmniDriver type is touched:
    ///   * the folder is added to the native DLL search path (SetDllDirectory + PATH)
    ///   * an AssemblyResolve handler loads the managed assembly from that folder
    /// </summary>
    internal static class OmniDriverLocator
    {
        public const string AssemblySimpleName = "NETOmniDriver-NET40";

        private const string ProbeFileName = "NETOmniDriver-NET40.dll";
        private const string NativeProbeFileName = "OmniDriver32.dll";
        private const string OverrideFileName = "ooi_home.txt";

        private static readonly List<string> ProbedLocations = new List<string>();
        private static string _ooiHome;
        private static bool _resolverInstalled;

        /// <summary>Folder that contains NETOmniDriver-NET40.dll, or null when not resolved.</summary>
        public static string OoiHome
        {
            get { return _ooiHome; }
        }

        public static bool IsResolved
        {
            get { return _ooiHome != null; }
        }

        /// <summary>Every folder that was inspected during the last <see cref="Initialize"/> call.</summary>
        public static IEnumerable<string> SearchedLocations
        {
            get { return ProbedLocations.ToArray(); }
        }

        /// <summary>Path of the file that remembers a manually picked OOI_HOME.</summary>
        public static string OverridePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Program.AppDataFolderName);
                return Path.Combine(dir, OverrideFileName);
            }
        }

        /// <summary>
        /// Searches the well-known locations and prepares the loader. Returns false with a
        /// human readable reason when the driver cannot be used.
        /// </summary>
        public static bool Initialize(out string error)
        {
            if (IntPtr.Size != 4)
            {
                error = "This application must run as a 32-bit (x86) process because " +
                        ProbeFileName + " is an x86-only assembly. Rebuild with PlatformTarget=x86.";
                return false;
            }

            ProbedLocations.Clear();
            foreach (string candidate in EnumerateCandidates())
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string normalized;
                try
                {
                    normalized = Path.GetFullPath(candidate.Trim().Trim('"'));
                }
                catch (ArgumentException) { continue; }
                catch (NotSupportedException) { continue; }
                catch (PathTooLongException) { continue; }

                if (ProbedLocations.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    continue;
                ProbedLocations.Add(normalized);

                if (ContainsDriver(normalized))
                    return Activate(normalized, out error);
            }

            error = "Could not find " + ProbeFileName + ". Install OmniDriver (the OOI_HOME folder) " +
                    "or point the application at the folder manually." + Environment.NewLine +
                    Environment.NewLine + "Searched:" + Environment.NewLine +
                    "  " + string.Join(Environment.NewLine + "  ", ProbedLocations.ToArray());
            return false;
        }

        /// <summary>Validates a user supplied folder, activates it and remembers it for next time.</summary>
        public static bool UseFolder(string folder, out string error)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                error = "The folder does not exist.";
                return false;
            }
            if (!ContainsDriver(folder))
            {
                error = "The folder does not contain " + ProbeFileName + "." + Environment.NewLine +
                        "Select the OOI_HOME folder of your OmniDriver installation.";
                return false;
            }
            if (!Activate(folder, out error))
                return false;

            TrySaveOverride(folder);
            return true;
        }

        /// <summary>True when the folder holds the managed assembly this application binds to.</summary>
        public static bool ContainsDriver(string folder)
        {
            try
            {
                return Directory.Exists(folder) && File.Exists(Path.Combine(folder, ProbeFileName));
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static bool Activate(string folder, out string error)
        {
            if (!File.Exists(Path.Combine(folder, NativeProbeFileName)))
            {
                error = "Found " + ProbeFileName + " in \"" + folder + "\" but the native " +
                        NativeProbeFileName + " is missing from the same folder. " +
                        "The OmniDriver installation looks incomplete.";
                return false;
            }

            _ooiHome = folder;
            SetDllDirectory(folder);
            PrependToProcessPath(folder);

            if (!_resolverInstalled)
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveOmniDriverAssembly;
                _resolverInstalled = true;
            }

            try
            {
                Assembly.Load(AssemblySimpleName);
            }
            catch (Exception ex)
            {
                _ooiHome = null;
                error = "Failed to load " + ProbeFileName + " from \"" + folder + "\": " + ex.Message;
                return false;
            }

            error = null;
            return true;
        }

        private static Assembly ResolveOmniDriverAssembly(object sender, ResolveEventArgs args)
        {
            if (_ooiHome == null)
                return null;

            string simpleName;
            try
            {
                simpleName = new AssemblyName(args.Name).Name;
            }
            catch (Exception)
            {
                return null;
            }

            string path = Path.Combine(_ooiHome, simpleName + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        }

        private static IEnumerable<string> EnumerateCandidates()
        {
            // 1. Folder the user picked on a previous run.
            string saved = TryLoadOverride();
            if (saved != null)
                yield return saved;

            // 2. Environment variable written by the OmniDriver installer.
            yield return Environment.GetEnvironmentVariable("OOI_HOME");

            // 3. Registry: HKLM\SOFTWARE\Ocean Optics\<product>\Location points at the
            //    install root, with OOI_HOME directly underneath it.
            foreach (string fromRegistry in EnumerateRegistryLocations())
                yield return fromRegistry;

            // 4. Default install locations.
            string pf32 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string pf64 = Environment.GetEnvironmentVariable("ProgramW6432")
                          ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] vendorFolders = { "Ocean Optics", "OceanOptics", "Ocean Insight" };
            string[] productFolders = { "OmniDriverSPAM", "OmniDriver" };

            foreach (string root in new[] { pf32, pf64 })
            {
                if (string.IsNullOrEmpty(root))
                    continue;
                foreach (string vendor in vendorFolders)
                    foreach (string product in productFolders)
                        yield return Path.Combine(root, vendor, product, "OOI_HOME");
            }

            // 5. Anything already on PATH (the installer adds OOI_HOME there).
            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVariable))
            {
                foreach (string entry in pathVariable.Split(Path.PathSeparator))
                {
                    if (entry.IndexOf("OOI_HOME", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.IndexOf("OmniDriver", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        yield return entry;
                    }
                }
            }

            // 6. Next to the executable, for a self-contained deployment.
            yield return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static IEnumerable<string> EnumerateRegistryLocations()
        {
            var results = new List<string>();
            RegistryView[] views = { RegistryView.Registry32, RegistryView.Registry64 };
            string[] vendorKeys = { @"SOFTWARE\Ocean Optics", @"SOFTWARE\OceanOptics", @"SOFTWARE\Ocean Insight" };

            foreach (RegistryView view in views)
            {
                try
                {
                    using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    {
                        foreach (string vendorKey in vendorKeys)
                        {
                            using (RegistryKey vendor = hklm.OpenSubKey(vendorKey))
                            {
                                if (vendor == null)
                                    continue;

                                foreach (string productName in vendor.GetSubKeyNames())
                                {
                                    using (RegistryKey product = vendor.OpenSubKey(productName))
                                    {
                                        string location = product == null ? null : product.GetValue("Location") as string;
                                        if (string.IsNullOrEmpty(location))
                                            continue;

                                        results.Add(Path.Combine(location, "OOI_HOME"));
                                        results.Add(location);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            return results;
        }

        private static string TryLoadOverride()
        {
            try
            {
                string path = OverridePath;
                if (!File.Exists(path))
                    return null;

                string value = File.ReadAllText(path).Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static void TrySaveOverride(string folder)
        {
            try
            {
                string path = OverridePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, folder);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void PrependToProcessPath(string folder)
        {
            string current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (current.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            Environment.SetEnvironmentVariable("PATH", folder + Path.PathSeparator + current);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static bool Contains(this List<string> list, string value, StringComparer comparer)
        {
            foreach (string item in list)
                if (comparer.Equals(item, value))
                    return true;
            return false;
        }
    }
}
