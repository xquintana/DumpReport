using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace DumpReport
{
    /// <summary>
    /// Holds the information about the exception found in the dump, provided that
    /// there is one and it could be retrieved.
    /// </summary>
    public class ExceptionInfo
    {
        public string module;
        public UInt64 address;
        public string code;
        public string description;
        public string frame;
    }

    /// <summary>
    /// Reads the debugger's output log and gets it parsed by the Parser objects.
    /// Finally, it sends the extracted information to the Report object.
    /// </summary>
    class LogManager
    {
        // Sections in the debugger's output file
        public const string SECTION_MARK     = ">>> ";
        public const string TARGET_INFO      = ">>> TARGET INFO";
        public const string MANAGED_THREADS  = ">>> MANAGED THREADS";
        public const string MANAGED_STACKS   = ">>> MANAGED STACKS";
        public const string EXCEPTION_RECORD = ">>> EXCEPTION RECORD";
        public const string INSTRUCT_PTRS    = ">>> INSTRUCTION POINTERS";
        public const string THREAD_STACKS    = ">>> THREAD STACKS";
        public const string LOADED_MODULES   = ">>> LOADED MODULES";
        public const string END_OF_LOG       = ">>> END OF LOG";

        // Parser objects
        DumpInfoParser dumpInfoParser = new DumpInfoParser();
        TargetInfoParser targetInfoParser = new TargetInfoParser();
        ManagedStacksParser managedStacksParser = new ManagedStacksParser();
        ExcepRecParser excepRecParser = new ExcepRecParser();
        ThreadParser threadInfoParser = new ThreadParser();
        ManagedThreadsParser managedThreadsParser = new ManagedThreadsParser();
        ModuleParser moduleParser = new ModuleParser();
        InstPtrParser instPtrParser = new InstPtrParser();
        Dictionary<string, Parser> m_parsers = new Dictionary<string, Parser>(); // Relates a Parser object with its section in the debugger's log

        public List<string> comments = new List<string>(); // Set of messages to be noted in the report
        ExceptionInfo exceptionInfo = new ExceptionInfo();
        Report report = null; // Outputs extracted data to an HTML file
        Config config = null; // Stores the paramaters of the application

        public LogManager(Config config, Report report)
        {
            this.config = config;
            this.report = report;
        }

        // Maps the sections in the debugger's output file with the corresponding Parser object
        void MapParsers()
        {
            m_parsers.Add(String.Empty, dumpInfoParser);
            m_parsers.Add(TARGET_INFO, targetInfoParser);
            m_parsers.Add(MANAGED_THREADS, managedThreadsParser);
            m_parsers.Add(MANAGED_STACKS, managedStacksParser);
            m_parsers.Add(EXCEPTION_RECORD, excepRecParser);
            m_parsers.Add(INSTRUCT_PTRS, instPtrParser);
            m_parsers.Add(THREAD_STACKS, threadInfoParser);
            m_parsers.Add(LOADED_MODULES, moduleParser);
            m_parsers.Add(END_OF_LOG, null);
        }

        // Returns the Parser object associated to the current section in the log
        Parser CheckNewSection(string line)
        {
            foreach (string section in m_parsers.Keys)
            {
                if (section.Length == 0)
                    continue;
                if (line.Contains(section))
                    return m_parsers[section];
            }
            return null;
        }

        // Reads the debugger's output log file and distributes the lines of each log section
        // to the corresponding Parser object.
        public bool ReadLog()
        {
            try
            {
                MapParsers();
                Parser parser = dumpInfoParser;

                using (StreamReader file = new StreamReader(config.LogFile, Encoding.Unicode))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.Contains(SECTION_MARK))
                            parser = CheckNewSection(line);
                        else if (parser != null)
                            parser.AddLine(line);
                    }
                }
                if (config.LogClean)
                    File.Delete(config.LogFile);
            }
            catch (Exception ex)
            {
                Program.ShowError(ex.Message);
                return false;
            }
            return true;
        }

        // Once the log file has been read, parse each section.
        public bool ParseLog()
        {
            try
            {
                foreach (Parser parser in m_parsers.Values)
                    if (parser != null) parser.Parse();
                CombineParserInfo();
            }
            catch (Exception ex)
            {
                Program.ShowError(ex.Message);
                return false;
            }
            return true;
        }

        // Complements the information of some Parsers with data from other Parsers
        void CombineParserInfo()
        {
            // Add managed information to the thread list
            foreach (ManagedStackInfo stack in managedStacksParser.Stacks)
                threadInfoParser.AddManagedInfo(stack.thread_num, stack);
            // Add instruction pointers to the thread list
            threadInfoParser.SetInstructionPointers(instPtrParser.InstPtrs);
            // Warn if no  PDB symbols were loaded from the expected folder
            if (!CheckPDBs())
                comments.Add("No PDBs loaded from " + config.PdbFolder);
        }

        // Process a specific log file that only contains an exception record
        public void ParseExceptionRecord(string file)
        {
            excepRecParser.RemoveLines();
            if (!File.Exists(file))
                return;
            using (StreamReader stream = new StreamReader(file, Encoding.Unicode))
            {
                string line;
                while ((line = stream.ReadLine()) != null)
                    excepRecParser.AddLine(line);
            }
            excepRecParser.Parse();
        }

        // Check if  PDB files have been loaded from the expected location
        bool CheckPDBs()
        {
            foreach (ModuleInfo module in moduleParser.Modules)
                if (module.pdbPath.ToUpper().Contains(config.PdbFolder.ToUpper()))
                    return true;
            return false;
        }

        // Writes all information to the report
        public void WriteReport()
        {
            Program.WriteConsole("Creating report...");
            WriteHeader();
            WriteExceptionInfo();
            WriteAllThreads();
            report.WriteJavascript(threadInfoParser.GetNumThreads());
        }

        // Writes the top part of the report
        void WriteHeader()
        {
            report.WriteDumpInfo(dumpInfoParser);
            report.WriteTargetInfo(targetInfoParser);
            report.WriteModuleInfo(moduleParser.Modules);
            report.WriteComments(comments);
        }

        // Tries to find an exception in the dump file and writes the info to the report.
        void WriteExceptionInfo()
        {
            int faultThreadNum = -1; // Index of the thread containing the exception

            if (excepRecParser.ContainsExceptionRecord())
            {
                excepRecParser.GetExceptionInfo(exceptionInfo);
                // Find the faulting thread by searching the exception address in the stacks or instruction pointers
                faultThreadNum = threadInfoParser.GetThreadByRetAddr(exceptionInfo.address);
                if (faultThreadNum < 0)
                    faultThreadNum = instPtrParser.GetThread(exceptionInfo.address);
            }

            if (faultThreadNum < 0) // Look in the managed threads
                faultThreadNum = managedThreadsParser.GetFaultingThread();

            if (faultThreadNum < 0) // Look for keywords in the stack traces
                faultThreadNum = threadInfoParser.GuessFaultingThread();

            if (exceptionInfo.address > 0 || faultThreadNum >= 0) // Exception information found
            {
                report.WriteSectionTitle("Exception Information");
                if (exceptionInfo.address > 0)
                    report.WriteExceptionInfo(exceptionInfo);
                if (faultThreadNum >= 0)
                    report.WriteThreadInfo(threadInfoParser.GetThread(faultThreadNum), true);
            }
            else
                report.Write("No exception found.");
        }

        // Write the information of all threads found in the dump file
        void WriteAllThreads()
        {
            report.WriteSectionTitle(string.Format("All Threads ({0})", threadInfoParser.GetNumThreads()));
            report.WriteAllThreadsMenu();
            foreach (ThreadInfo threadInfo in threadInfoParser.Threads)
                report.WriteThreadInfo(threadInfo);
        }

        // The following funtions just call the analogous function in the corresponding Parser object
        public bool ContainsExceptionRecord() { return excepRecParser.ContainsExceptionRecord(); }
        public bool GetUnhandledExceptionFilterInfo(ref int threadNum, out string arg1) { return threadInfoParser.GetUnhandledExceptionFilterInfo(ref threadNum, out arg1); }
        public bool GetKiUserExceptionDispatchInfo(out string childSP) { return threadInfoParser.GetKiUserExceptionDispatchInfo(out childSP); }
        public bool GetRtlDispatchExceptionInfo(out string arg3) { return threadInfoParser.GetRtlDispatchExceptionInfo(out arg3); }
    }
}
