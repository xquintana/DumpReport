using System;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace DumpReport
{
    class Program
    {
        static public string configFile = Resources.configFile;
        static public string appDirectory = null;
        static public bool   is32bitDump = false; // True if the dump corresponds to a 32-bit process

        static Config     config = new Config(); // Stores the paramaters of the application
        static Report     report = new Report(config); // Outputs extracted data to an HTML file
        static LogManager logManager = new LogManager(config, report); // Parses the debugger's output log

        static void Main(string[] args)
        {
            try
            {
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
                config.CheckParams();

                WriteConsole("Processing dump " + config.DumpFile);
                // Find out dump bitness.
                LaunchDebugger(Resources.dbgScriptInit, config.LogFile);
                CheckDumpBitness();
                // Execute main debugger script
                LaunchDebugger(Resources.GetDbgScriptMain(is32bitDump), config.LogFile);

                // Process debugger's output
                WriteConsole("Reading log...");
                logManager.ReadLog();
                logManager.ParseLog();

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

        // Launches the debugger, which automatically executes a script and stores the output into a file
        public static void LaunchDebugger(string script, string outFile)
        {
            // Create a temporary script file
            string scriptFile = Path.Combine(Path.GetTempPath(), "WinDbgScript.txt");

            // Set the path of the output file
            using (StreamWriter stream = new StreamWriter(scriptFile))
                stream.WriteLine(script.Replace("[LOG_FILE]", outFile));

            // Remove the output file from previous executions
            File.Delete(outFile);

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
            using (Process process = Process.Start(psi))
            {
                bool finished = process.WaitForExit(config.DbgTimeout * 60 * 1000);

                // Remove the temporary script file
                File.Delete(scriptFile);

                if (!finished)
                {
                    process.Kill();
                    throw new Exception("The debugger took too long to execute.");
                }
            }

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

            WriteConsole("Getting exception record...");

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
            Console.WriteLine(msg);
            report.WriteError(msg);
        }

        public static void WriteConsole(string msg)
        {
            if (config.QuietMode) return;
            Console.WriteLine(msg);
        }
    }
}
