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
        public string DbgExe64 { get; set; } // Full path of the 64-bit version debugger
        public string DbgExe32 { get; set; } // Full path of the 32-bit version debugger
        public Int32  DbgTimeout { get; set; } // Maximum number of minutes to wait for the debugger to finish
        public bool   DbgVisible { get; set; } // Runs the debugger in visible or hidden mode (only supported by CDB)
        public string StyleFile { get; set; } //Full path of a custom CSS file to use
        public string ReportFile { get; set; } // Full path of the report to be created
        public bool   ReportShow { get; set; } // If true, the report will be displayed automatically in the default browser
        public bool   QuietMode { get; set; }  // If true. the application will not show progress messages in the console
        public string SymbolCache { get; set; } // Folder to use as the debugger's symbol cache
        public string DumpFile { get; set; } // Full path of the DMP file
        public string PdbFolder { get; set; } // Folder where the PDBs are located
        public string LogFile { get; set; } // Full path of the debugger's output file
        public string LogFolder { get; set; } // Folder where the debugger's output file is stored
        public bool   LogClean { get; set; } // If true, log files are deleted after execution
        public string SourceCodeRoot { get; set; } // Specifies a root folder for the source files

        public Config()
        {
            DbgExe64 = String.Empty;
            DbgExe32 = String.Empty;
            DbgTimeout = 10;
            DbgVisible = true;
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
            ReportFile = Path.Combine(Program.appDirectory, ReportFile);

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
                                    value = reader["visible"];
                                    if (value != null && value.Length > 0)
                                        DbgVisible = (value == "1");
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
                                        ReportFile = Utils.GetAbsolutePath(value);
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
            if (args.Length == 1 && (Path.GetExtension(args[0]).ToUpper() == ".DMP"))
            {
                if (File.Exists(args[0]))
                    DumpFile = args[0];
                else
                    throw new ArgumentException("Dump file '" + args[0] + "' not found");
                return;
            }
            for (int idx = 0; idx < args.Length; idx++)
            {
                if (args[idx] == "/DUMPFILE")        DumpFile = GetParamValue(args, ref idx);
                else if (args[idx] == "/PDBFOLDER")  PdbFolder = GetParamValue(args, ref idx);
                else if (args[idx] == "/REPORTFILE") ReportFile = GetParamValue(args, ref idx);
                else if (args[idx] == "/SHOWREPORT") ReportShow = (GetParamValue(args, ref idx) == "1");
                else if (args[idx] == "/QUIET")      QuietMode = (GetParamValue(args, ref idx) == "1");
            }
            if (DumpFile.Length == 0)
                throw new ArgumentException("/DUMPFILE parameter not found");
        }

        // Retrieves the value from the pair '/PARAMETER value'
        string GetParamValue(string[] args, ref int idx)
        {
            if (idx + 1 >= args.Length || args[idx + 1].Length == 0 || args[idx + 1][0] == '/')
                throw new ArgumentException("Value not found for parameter " + args[idx]);
            return args[++idx];
        }

        // Checks that the files and folders exist and sets all paths to absolute paths.
        public void CheckParams()
        {
            if (!File.Exists(DumpFile))
                throw new ArgumentException("Dump file not found: " + DumpFile);
            if (DbgExe64.Length == 0 && DbgExe32.Length == 0)
                throw new ArgumentException("Debuggers not specified.");
            if (!Environment.Is64BitOperatingSystem && DbgExe32.Length == 0)
                throw new Exception("The attribute 'exe32' must be set on 32-bit computers.");
            if (DbgExe64.Length > 0 && !File.Exists(DbgExe64))
                throw new ArgumentException("64-bit debugger not found: " + DbgExe64);
            if (DbgExe32.Length > 0 && !File.Exists(DbgExe32))
                throw new ArgumentException("32-bit debugger not found: " + DbgExe32);
            if (PdbFolder.Length > 0 && !Directory.Exists(PdbFolder))
                throw new ArgumentException("PDB folder not found: " + PdbFolder);
            if (PdbFolder.Length == 0)
                PdbFolder = Path.GetDirectoryName(DumpFile);
            if (StyleFile.Length > 0 && !File.Exists(StyleFile))
                throw new ArgumentException("Style file (CSS) not found: " + StyleFile);
            if (LogFolder.Length > 0)
            {
                if (!Directory.Exists(LogFolder))
                    throw new ArgumentException("Invalid log folder " + LogFolder);
                LogFile = Path.Combine(LogFolder, Path.GetFileName(DumpFile) + ".log");
            }
            else
                LogFile = DumpFile + ".log";
            if (SymbolCache.Length > 0 && !Directory.Exists(SymbolCache))
                throw new ArgumentException("Symbol cache folder not found: " + SymbolCache);

            // Set absolute paths
            DbgExe64 = Utils.GetAbsolutePath(DbgExe64);
            DbgExe32 = Utils.GetAbsolutePath(DbgExe32);
            StyleFile = Utils.GetAbsolutePath(StyleFile);
            ReportFile = Utils.GetAbsolutePath(ReportFile);
            SymbolCache = Utils.GetAbsolutePath(SymbolCache);
            DumpFile = Utils.GetAbsolutePath(DumpFile);
            PdbFolder = Utils.GetAbsolutePath(PdbFolder);
            LogFile = Utils.GetAbsolutePath(LogFile);
            LogFolder = Utils.GetAbsolutePath(LogFolder);
        }

        // Prints the application title to the console
        static void PrintTitle()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n" + Resources.appTitle);
            Console.ResetColor();
        }

        // Prints the application usage to the console
        static bool PrintAppHelp()
        {
            PrintTitle();
            Console.WriteLine(string.Format(Resources.appHelp, Path.GetFileName(Program.configFile)));
            return true;
        }

        // Prints the configuration file syntax to the console
        static bool PrintConfigHelp()
        {
            PrintTitle();
            Console.WriteLine(string.Format(Resources.xmlHelp, Path.GetFileName(Program.configFile), Resources.xml));
            return true;
        }

        // Prints the CSS file syntax to the console
        static bool PrintStyleHelp()
        {
            PrintTitle();
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
            string file = Path.Combine(Program.appDirectory, "style.css");
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
