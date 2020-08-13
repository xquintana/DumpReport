using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace DumpReport
{
    class Program
    {
        static public string configFile = Resources.configFile;
        static public string appDirectory = null;
        static public bool is32bitDump = false; // True if the dump corresponds to a 32-bit process

        static Config config = new Config(); // Stores the paramaters of the application
        static Report report = new Report(config); // Outputs extracted data to an HTML file
        static LogManager logManager = new LogManager(config, report); // Parses the debugger's output log

        static void Main(string[] args)
        {
            try
            {
                WriteTitle();

                appDirectory = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName;
                configFile = Path.Combine(appDirectory, configFile);

                // If the user just requests help, show help and quit.
                if (config.CheckHelp(args) == true)
                    return;

                // Read parameters from config file and command line
                config.ReadConfigFile(configFile);
                config.ReadCommandLine(args);

                // Create the report file
                report.Open(config.ReportFile);

                // Basic check of the input parameters
                config.CheckArguments();

                WriteConsole("Processing dump " + config.DumpFile);
                WriteConsole("Checking dump bitness...", true);
                // Find out dump bitness.
                LaunchDebugger(Resources.dbgScriptInit, config.LogFile);
                CheckDumpBitness();
                // Execute main debugger script
                WriteConsole("Creating log...", true);
                LaunchDebugger(Resources.dbgScriptMain, config.LogFile, !config.QuietMode);

                // Process debugger's output
                WriteConsole("Reading log...", true);
                logManager.ReadLog();
                logManager.ParseLog();
                logManager.CombineParserInfo();

                // If the dump reveals an exception but the details are missing, try to find them
                if (logManager.GetExceptionInfo() && logManager.NeedMoreExceptionInfo())
                {
                    FindExceptionRecord(); // Execute a new script in order to retrieve the exception record
                    logManager.GetExceptionInfo(); // Check again with the new script output
                }

                // Write the extracted information to the report file
                logManager.WriteReport();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }

            if (report.IsOpen())
            {
                report.Close();
                if (config.ReportShow && File.Exists(config.ReportFile))
                    LaunchBrowser(config.ReportFile);
            }
            WriteConsole("Finished.");
        }

        // Selects the most appropiate debugger to use depending on the current OS and the dump bitness
        public static string GetDebugger()
        {
            if (!Environment.Is64BitOperatingSystem)
                return config.DbgExe32;

            if (is32bitDump)
            {
                if (config.DbgExe32.Length > 0)
                    return config.DbgExe32;
                return config.DbgExe64;
            }
            else
            {
                if (config.DbgExe64.Length > 0)
                    return config.DbgExe64;
                return config.DbgExe32;
            }
        }

        static async Task<bool> LaunchDebuggerAsync(Process process)
        {
            return await Task.Run(() =>
            {
                return process.WaitForExit(config.DbgTimeout * 60 * 1000); // Convert minutes to milliseconds
            });
        }

        static string PreprocessScript(string script, string outFile, LogProgress progress)
        {
            // Insert the output path in the script
            script = script.Replace("{LOG_FILE}", outFile);
            // Set the proper intruction pointer register
            script = script.Replace("{INSTRUCT_PTR}", is32bitDump ? "@eip" : "@rip");
            // If enabled, insert commands used to measure progress
            if (progress != null)
                script = progress.PrepareScript(script, outFile, is32bitDump);
            return script;
        }

        // Launches the debugger, which automatically executes a script and stores the output into a file
        public static void LaunchDebugger(string script, string outFile, bool showProgress = false)
        {
            LogProgress progress = showProgress ? new LogProgress() : null;

            // Set the path of the temporary script file
            string scriptFile = Path.Combine(Path.GetTempPath(), "WinDbgScript.txt");

            // Replace special marks in the original script
            script = PreprocessScript(script, outFile, progress);

            // Remove old files
            File.Delete(scriptFile);
            File.Delete(outFile);

            // Create the script file
            using (StreamWriter stream = new StreamWriter(scriptFile))
                stream.WriteLine(script);

            // Start the debugger
            string arguments = string.Format(@"-y ""{0};srv*{1}*http://msdl.microsoft.com/download/symbols"" -z ""{2}"" -c ""$$><{3};q""",
                config.PdbFolder, config.SymbolCache, config.DumpFile, scriptFile);
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = GetDebugger(),
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden // WinDBG will only hide the main window
            };

            Process process = new Process();
            process.StartInfo = psi;
            if (!process.Start())
                throw new Exception("The debugger could not be launched.");
            Task<bool> task = LaunchDebuggerAsync(process);
            while (!task.IsCompleted)
            {
                task.Wait(500);
                if (showProgress)
                    progress.ShowLogProgress();
            }
            bool exited = task.Result;

            File.Delete(scriptFile);

            if (!exited)
            {
                process.Kill();
                throw new Exception(String.Format("Execution has been cancelled after {0} minutes.", config.DbgTimeout));
            }
            if (process.ExitCode != 0)
                throw new Exception("The debugger did not finish properly.");

            if (showProgress)
                progress.DeleteProgressFile();

            // Check that the output log has been generated
            if (!File.Exists(outFile))
                throw new Exception("The debugger did not generate any output.");
        }

        // Opens an html file with the default browser
        public static void LaunchBrowser(string htmlFile)
        {
            Process.Start(htmlFile);
        }

        // Determines whether the dump corresponds to a 32 or 64-bit process, by reading the
        // output of a script previously executed by the debugger
        public static void CheckDumpBitness()
        {
            bool x86Found = false;
            bool wow64Found = false;
            // Read the output file generated by the debugger
            using (StreamReader file = new StreamReader(config.LogFile, Encoding.Unicode))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Contains("WOW64 found"))
                        wow64Found = true;
                    else if (line.Contains("Effective machine") && line.Contains("x86"))
                        x86Found = true;
                }
                is32bitDump = (wow64Found || x86Found);
            }

            if (is32bitDump && GetDebugger() == config.DbgExe64)
                logManager.notes.Add("32-bit dump processed with a 64-bit debugger.");
            else if (!is32bitDump && GetDebugger() == config.DbgExe32)
                logManager.notes.Add("64-bit dump processed with a 32-bit debugger.");
            if (wow64Found)
                logManager.notes.Add("64-bit dumps of 32-bit processes may show inaccurate or incomplete call stack traces.");
        }

        // Tries to obtain the proper exception record by using auxiliary debugger scripts.
        public static void FindExceptionRecord()
        {
            string exrLogFile = config.LogFile;
            exrLogFile = Path.ChangeExtension(exrLogFile, ".exr.log"); // Store the output of the exception record script in a separate file
            File.Delete(exrLogFile); // Delete previous logs

            WriteConsole("Getting exception record...", true);

            string script = logManager.GetExceptionRecordScript();
            if (script != null)
                LaunchDebugger(script, exrLogFile);

            if (File.Exists(exrLogFile))
            {
                logManager.ParseExceptionRecord(exrLogFile);
                if (config.LogClean)
                    File.Delete(exrLogFile);
            }
        }

        public static void ShowError(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n" + msg);
            Console.ResetColor();
            report.WriteError(msg);
        }

        public static void WriteTitle()
        {
            if (config.QuietMode) return;
            Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Version version = assembly.GetName().Version;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(String.Format("{0} {1}.{2}", Assembly.GetCallingAssembly().GetName().Name,
                version.Major, version.Minor));
            Console.ResetColor();
        }

        public static void WriteConsole(string msg, bool sameLine = false)
        {
            if (config.QuietMode) return;
            if (sameLine)
            {
                string blank = String.Empty;
                blank = blank.PadLeft(Console.WindowWidth - 1, ' ');
                Console.Write("\r" + blank); // Clean the line before writing
                Console.Write("\r" + msg);
            }
            else
                Console.WriteLine(msg);
        }
    }
}
