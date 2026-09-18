using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using UvVisMeasurement.Driver;
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

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            InstallCrashHandlers();

            // The OmniDriver assembly must be locatable before any type that references it
            // gets JIT compiled, so this runs before MainForm is even mentioned. RunApplication
            // is kept in a separate non-inlined method for the same reason.
            string error;
            if (!OmniDriverLocator.Initialize(out error) && !AskUserForOoiHome(error))
                return;

            RunApplication();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApplication()
        {
            Application.Run(new MainForm());
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

        private static string Log(Exception exception, string origin)
        {
            try
            {
                string path = ErrorLogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var text = new StringBuilder();
                text.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                "  (" + origin + ", thread " + Thread.CurrentThread.ManagedThreadId + ") ====");
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
        /// Offers a folder picker when the OmniDriver install could not be found automatically.
        /// The choice is remembered, so this only appears once per machine.
        /// </summary>
        private static bool AskUserForOoiHome(string error)
        {
            while (true)
            {
                DialogResult choice = MessageBox.Show(
                    error + Environment.NewLine + Environment.NewLine +
                    "Select the OOI_HOME folder manually?",
                    "OmniDriver not found",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (choice != DialogResult.Yes)
                    return false;

                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the OmniDriver OOI_HOME folder " +
                                         "(the one containing NETOmniDriver-NET40.dll).";
                    dialog.ShowNewFolderButton = false;

                    if (dialog.ShowDialog() != DialogResult.OK)
                        return false;

                    if (OmniDriverLocator.UseFolder(dialog.SelectedPath, out error))
                        return true;
                }
            }
        }
    }
}
