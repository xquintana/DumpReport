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
        public ExceptionInfo() { threadNum = -1; }
        public bool Found()
        {
            return ((description != null && description.Length > 0) || (module != null && module.Length > 0) ||
                    (frame != null && frame.Length > 0) || address > 0 || threadNum >= 0);
        }

        public string description;
        public string module;
        public string frame;
        public UInt64 address;
        public int threadNum;
    }

    /// <summary>
    /// Reads the debugger's output log and gets it parsed by the Parser objects.
    /// Finally, it sends the extracted information to the Report object.
    /// </summary>
    class LogManager
    {
        // Sections in the debugger's output file
        public const string SECTION_MARK = ">>> ";
        public const string TARGET_INFO = ">>> TARGET INFO";
        public const string MANAGED_THREADS = ">>> MANAGED THREADS";
        public const string MANAGED_STACKS = ">>> MANAGED STACKS";
        public const string EXCEPTION_INFO = ">>> EXCEPTION INFO";
        public const string HEAP = ">>> HEAP";
        public const string INSTRUCT_PTRS = ">>> INSTRUCTION POINTERS";
        public const string THREAD_STACKS = ">>> THREAD STACKS";
        public const string LOADED_MODULES = ">>> LOADED MODULES";
        public const string END_OF_LOG = ">>> END OF LOG";

        // Parser objects
        DumpInfoParser dumpInfoParser = new DumpInfoParser();
        TargetInfoParser targetInfoParser = new TargetInfoParser();
        ManagedThreadsParser managedThreadsParser = new ManagedThreadsParser();
        ManagedStacksParser managedStacksParser = new ManagedStacksParser();
        ExcepInfoParser excepInfoParser = new ExcepInfoParser();
        HeapParser heapParser = new HeapParser();
        InstPtrParser instPtrParser = new InstPtrParser();
        ThreadParser threadParser = new ThreadParser();
        ModuleParser moduleParser = new ModuleParser();
        Dictionary<string, Parser> m_parsers = new Dictionary<string, Parser>(); // Relates a Parser object with its section mark in the debugger's log

        public List<string> notes = new List<string>(); // Set of messages to be noted in the report
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
            m_parsers.Add(EXCEPTION_INFO, excepInfoParser);
            m_parsers.Add(HEAP, heapParser);
            m_parsers.Add(INSTRUCT_PTRS, instPtrParser);
            m_parsers.Add(THREAD_STACKS, threadParser);
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
        public void ReadLog()
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
                throw new Exception(String.Format("{0} {1}", "Cannot read log file:", ex.Message));
            }
        }

        // Once the log file has been read, parse each section.
        public void ParseLog()
        {
            try
            {
                foreach (Parser parser in m_parsers.Values)
                    if (parser != null) parser.Parse();
                CheckParserInfo();
            }
            catch (Exception ex)
            {
                throw new Exception(String.Format("{0} {1}", "Cannot parse log file:", ex.Message));
            }
            if (config.LogClean)
                File.Delete(config.LogFile);
        }

        // Checks the extracted info and notifies possible anomalies
        void CheckParserInfo()
        {
            // Check if some PDB files have been loaded from the expected location
            bool PdbLoadedFromPath = false;
            foreach (ModuleInfo module in moduleParser.Modules)
                if (module.pdbPath.ToUpper().Contains(config.PdbFolder.ToUpper()))
                    PdbLoadedFromPath = true;
            if (!PdbLoadedFromPath)
                notes.Add("No PDBs loaded from " + config.PdbFolder);
        }

        // Complements the information of some Parsers with data from other Parsers
        public void CombineParserInfo()
        {
            // Assign the call stack of the exception thread, if present.
            // In some cases, the exception thread as it appears in section 'THREAD STACKS' may not provide a meaningful 
            // call stack (e.g, may show 'NtGetContextThread' if the dump was created by calling 'MiniDumpWriteDump')
            ThreadInfo exceptionThread = excepInfoParser.GetExceptionThread();
            if (exceptionThread != null && exceptionThread.threadNum < threadParser.Threads.Count)
                threadParser.Threads[exceptionThread.threadNum].stack = exceptionThread.stack;

            // Add managed frames to the main thread list
            foreach (ManagedStackInfo stack in managedStacksParser.Stacks)
                threadParser.AddManagedInfo(stack.threadNum, stack);

            // Add instruction pointers to the thread list
            threadParser.SetInstructionPointers(instPtrParser.InstPtrs);
        }

        // Returns true if the dump contains an exception and extracts as much information of the exception as possible.
        public bool GetExceptionInfo()
        {
            if (heapParser.ErrorDetected == true)
            {
                heapParser.GetExceptionInfo(exceptionInfo, threadParser);
                return true;
            }

            excepInfoParser.GetExceptionInfo(exceptionInfo, threadParser);

            // If the exception is managed, update the current exception info
            int managedFaultThreadNum = managedThreadsParser.GetFaultThreadNum();
            if (managedFaultThreadNum >= 0 && (excepInfoParser.IsClrException() || exceptionInfo.description == null))
            {
                exceptionInfo.threadNum = managedFaultThreadNum;
                exceptionInfo.description = managedThreadsParser.GetThread(managedFaultThreadNum).exceptionDescription;
            }

            if (exceptionInfo.threadNum < 0)
            {
                // We don't have the exception details but we can at least try to identify the faulting thread
                if (exceptionInfo.threadNum < 0) // Look for keywords in the call stacks
                    exceptionInfo.threadNum = threadParser.GuessFaultingThread();
            }
            return (exceptionInfo.address > 0 || exceptionInfo.threadNum >= 0);
        }

        // Returns true if the exeption info found is not valid or incomplete
        public bool NeedMoreExceptionInfo()
        {
            return heapParser.ErrorDetected == false && exceptionInfo.address == 0;
        }

        // Process a specific log file that only contains an exception record
        public void ParseExceptionRecord(string file)
        {
            excepInfoParser.RemoveLines();
            if (!File.Exists(file))
                return;
            using (StreamReader stream = new StreamReader(file, Encoding.Unicode))
            {
                string line;
                while ((line = stream.ReadLine()) != null)
                    excepInfoParser.AddLine(line);
            }
            excepInfoParser.Parse();
        }

        // Writes all information to the report
        public void WriteReport()
        {
            Program.WriteConsole("Creating report...", true);
            WriteHeader();
            WriteExceptionInfo();
            WriteAllThreads();
            report.WriteJavascript(threadParser.GetNumThreads());
            Program.WriteConsole("\rReport created in " + config.ReportFile);
        }

        // Writes the top part of the report
        void WriteHeader()
        {
            report.WriteDumpInfo(dumpInfoParser);
            report.WriteTargetInfo(targetInfoParser);
            report.WriteModuleInfo(moduleParser.Modules);
            report.WriteNotes(notes);
        }

        // Tries to find an exception in the dump file and writes the info to the report.
        void WriteExceptionInfo()
        {
            if (exceptionInfo.Found() == true)
            {
                report.WriteSectionTitle("Exception Information");
                report.WriteExceptionInfo(exceptionInfo);
                if (exceptionInfo.threadNum >= 0)
                    report.WriteFaultingThreadInfo(threadParser.GetThread(exceptionInfo.threadNum));
            }
            else
                report.Write("No exception found.");
        }

        // Write the information of all threads found in the dump file
        void WriteAllThreads()
        {
            report.WriteSectionTitle(string.Format("All threads ({0}) grouped by call stack", threadParser.GetNumThreads()));
            report.WriteAllThreadsMenu();
            Dictionary<string, List<ThreadInfo>> threadGroups = threadParser.GroupThreadsByCallStack();
            foreach (List<ThreadInfo> threads in threadGroups.Values)
                report.WriteThreadInfo(threads);
        }

        // Returns a script that hopefully will retrieve the exception record
        public string GetExceptionRecordScript()
        {
            FrameInfo frameInfo;
            ThreadInfo threadInfo;

            if (Program.is32bitDump)
            {
                // Find the exception record in the block of memory pointed by the first param of 'UnhandledExceptionFilter'
                threadParser.GetFrameByKeyword("UnhandledExceptionFilter", out frameInfo, out threadInfo);
                if (frameInfo != null && Utils.NotZeroAddress(frameInfo.argsToChild1))
                {
                    System.Diagnostics.Trace.WriteLine("GetExceptionRecordScript-> " + Path.GetFileName(config.DumpFile) + " using UnhandledExceptionFilter (x86)");
                    return Resources.dbgUnhandledExceptionFilter32.Replace("[FIRST_PARAM]", frameInfo.argsToChild1);
                }
                // Find the exception record in the address pointed by the third param of 'RtlDispatchException'
                threadParser.GetFrameByKeyword("RtlDispatchException", out frameInfo, out threadInfo);
                if (frameInfo != null && Utils.NotZeroAddress(frameInfo.argsToChild3))
                {
                    System.Diagnostics.Trace.WriteLine("GetExceptionRecordScript-> " + Path.GetFileName(config.DumpFile) + " using RtlDispatchException (x86)");
                    return Resources.dbgRtlDispatchException.Replace("[THIRD_PARAM]", frameInfo.argsToChild3);
                }
            }
            else
            {
                // Find the exception record in the call stack of the 'KiUserExceptionDispatch' frame
                threadParser.GetFrameByKeyword("KiUserExceptionDispatch", out frameInfo, out threadInfo);
                if (frameInfo != null && Utils.NotZeroAddress(frameInfo.childSP))
                {
                    System.Diagnostics.Trace.WriteLine("GetExceptionRecordScript-> " + Path.GetFileName(config.DumpFile) + " using KiUserExceptionDispatch");
                    return Resources.dbgKiUserExceptionDispatch.Replace("[CHILD_SP]", frameInfo.childSP);
                }
                // Find the exception record in the block of memory pointed by the fourth param of 'WerpReportFault'
                threadParser.GetFrameByKeyword("WerpReportFault", out frameInfo, out threadInfo);
                if (frameInfo != null && Utils.NotZeroAddress(frameInfo.argsToChild4))
                {
                    System.Diagnostics.Trace.WriteLine("GetExceptionRecordScript-> " + Path.GetFileName(config.DumpFile) + " using WerpReportFault");
                    return Resources.dbgWerpReportFault64.Replace("[FOURTH_PARAM]", frameInfo.argsToChild4);
                }
                // Find the exception record in the address pointed by the third param of 'RtlDispatchException'
                threadParser.GetFrameByKeyword("RtlDispatchException", out frameInfo, out threadInfo);
                if (frameInfo != null && Utils.NotZeroAddress(frameInfo.argsToChild3))
                {
                    System.Diagnostics.Trace.WriteLine("GetExceptionRecordScript-> " + Path.GetFileName(config.DumpFile) + " using RtlDispatchException");
                    return Resources.dbgRtlDispatchException.Replace("[THIRD_PARAM]", frameInfo.argsToChild3);
                }
            }
            System.Diagnostics.Trace.WriteLine("GetExceptionRecordScript-> " + Path.GetFileName(config.DumpFile) + " No suitable method found");
            return null;
        }
    }
}
