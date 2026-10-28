using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using UvVisMeasurement.Driver;
using UvVisMeasurement.Io;
using UvVisMeasurement.Localization;
using UvVisMeasurement.Ui;

namespace UvVisMeasurement
{
    internal static class Program
    {
        /// <summary>
        /// Folder under %LOCALAPPDATA% holding everything this application remembers
        /// between runs: the crash log and the manually picked OOI_HOME. Named once here
        /// so the two writers cannot drift apart.
        /// </summary>
        public const string AppDataFolderName = "UV-VIS_meas";

        /// <summary>Command-line switch that starts with simulated spectrometers.</summary>
        public const string DemoSwitch = "--demo";

        private enum StartupChoice
        {
            OmniDriver,
            Demo,
            Exit
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            InstallCrashHandlers();

            // One instance per user session. OmniDriver lets only one process open a
            // spectrometer; a second copy found no device and told the operator to check
            // the USB cable, which sent them looking in the wrong place. Measured on a
            // Flame-S (FLMS18996), 2026-10-28.
            bool firstInstance;
            using (var instanceGuard = new Mutex(true, SingleInstanceMutexName, out firstInstance))
            {
                if (!firstInstance)
                {
                    ActivateRunningInstance();
                    return;
                }

                RunGuarded(args);
                GC.KeepAlive(instanceGuard);
            }
        }

        private const string SingleInstanceMutexName = @"Local\UV-VIS_meas.SingleInstance";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        /// <summary>Brings the window of the copy that is already running to the front.</summary>
        private static void ActivateRunningInstance()
        {
            const int SwRestore = 9;
            Process current = Process.GetCurrentProcess();

            foreach (Process other in Process.GetProcessesByName(current.ProcessName))
            {
                if (other.Id == current.Id || other.MainWindowHandle == IntPtr.Zero)
                    continue;

                if (IsIconic(other.MainWindowHandle))
                    ShowWindow(other.MainWindowHandle, SwRestore);

                SetForegroundWindow(other.MainWindowHandle);
                return;
            }
        }

        private static void RunGuarded(string[] args)
        {
            StartupChoice choice = HasSwitch(args, DemoSwitch) ? StartupChoice.Demo : LocateOmniDriver();
            if (choice == StartupChoice.Exit)
                return;

            SpectrometerBackend.Current = choice == StartupChoice.Demo
                ? SpectrometerBackendKind.Simulator
                : SpectrometerBackendKind.OmniDriver;

            // Before the first window, so the splash screen and every dialog are already
            // in the operator's language.
            string ignored;
            L.SetLanguage(L.ResolveStartupLanguage(UserSettingsStore.Load(SettingsPath, out ignored).Language));

            RunApplication();
        }

        /// <summary>
        /// The OmniDriver assembly must be locatable before any type that references it gets
        /// JIT compiled, so this runs before MainForm is even mentioned. RunApplication is
        /// kept in a separate non-inlined method for the same reason.
        /// </summary>
        private static StartupChoice LocateOmniDriver()
        {
            string error;
            return OmniDriverLocator.Initialize(out error) ? StartupChoice.OmniDriver : AskUserForOoiHome(error);
        }

        private static bool HasSwitch(string[] args, string name)
        {
            foreach (string arg in args ?? new string[0])
            {
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "/" + name.TrimStart('-'), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication()
        {
            Application.Run(new MainForm());
        }

        /// <summary>
        /// Read from this assembly, not from Application.ProductVersion: that one reports the
        /// process's entry EXE, which is not this program when a test or tool hosts the form.
        /// </summary>
        public static string Version
        {
            get
            {
                Assembly assembly = typeof(Program).Assembly;
                var informational = (AssemblyInformationalVersionAttribute[])assembly.GetCustomAttributes(
                    typeof(AssemblyInformationalVersionAttribute), false);

                return informational.Length > 0
                    ? informational[0].InformationalVersion
                    : assembly.GetName().Version.ToString();
            }
        }

        /// <summary>The copyright line shown in the About box, taken from the EXE's own metadata.</summary>
        public static string Copyright
        {
            get
            {
                var attributes = (AssemblyCopyrightAttribute[])typeof(Program).Assembly.GetCustomAttributes(
                    typeof(AssemblyCopyrightAttribute), false);

                return attributes.Length > 0 ? attributes[0].Copyright : string.Empty;
            }
        }

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppDataFolderName, UserSettingsStore.FileName);
            }
        }

        /// <summary>Path of the crash log written when an unhandled exception escapes.</summary>
        public static string ErrorLogPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppDataFolderName, "error.log");
            }
        }

        /// <summary>
        /// Turns the default "Microsoft .NET Framework" crash box into a logged, readable
        /// report. A spectrometer run is often unattended, so the stack trace has to land
        /// somewhere durable rather than in a dialog nobody sees.
        /// </summary>
        private static void InstallCrashHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (sender, e) => Report(e.Exception, "UI thread");
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                Report(e.ExceptionObject as Exception, "background thread");
        }

        private static void Report(Exception exception, string origin)
        {
            if (exception == null)
                return;

            string log = Log(exception, origin);

            MessageBox.Show(
                exception.Message + Environment.NewLine + Environment.NewLine +
                "Origin: " + origin + Environment.NewLine +
                (log == null ? "(the log file could not be written)" : "Details written to:" + Environment.NewLine + log),
                "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>Appends to the crash log without a dialog; returns the log path, or null.</summary>
        public static string Log(Exception exception, string origin)
        {
            try
            {
                string path = ErrorLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var text = new StringBuilder();
                text.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                "  (" + origin + ", thread " + Thread.CurrentThread.ManagedThreadId + ") ====");
                text.AppendLine("Version " + Version + ", backend " + SpectrometerBackend.Current);
                text.AppendLine(exception.ToString());
                text.AppendLine();

                File.AppendAllText(path, text.ToString(), Encoding.UTF8);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Offers a folder picker when the OmniDriver install could not be found automatically,
        /// or a start in demo mode for a PC that has no OmniDriver at all. A picked folder is
        /// remembered, so this only appears once per machine.
        /// </summary>
        private static StartupChoice AskUserForOoiHome(string error)
        {
            while (true)
            {
                DialogResult choice = MessageBox.Show(
                    error + Environment.NewLine + Environment.NewLine +
                    "Yes  -  select the OOI_HOME folder manually" + Environment.NewLine +
                    "No  -  start in demo mode with simulated spectrometers" + Environment.NewLine +
                    "Cancel  -  exit",
                    "OmniDriver not found",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

                if (choice == DialogResult.No)
                    return StartupChoice.Demo;
                if (choice != DialogResult.Yes)
                    return StartupChoice.Exit;

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the OmniDriver OOI_HOME folder " +
                                         "(the one containing NETOmniDriver-NET40.dll).";
                    dialog.ShowNewFolderButton = false;

                    if (dialog.ShowDialog() == DialogResult.OK &&
                        OmniDriverLocator.UseFolder(dialog.SelectedPath, out error))
                    {
                        return StartupChoice.OmniDriver;
                    }
                }
            }
        }
    }
}
