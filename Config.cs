using System;
using System.Xml;
using System.IO;

namespace DumpReport
{
    /// <summary>
    /// Contains the input parameters, specified from command line and from the XML configuration file.
    /// </summary>
    class Config
    {
        public string DbgExe64 { get; set; }        // Full path of the 64-bit version debugger
        public string DbgExe32 { get; set; }        // Full path of the 32-bit version debugger
        public Int32 DbgTimeout { get; set; }       // Maximum number of minutes to wait for the debugger to finish
        public string StyleFile { get; set; }       // Full path of a custom CSS file to use
        public string ReportFile { get; set; }      // Full path of the report to be created
        public bool ReportShow { get; set; }        // If true, the report will be displayed automatically in the default browser
        public bool QuietMode { get; set; }         // If true. the application will not show progress messages in the console
        public string SymbolCache { get; set; }     // Folder to use as the debugger's symbol cache
        public string DumpFile { get; set; }        // Full path of the DMP file
        public string PdbFolder { get; set; }       // Folder where the PDBs are located
        public string LogFile { get; set; }         // Full path of the debugger's output file
        public string LogFolder { get; set; }       // Folder where the debugger's output file is stored
        public bool LogClean { get; set; }          // If true, log files are deleted after execution
        public string SourceCodeRoot { get; set; }  // Specifies a root folder for the source files

        public Config()
        {
            DbgExe64 = String.Empty;
            DbgExe32 = String.Empty;
            DbgTimeout = 60;
            StyleFile = String.Empty;
            ReportFile = "DumpReport.html";
            ReportShow = false;
            QuietMode = false;
            SymbolCache = "";
            DumpFile = String.Empty;
            PdbFolder = String.Empty;
            LogFolder = String.Empty;
            LogFile = String.Empty;
            SourceCodeRoot = String.Empty;
        }

        // If the user requested for help, displays the help in the console and returns true.
        // Otherwise returns false.
        public bool CheckHelp(string[] args)
        {
            if (args.Length == 0 || (args.Length == 1 && args[0] == "/?"))
                return PrintAppHelp();

            for (int idx = 0; idx < args.Length; idx++)
            {
                if (args[idx] == "/CONFIG")
                {
                    if ((idx + 1 < args.Length) && args[idx + 1] == "HELP")
                        return PrintConfigHelp();
                    if ((idx + 1 < args.Length) && args[idx + 1] == "CREATE")
                        return CreateConfigFile();
                    throw new ArgumentException("Use /CONFIG with HELP or CREATE");
                }
                if (args[idx] == "/STYLE")
                {
                    if ((idx + 1 < args.Length) && args[idx + 1] == "HELP")
                        return PrintStyleHelp();
                    if ((idx + 1 < args.Length) && args[idx + 1] == "CREATE")
                        return CreateCSS();
                    throw new ArgumentException("Use /STYLE with HELP or CREATE");
                }
            }
            return false;
        }

        // Reads the parameters from the configuration file
        public void ReadConfigFile(string configPath)
        {
            string value;

            if (!File.Exists(configPath))
                throw new Exception("Configuration file does not exist.\nPlease run 'DumpReport /CONFIG CREATE' to create it.");
            try
            {
                using (XmlReader reader = XmlReader.Create(configPath))
                {
                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name)
                            {
                                case "Debugger":
                                    value = reader["exe64"];
                                    if (value != null && value.Length > 0)
                                        DbgExe64 = value;
                                    value = reader["exe32"];
                                    if (value != null && value.Length > 0)
                                        DbgExe32 = value;
                                    value = reader["timeout"];
                                    if (value != null && value.Length > 0)
                                        DbgTimeout = Convert.ToInt32(value);
                                    break;
                                case "Pdb":
                                    value = reader["folder"];
                                    if (value != null && value.Length > 0)
                                        PdbFolder = value;
                                    break;
                                case "Style":
                                    value = reader["file"];
                                    if (value != null && value.Length > 0)
                                        StyleFile = value;
                                    break;
                                case "Report":
                                    value = reader["file"];
                                    if (value != null && value.Length > 0)
                                        ReportFile = value;
                                    value = reader["show"];
                                    if (value != null && value.Length > 0)
                                        ReportShow = (value == "1");
                                    break;
                                case "Log":
                                    value = reader["folder"];
                                    if (value != null && value.Length > 0)
                                        LogFolder = value;
                                    value = reader["clean"];
                                    if (value != null && value.Length > 0)
                                        LogClean = Convert.ToInt32(value) == 1;
                                    break;
                                case "SymbolCache":
                                    value = reader["folder"];
                                    if (value != null && value.Length > 0)
                                        SymbolCache = value;
                                    break;
                                case "SourceCodeRoot":
                                    value = reader["folder"];
                                    if (value != null && value.Length > 0)
                                        SourceCodeRoot = value.ToUpper();
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw new Exception("Configuration file contains errors.\nPlease run 'DumpReport /CONFIG HELP' for XML syntax.");
            }
        }

        // Reads the parameters from the command-line
        public void ReadCommandLine(string[] args)
        {
            if (args.Length == 1 && args[0][0] != '/')
            {
                DumpFile = args[0];
                return;
            }
            try
            {
                for (int idx = 0; idx < args.Length; idx++)
                {
                    if (args[idx] == "/DUMPFILE") DumpFile = GetParamValue(args, ref idx);
                    else if (args[idx] == "/PDBFOLDER") PdbFolder = GetParamValue(args, ref idx);
                    else if (args[idx] == "/REPORTFILE") ReportFile = GetParamValue(args, ref idx);
                    else if (args[idx] == "/SHOWREPORT") ReportShow = (GetParamValue(args, ref idx) == "1");
                    else if (args[idx] == "/QUIET") QuietMode = (GetParamValue(args, ref idx) == "1");
                    else throw new ArgumentException("Invalid parameter " + args[idx]);
                }
                if (DumpFile.Length == 0)
                    throw new ArgumentException("/DUMPFILE parameter not found");
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException(ex.Message + "\r\nPlease type 'DumpReport' for help.");
            }
        }

        // Retrieves the value from the pair '/PARAMETER value'
        string GetParamValue(string[] args, ref int idx)
        {
            if (idx + 1 >= args.Length || args[idx + 1].Length == 0 || args[idx + 1][0] == '/')
                throw new ArgumentException("Value not found for parameter " + args[idx]);
            return args[++idx];
        }

        public void CheckDebugger(string dbgFullPath, string bitness)
        {
            if (dbgFullPath.Length > 0)
            {
                if (!File.Exists(dbgFullPath))
                    throw new ArgumentException(String.Format("{0} debugger not found: {1}", bitness, dbgFullPath));
                string debugger = Path.GetFileName(dbgFullPath).ToLower();
                if (debugger != "windbg.exe" && debugger != "cdb.exe")
                    throw new ArgumentException(String.Format("Wrong {0} debugger ('{1}'). Only 'WinDBG.exe' or 'CDB.exe' are supported.", bitness, debugger));
            }
        }

        // Checks that the files and folders exist and sets all paths to absolute paths.
        public void CheckArguments()
        {
            // Check dump file path.
            DumpFile = Utils.GetAbsolutePath(DumpFile);
            if (Path.GetExtension(DumpFile).ToUpper() != ".DMP")
                throw new Exception("Only dump files (*.dmp) are supported.");
            if (!File.Exists(DumpFile))
                throw new ArgumentException("Dump file not found: " + DumpFile);

            // Check pdb file path.
            if (PdbFolder.Length == 0)
                PdbFolder = Path.GetDirectoryName(DumpFile);
            else
                PdbFolder = Utils.GetAbsolutePath(PdbFolder);
            if (!Directory.Exists(PdbFolder))
                throw new ArgumentException("PDB folder not found: " + PdbFolder);

            // Check debugger paths.
            DbgExe64 = Utils.GetAbsolutePath(DbgExe64);
            DbgExe32 = Utils.GetAbsolutePath(DbgExe32);
            if (DbgExe64.Length == 0 && DbgExe32.Length == 0)
                throw new ArgumentException("No debuggers specified in the configuration file.\r\nPlease type 'DumpReport /CONFIG HELP' for help.");
            if (!Environment.Is64BitOperatingSystem && DbgExe32.Length == 0)
                throw new Exception("The attribute 'exe32' must be set on 32-bit computers.");
            CheckDebugger(DbgExe64, "64-bit");
            CheckDebugger(DbgExe32, "32-bit");

            // Check style file.
            StyleFile = Utils.GetAbsolutePath(StyleFile);
            if (StyleFile.Length > 0 && !File.Exists(StyleFile))
                throw new ArgumentException("Style file (CSS) not found: " + StyleFile);

            // Check log file.
            LogFolder = Utils.GetAbsolutePath(LogFolder);
            if (LogFolder.Length > 0)
            {
                if (!Directory.Exists(LogFolder))
                    throw new ArgumentException("Invalid log folder " + LogFolder);
                LogFile = Path.Combine(LogFolder, Path.GetFileName(DumpFile) + ".log");
            }
            else
                LogFile = DumpFile + ".log";

            // Check symbol cache.
            SymbolCache = Utils.GetAbsolutePath(SymbolCache);
            if (SymbolCache.Length > 0 && !Directory.Exists(SymbolCache))
                throw new ArgumentException("Symbol cache folder not found: " + SymbolCache);

            // Make sure the report file contains a full path.
            ReportFile = Utils.GetAbsolutePath(ReportFile);
        }

        static void PrintColor(string line, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ResetColor();
        }

        // Prints the application usage to the console
        static bool PrintAppHelp()
        {
            Console.WriteLine(string.Format(Resources.appHelp, Path.GetFileName(Program.configFile)));
            return true;
        }

        // Prints the configuration file syntax to the console
        static bool PrintConfigHelp()
        {
            Console.WriteLine(string.Format(Resources.xmlHelpIntro, Path.GetFileName(Program.configFile)));
            PrintColor("\r\nSample:\r\n", ConsoleColor.White);
            Console.WriteLine(Resources.xml);
            PrintColor("Nodes:", ConsoleColor.White);
            Console.WriteLine(Resources.xmlHelpNodes);
            return true;
        }

        // Prints the CSS file syntax to the console
        static bool PrintStyleHelp()
        {
            Console.WriteLine(Resources.cssHelp);
            return true;
        }

        // Creates an empty configuration file.
        static bool CreateConfigFile()
        {
            string file = Program.configFile;
            if (File.Exists(file))
            {
                Console.Write("File already exists. Overwrite? [Y/N] > ");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                    return true;
                Console.WriteLine();
            }

            using (StreamWriter stream = new StreamWriter(file))
                stream.WriteLine(Resources.xml);
            Console.WriteLine("Configuration file created.\nPlease edit the path to the debuggers (WinDBG.exe or CDB.exe).");
            return true;
        }

        // Creates a default CSS file (style.css).
        static bool CreateCSS()
        {
            string file = Utils.GetAbsolutePath("style.css");
            if (File.Exists(file))
            {
                Console.Write("File " + file + " already exists. Overwrite? [Y/N] > ");
                if (Console.ReadKey().Key != ConsoleKey.Y)
                    return true;
                Console.WriteLine();
            }
            using (StreamWriter stream = new StreamWriter(file))
                stream.WriteLine(Resources.css);
            Console.WriteLine("File " + file + " has been created.\nEdit the <Style> entry in the XML configuration file in order to use it.");
            return true;
        }
    }
}
