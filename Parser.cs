using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;


namespace DumpReport
{
    /**
     * ****** INFO Classes ******
     *
     * The 'Info' classes represent units of information present in the dump log.
     * For example, they store data about a single thread, a loaded module,..etc
     * This information is extracted by means of classes of type 'Parser', declared later on.
     * */

    /// <summary>
    /// Stores information about a managed call stack frame.
    /// </summary>
    class ManagedStackFrameInfo
    {
        public string childSP;
        public string instructPtr;
        public string callSite;
    }
    /// <summary>
    /// Stores information about a managed call stack.
    /// </summary>
    class ManagedStackInfo
    {
        public string threadId;
        public int threadNum;
        public List<ManagedStackFrameInfo> frames;
    }
    /// <summary>
    /// Stores information about a managed thread.
    /// </summary>
    class ManagedThreadInfo
    {
        public int threadNum; // Thread number in total threads
        public int threadNumManaged; // Thread number in managed threads
        public string threadId;
        public string threadObj;
        public string state;
        public string gcMode;
        public string gcAllocCtx;
        public string domain;
        public string lockCount;
        public string apt;
        public string exceptionDescription;
    }
    /// <summary>
    /// Stores information about a call stack frame.
    /// </summary>
    class FrameInfo
    {
        //public int threadNum;
        public int numFrame;
        public bool inline;
        public string childSP;
        public string returnAddress;
        public string argsToChild1;
        public string argsToChild2;
        public string argsToChild3;
        public string argsToChild4;
        public string callSite;
        // the following members are extracted from 'callSite'
        public string module;
        public string function;
        public string file;
        public string line;
    }
    /// <summary>
    /// Stores information about a thread.
    /// </summary>
    class ThreadInfo
    {
        public int threadNum; // index of the thread in the thread list
        public string threadId;
        public string instructPtr;
        public List<FrameInfo> stack;
    }
    /// <summary>
    /// Stores information about a loaded module.
    /// </summary>
    class ModuleInfo
    {
        public string startAddr;
        public string endAddr;
        public string moduleName;
        public string pdbStatus;
        public string pdbPath;
        public string timestamp;
        public string imagePath;
        public string imageName;
        public string fileVersion;
        public string productVersion;
        public string fileDescription;
    }

    /**
     * ****** PARSER Classes ******
     *
     * The 'Parser' classes extract information from a specific section in the debuggers's output file.
     * */

    /// <summary>
    /// The base class of all Parser objects.
    /// Provides a method AddLine() that collects the lines to be parsed and defines an
    /// abstract method Parse() that all inherited classes must implement.
    /// </summary>
    abstract class Parser
    {
        protected List<string> lines = new List<string>();
        protected MatchCollection matches = null;
        protected string pattern;

        public void AddLine(string line) { lines.Add(line); }
        public void RemoveLines() { lines.Clear(); }
        public abstract void Parse();
    }

    /// <summary>
    /// Extracts information about the dump and its framework.
    /// </summary>
    class DumpInfoParser : Parser
    {
        public string DumpBitness { get; set; }
        public bool Wow64Found { get; set; }
        public bool SosLoaded { get; set; }
        public string ClrVersion { get; set; }
        public string CreationTime { get; set; }

        public override void Parse()
        {
            SosLoaded = false;
            Wow64Found = false;

            for (int idx = 0; idx < lines.Count; idx++)
            {
                if (lines[idx].Contains("Effective machine"))
                {
                    string pattern = @"Effective machine:.+\((?<eff_mach>.+)\)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        DumpBitness = matches[0].Groups["eff_mach"].Value;
                }
                else if (lines[idx].Contains("WOW64 found"))
                    Wow64Found = true;
                else if (lines[idx].Contains("Automatically loaded SOS Extension"))
                    SosLoaded = true;
                else if (lines[idx].Contains("Debug session time:"))
                {
                    pattern = @"Debug session time:\s(?<creation_time>.+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        CreationTime = Utils.GetNormalizedDumpTime(matches[0].Groups["creation_time"].Value);
                }
                else if (lines[idx].Contains("eeversion"))
                {
                    while (++idx < lines.Count)
                    {
                        pattern = @"(?<clr_ver>[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)";
                        matches = Regex.Matches(lines[idx], pattern);
                        if (matches.Count == 1)
                            ClrVersion = matches[0].Groups["clr_ver"].Value;
                    }
                }
            }
            // If this value has not been found, the debugger's log was not generated properly. 
            if (DumpBitness == null || DumpBitness.Length == 0)
                throw new Exception("The debugger's log has an invalid format.");
        }
    }

    /// <summary>
    /// Extracts information about the computer where the dump was generated from.
    /// </summary>
    class TargetInfoParser : Parser
    {
        public string ComputerName { get; set; }
        public string UserName { get; set; }
        public string ProcessId { get; set; }
        public string CommandLine { get; set; }
        public string OsInfo { get; set; }
        public Dictionary<string, string> Environment { get; set; }

        public override void Parse()
        {
            Environment = new Dictionary<string, string>();

            for (int idx = 0; idx < lines.Count; idx++)
            {
                if (lines[idx].Contains("TARGET:"))
                {
                    OsInfo = lines[++idx].Split(new string[] { "Free x" }, StringSplitOptions.None)[0];
                    while (++idx < lines.Count && !lines[idx].Contains("Debug session time")) ;
                }
                else if (lines[idx].Contains("COMPUTERNAME = "))
                {
                    pattern = @"COMPUTERNAME\s=\s(?<computer_name>.+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        ComputerName = matches[0].Groups["computer_name"].Value;
                }
                else if (lines[idx].Contains("USERNAME = "))
                {
                    pattern = @"USERNAME\s=\s(?<user_name>.+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        UserName = matches[0].Groups["user_name"].Value;
                }
                else if (lines[idx].Contains("PROCESS_ID:"))
                {
                    idx++;
                    pattern = @"id:\s(?<proc_id>\w+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        ProcessId = "0x" + matches[0].Groups["proc_id"].Value;
                }
                else if (lines[idx].Contains("PEB at")) // Parse PEB
                {
                    while (++idx < lines.Count)
                    {
                        if (lines[idx].Contains("CommandLine:"))
                        {
                            pattern = @"CommandLine:\s+'(?<command_line>.+)'";
                            matches = Regex.Matches(lines[idx], pattern);
                            if (matches.Count == 1)
                                CommandLine = matches[0].Groups["command_line"].Value;
                        }
                        else if (lines[idx].Contains("Environment:"))
                        {
                            while (++idx < lines.Count) // This subsection is the last one
                            {
                                if (lines[idx].Contains("=C:=C:") || lines[idx].Contains("=::"))
                                    continue;
                                pattern = @"(?<envvar_name>.+)=(?<envvar_value>.+)";
                                matches = Regex.Matches(lines[idx], pattern);
                                if (matches.Count == 1)
                                    Environment.Add(matches[0].Groups["envvar_name"].Value.Trim(' '), matches[0].Groups["envvar_value"].Value);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extracts information about the managed threads.
    /// </summary>
    class ManagedThreadsParser : Parser
    {
        public List<ManagedThreadInfo> Threads { get; set; }

        public override void Parse()
        {
            Debug.Assert(lines.Count > 0);
            ManagedThreadInfo managedThread = null;
            Threads = new List<ManagedThreadInfo>();

            foreach (string line in lines)
            {
                pattern = @"(?<thread_num>[0-9]+)\s+(?<thread_num_man>[0-9]+)\s+(?<osid>\w+)\s+(?<thread_obj>\w+)\s+(?<state>\w+)\s+(?<gc_mode>\w+)\s+(?<gc_alloc_ctx>[\w,:]+)\s+(?<domain>\w+)\s+(?<lock_count>\w+)\s+(?<apt>\w+)\s+(?<exception>.*)";
                matches = Regex.Matches(line, pattern);

                if (matches.Count == 1)
                {
                    managedThread = new ManagedThreadInfo
                    {
                        threadNum = Convert.ToInt32(matches[0].Groups["thread_num"].Value),
                        threadNumManaged = Convert.ToInt32(matches[0].Groups["thread_num_man"].Value),
                        threadId = matches[0].Groups["osid"].Value,
                        threadObj = matches[0].Groups["thread_obj"].Value,
                        state = matches[0].Groups["state"].Value,
                        gcMode = matches[0].Groups["gc_mode"].Value,
                        gcAllocCtx = matches[0].Groups["gc_alloc_ctx"].Value,
                        domain = matches[0].Groups["domain"].Value,
                        lockCount = matches[0].Groups["lock_count"].Value,
                        apt = matches[0].Groups["apt"].Value,
                        exceptionDescription = CleanExceptionDescription(matches[0].Groups["exception"].Value)
                    };
                    Threads.Add(managedThread);
                }
            }
        }

        // Managed exceptions can be reported with an address at the end of the description.
        // This methods removes this address if it exists (e.g, "System.ArgumentException 000001ea85257290").
        string CleanExceptionDescription(string description)
        {
            string[] parts = description.Split(' ');
            if (parts.Length > 1)
            {
                // check whether it's a valid hexadecimal value.
                try
                {
                    Utils.StrHexToUInt64(parts[parts.Length - 1]);
                    return String.Join("", parts, 0, parts.Length - 1);
                }
                catch (Exception) { }
            }
            return description;
        }

        public int GetFaultThreadNum()
        {
            foreach (ManagedThreadInfo thread in Threads)
            {
                matches = Regex.Matches(thread.exceptionDescription, "exception", RegexOptions.IgnoreCase);
                if (matches.Count == 1)
                    return Convert.ToInt32(thread.threadNum);
            }
            return -1;
        }

        // The thread number relates to the total amount of threads (native and managed)
        public ManagedThreadInfo GetThread(int threadNum)
        {
            foreach (ManagedThreadInfo thread in Threads)
            {
                if (thread.threadNum == threadNum)
                    return thread;
            }
            return null;
        }
    }

    /// <summary>
    /// Extracts information about the managed call stacks.
    /// </summary>
    class ManagedStacksParser : Parser
    {
        public List<ManagedStackInfo> Stacks { get; set; }

        public override void Parse()
        {
            int idx = 0;
            string line;
            string thread_id;
            int thread_num;
            ManagedStackInfo stack = null;
            Stacks = new List<ManagedStackInfo>();
            Debug.Assert(lines.Count > 0);

            while (idx < lines.Count)
            {
                line = lines[idx++];
                pattern = @"OS Thread Id: (?<thread_id>\w+)\s\((?<thread_num>\w+)\)";
                matches = Regex.Matches(line, pattern);
                if (matches.Count == 1)
                {
                    thread_id = matches[0].Groups["thread_id"].Value;
                    thread_num = Convert.ToInt32(matches[0].Groups["thread_num"].Value);
                    stack = null;

                    while (idx < lines.Count)
                    {
                        line = lines[idx++];
                        if (line.Contains("Child SP"))
                            continue;
                        if (line.Contains("GetFrameContext failed") || line.Contains("Unable to walk the managed stack"))
                            break;
                        if (line.Contains("OS Thread Id"))
                        {
                            idx--;
                            break;
                        }
                        if (line.Contains("InlinedCallFrame:"))
                            continue;

                        pattern = @"(?<child_SP>[\w]{8,16})\s(?<ip>[\w]{8,16})\s(?<call_site>.+)";
                        matches = Regex.Matches(line, pattern);

                        if (matches.Count == 1)
                        {
                            if (stack == null)
                            {
                                stack = new ManagedStackInfo();
                                stack.threadId = thread_id;
                                stack.threadNum = thread_num;
                                stack.frames = new List<ManagedStackFrameInfo>();
                                Stacks.Add(stack);
                            }

                            ManagedStackFrameInfo manStackFrame = new ManagedStackFrameInfo();
                            manStackFrame.childSP = matches[0].Groups["child_SP"].Value;
                            manStackFrame.instructPtr = matches[0].Groups["ip"].Value;
                            manStackFrame.callSite = matches[0].Groups["call_site"].Value;

                            while (manStackFrame.callSite.Contains("*** ") && idx < lines.Count)
                                manStackFrame.callSite = lines[idx++];

                            stack.frames.Add(manStackFrame);
                        }
                        else
                            break;
                    }
                }
            }
        }

        public ManagedStackInfo GetStack(int threadNum)
        {
            foreach (ManagedStackInfo stack in Stacks)
            {
                if (stack.threadNum == threadNum)
                    return stack;
            }
            return null;
        }
    }

    /// <summary>
    /// Extracts information about the exception, if present.
    /// </summary>
    class ExcepInfoParser : Parser
    {
        UInt64 address;    // Exception address as number
        string addressHex; // Exception address as hexadecimal string
        string frame;
        string description;
        string module;
        int threadNum;
        bool isClrException;
        ThreadInfo exceptionThread; // The thread that generated the exception       

        public override void Parse()
        {
            addressHex = null;
            frame = null;
            description = null;
            module = null;
            threadNum = -1;

            Debug.Assert(lines.Count > 0);
            for (int idx = 0; idx < lines.Count; idx++)
            {
                if (lines[idx].Contains("ExceptionAddress:"))
                {
                    pattern = @"ExceptionAddress:\s(?<excep_addr>[\w]+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                    {
                        addressHex = matches[0].Groups["excep_addr"].Value;
                        address = Utils.StrHexToUInt64(addressHex);
                        if (address == 0) // This exception record is not valid
                            return;
                        // Find exception frame
                        if (lines[idx].Contains("("))
                        {
                            pattern = @"\s\((?<excep_frame>.+)\)";
                            matches = Regex.Matches(lines[idx], pattern);
                            if (matches.Count == 1)
                            {
                                frame = matches[0].Groups["excep_frame"].Value;
                                if (frame.Contains("!"))
                                {
                                    string[] parts = frame.Split('!');
                                    if (parts.Length >= 2) module = parts[0];
                                }
                            }
                        }
                    }
                }
                else if (lines[idx].Contains("ExceptionCode:"))
                {
                    pattern = @"ExceptionCode: (?<exception>.+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                    {
                        description = matches[0].Groups["exception"].Value;
                        if (description.Contains("CLR exception"))
                            isClrException = true;
                    }
                }
                else if (lines[idx].Contains("EXCEPTION CALL STACK:"))
                {
                    ThreadParser threadParser = new ThreadParser(); // Used to extract the exception thread's call stack
                    while (++idx < lines.Count)
                        threadParser.AddLine(lines[idx]);

                    threadParser.Parse();
                    if (threadParser.Threads.Count > 0)
                    {
                        exceptionThread = threadParser.Threads[0];
                        threadNum = exceptionThread.threadNum;
                    }
                }
                else if (lines[idx].Contains("Unable to get exception context"))
                    return; // The exception info is not useful.
            }
        }

        public ThreadInfo GetExceptionThread() { return exceptionThread; }

        public bool IsClrException() { return isClrException; }

        public void GetExceptionInfo(ExceptionInfo exceptionInfo, ThreadParser threadParser)
        {
            if (threadNum < 0)
                threadNum = GetFaultingThreadNum(threadParser);

            if (threadNum >= 0 && address > 0 && (frame == null || module == null))
            {
                // Get the frame in the faulting thread with that address
                FrameInfo frameInfo = threadParser.GetFrameInfoByAddress(threadNum, address);
                if (frameInfo != null)
                {
                    if (frame == null) frame = frameInfo.function;
                    if (module == null) module = frameInfo.module;
                }
            }
            // Copy the internal info
            exceptionInfo.description = description;
            exceptionInfo.address = address;
            exceptionInfo.module = module;
            exceptionInfo.frame = frame;
            exceptionInfo.threadNum = threadNum;
        }

        // Returns the index of the faulting thread
        int GetFaultingThreadNum(ThreadParser threadParser)
        {
            // Get all threads containing the exception address
            int threadNum = -1;
            List<ThreadInfo> threads = threadParser.GetThreadsByAddress(address);
            if (threads.Count == 1)
                threadNum = threads[0].threadNum;
            else if (threads.Count > 1)
                threadNum = threadParser.GuessFaultingThread(threads);
            return threadNum;
        }
    }

    /// <summary>
    /// Stores information about heap errors.
    /// </summary>
    class HeapParser : Parser
    {
        public bool ErrorDetected { get; set; } // True if a heap error has been detected

        string errorType; // The error type as reported by the debugger
        string errorDetails; // The report also adds a description to the error type
        List<UInt64> stack = new List<UInt64>();  // The stack reported by the !heap command

        public override void Parse()
        {
            ErrorDetected = false;
            Debug.Assert(lines.Count > 0);
            int idx = 0;
            while (++idx < lines.Count)
            {
                if (lines[idx].Contains("Error type:"))
                {
                    ErrorDetected = true;
                    errorDetails = String.Empty;
                    pattern = @"Error type:\s+(?<error_type>.+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        errorType = matches[0].Groups["error_type"].Value;

                    // Parse the error description and the call stack
                    while (++idx < lines.Count)
                    {
                        if (lines[idx].Contains("Stack trace:"))
                        {
                            pattern = @"\s+(?<frame_addr>\w+):\s";
                            while (++idx < lines.Count)
                            {                                
                                if (lines[idx].Contains("*** WARNING") || 
                                    lines[idx].Contains("Stack trace"))  // Version 10.0.22000.194 adds a second line containing "stack trace"
                                    continue;
                                matches = Regex.Matches(lines[idx], pattern);
                                if (matches.Count == 1)
                                    stack.Add(Utils.StrHexToUInt64(matches[0].Groups["frame_addr"].Value));
                                else return;
                            }
                        }
                        else errorDetails += lines[idx];
                    }
                }
            }
        }

        public void GetExceptionInfo(ExceptionInfo exceptionInfo, ThreadParser threadParser)
        {
            exceptionInfo.description = "Heap corruption (" + errorType + "). " + errorDetails.Replace(".", ". ");

            // Finds out the thread number that corresponds to the extracted call stack
            List<ThreadInfo> threads = threadParser.GetThreadsByStack(stack);
            if (threads.Count == 1) // Only one thread is expected
                exceptionInfo.threadNum = threads[0].threadNum;
        }
    }

    /// <summary>
    /// Stores the values of the 'Instruction Pointer' register (EIP/RIP) of all threads.
    /// </summary>
    class InstPtrParser : Parser
    {
        public List<string> InstPtrs { get; set; }

        public override void Parse()
        {
            Debug.Assert(lines.Count > 0);
            InstPtrs = new List<string>();
            pattern = @"=\s(?<inst_ptr>[\w,`]+)";

            foreach (string line in lines)
            {
                matches = Regex.Matches(line, pattern);
                if (matches.Count == 1)
                    InstPtrs.Add(matches[0].Groups["inst_ptr"].Value.Replace("`", String.Empty));
            }
        }

        public int GetThread(UInt64 faultAddress)
        {
            int threadCount = 0;
            foreach (string hexString in InstPtrs)
            {
                if (Utils.StrHexToUInt64(hexString) == faultAddress)
                    return threadCount;
                threadCount++;
            }
            return -1;
        }
    }

    /// <summary>
    /// Extracts information about the threads found in the dump.
    /// </summary>
    class ThreadParser : Parser
    {
        public List<ThreadInfo> Threads { get; set; }
        List<string> ExceptionKeywords = new List<string>();
        int numFrames = 0;

        // Creates a list of keywords likely to be present in the call stack of a faulting thread
        void InitExceptionKeywords()
        {
            ExceptionKeywords = new List<string>
            {
                "UnhandledExceptionFilter",
                "WerpReportFault",
                "KiUserExceptionDispatch",
                "RtlDispatchException",
                "CWinApp::ProcessWndProcException",
                "invoke_watson",
                "!abort+"
            };
        }

        public void ReplaceThreadStack(int threadNum, List<FrameInfo> stack)
        {
            foreach (ThreadInfo threadInfo in Threads)
            {
                if (threadInfo.threadNum == threadNum)
                {
                    threadInfo.stack.Clear();
                    threadInfo.stack = stack;
                    return;
                }
            }
        }

        public override void Parse()
        {
            Debug.Assert(lines.Count > 0);
            ThreadInfo thread = null;
            Threads = new List<ThreadInfo>();

            InitExceptionKeywords();

            foreach (string line in lines)
            {
                if (line.Contains("Id:"))
                {
                    pattern = @"(?<thread_num>[0-9]+)\s+Id:\s.*\.(?<thread_id>\w+).+\sTeb:\s(?<teb>[\w,`]+)";
                    matches = Regex.Matches(line, pattern);

                    if (matches.Count == 1)
                    {
                        thread = new ThreadInfo();
                        thread.stack = new List<FrameInfo>();
                        Threads.Add(thread);
                        thread.threadNum = Convert.ToInt32(matches[0].Groups["thread_num"].Value);
                        thread.threadId = matches[0].Groups["thread_id"].Value;
                        numFrames = 0;
                    }
                }
                else if (line.Contains("Inline Function"))
                {
                    string[] parts = line.Split(new string[] { "--------" }, StringSplitOptions.None);
                    FrameInfo frame = new FrameInfo()
                    {
                        numFrame = numFrames++,
                        callSite = parts[parts.Length - 1].Trim(' ', ':'),
                        inline = true
                    };
                    ParseCallSite(frame);
                    thread.stack.Add(frame);
                }
                else
                {
                    if (Program.is32bitDump) // 32-bits log
                        pattern = @"^\w+\s(?<child>\w+)\s(?<return_addr>\w+)\s+(?<args_to_child1>\w+)\s(?<args_to_child2>\w+)\s(?<args_to_child3>\w+)\s(?<call_site>.*)";
                    else // 64-bits log
                        pattern = @"(?<child>[\w,`]+)\s(?<return_addr>[\w,`]+)\s+:\s+(?<args_to_child1>[\w,`]+)\s+(?<args_to_child2>[\w,`]+)\s+(?<args_to_child3>[\w,`]+)\s+(?<args_to_child4>[\w,`]+)\s+:\s+(?<call_site>.*)";

                    matches = Regex.Matches(line, pattern);

                    if (matches.Count == 1)
                    {
                        FrameInfo frame = new FrameInfo
                        {
                            numFrame = numFrames++,
                            childSP = matches[0].Groups["child"].Value.Replace("`", string.Empty),
                            returnAddress = matches[0].Groups["return_addr"].Value.Replace("`", string.Empty),
                            argsToChild1 = matches[0].Groups["args_to_child1"].Value.Replace("`", string.Empty),
                            argsToChild2 = matches[0].Groups["args_to_child2"].Value.Replace("`", string.Empty),
                            argsToChild3 = matches[0].Groups["args_to_child3"].Value.Replace("`", string.Empty),
                            argsToChild4 = matches[0].Groups["args_to_child4"].Value.Replace("`", string.Empty),
                            callSite = matches[0].Groups["call_site"].Value
                        };
                        ParseCallSite(frame);
                        thread.stack.Add(frame);
                    }
                }
            }
        }

        public int GetNumThreads()
        {
            return Threads.Count;
        }

        void ParseCallSite(FrameInfo frame)
        {
            // CDB adds this keyword for some managed frames.
            // It is removed to make the call site look more similar to the one extracted with WinDBG.
            frame.callSite = frame.callSite.Replace("<Module>", "");

            // Expected frame format: module!function [file @ line]
            // module and/or file info may be missing.
            if (frame.callSite.Contains("!"))
            {
                string[] parts = frame.callSite.Split('!');
                if (parts.Length == 2)
                {
                    frame.module = parts[0];
                    if (parts[1].Contains("["))
                    {   // frame contains file name and line number
                        pattern = @"(?<function>.+)\[(?<file>.+)\s@\s(?<line>[0-9]+)\]";
                        matches = Regex.Matches(parts[1], pattern);
                        if (matches.Count == 1)
                        {
                            frame.function = matches[0].Groups["function"].Value;
                            frame.file = matches[0].Groups["file"].Value;
                            frame.line = matches[0].Groups["line"].Value;
                        }
                        else frame.function = parts[1];
                    }
                    else frame.function = parts[1];
                }
                else frame.function = frame.callSite;
            }
            else frame.function = frame.callSite;
        }

        public FrameInfo GetFrameInfoByAddress(int threadNum, UInt64 address)
        {
            if (threadNum > Threads.Count)
                return null;
            ThreadInfo thread = Threads[threadNum];
            if (thread.stack.Count == 0)
                return null;
            if (Utils.StrHexToUInt64(Threads[threadNum].instructPtr) == address)
                return thread.stack[0];
            for (int i = 0; i < Threads[threadNum].stack.Count - 1; i++)
            {
                if (thread.stack[i].inline == true)
                    continue;
                if (Utils.StrHexToUInt64(thread.stack[i].returnAddress) == address)
                    return thread.stack[i + 1];
            }
            return null;
        }

        // Returns a list of threads that contain a specific address
        public List<ThreadInfo> GetThreadsByAddress(UInt64 address)
        {
            List<UInt64> stack = new List<UInt64> { address };
            return GetThreadsByStack(stack);
        }

        // Returns a list of threads that contain the input call stack
        public List<ThreadInfo> GetThreadsByStack(List<UInt64> stack)
        {
            List<ThreadInfo> threads = new List<ThreadInfo>();

            // Convert the input stack into a string
            string inputStackStamp = String.Empty;
            foreach (UInt64 addr in stack)
                inputStackStamp += Utils.UInt64toStringHex(addr, false).ToUpper();

            foreach (ThreadInfo thread in Threads)
            {
                // Convert current stack into a string
                string currentStackStamp = String.Empty;
                foreach (FrameInfo frame in thread.stack)
                {
                    if (frame.returnAddress == null)
                        continue;
                    currentStackStamp += frame.returnAddress.ToUpper();
                }
                // Check if the current stack contains the input stack
                if (currentStackStamp.Contains(inputStackStamp))
                    threads.Add(thread);
            }
            return threads;
        }

        // Returns the index of the first thread that contains a keyword in the call stack
        public bool GetFrameByKeyword(string keyword, out FrameInfo frameInfo, out ThreadInfo threadInfo)
        {
            frameInfo = null;
            threadInfo = null;
            string pattern = keyword + @"[^\w]";

            foreach (ThreadInfo threadInfoAux in Threads)
                foreach (FrameInfo frameInfoAux in threadInfoAux.stack)
                    if (Regex.Match(frameInfoAux.function, pattern, RegexOptions.IgnoreCase).Success)
                    {
                        frameInfo = frameInfoAux;
                        threadInfo = threadInfoAux;
                        return true;
                    }
            return false;
        }

        // Returns the index of the first thread in a thread list containing an exception keyword.
        // If the thread list is null, it looks in all threads.
        public int GuessFaultingThread(List<ThreadInfo> threadList = null)
        {
            List<ThreadInfo> threads = (threadList != null) ? threadList : Threads;
            foreach (ThreadInfo thread in threads)
            {
                foreach (FrameInfo frame in thread.stack)
                {
                    foreach (string keyword in ExceptionKeywords)
                        if (frame.callSite.Contains(keyword))
                            return thread.threadNum;
                }
            }
            return -1;
        }

        public ThreadInfo GetThread(int threadNum)
        {
            return Threads[threadNum];
        }

        // Improves the function names of the managed frames.
        public void AddManagedInfo(int threadNum, ManagedStackInfo managedStack)
        {
            List<FrameInfo> stack = Threads[threadNum].stack;
            foreach (ManagedStackFrameInfo managedFrame in managedStack.frames)
            {
                foreach (FrameInfo frame in stack)
                {
                    if (frame.inline)
                        continue;
                    if ((Program.is32bitDump && Utils.SameStrAddress(managedFrame.instructPtr, frame.callSite)) ||
                        (!Program.is32bitDump && managedFrame.childSP == frame.childSP))
                    {
                        if (!managedFrame.callSite.Contains("!")) // If the managed frame does not contain a module name, add a default one
                            frame.callSite = "(managed)!" + managedFrame.callSite;
                        else
                            frame.callSite = managedFrame.callSite;
                        ParseCallSite(frame);
                    }
                }
            }
        }

        // Sets the instruction pointer for each thread
        public void SetInstructionPointers(List<string> instPtrs)
        {
            if (instPtrs.Count != Threads.Count)
                return;
            for (int i = 0; i < Threads.Count; i++)
                Threads[i].instructPtr = instPtrs[i];
        }

        // Returns a dictionary where each value is a list of threads with the same call stack
        public Dictionary<string, List<ThreadInfo>> GroupThreadsByCallStack()
        {
            Dictionary<string, List<ThreadInfo>> groups = new Dictionary<string, List<ThreadInfo>>();

            foreach (ThreadInfo thread in Threads)
            {
                string key = String.Empty;
                foreach (FrameInfo frame in thread.stack)
                    key += frame.returnAddress;
                if (groups.ContainsKey(key))
                    groups[key].Add(thread);
                else
                {
                    List<ThreadInfo> newGroup = new List<ThreadInfo>();
                    newGroup.Add(thread);
                    groups.Add(key, newGroup);
                }
            }
            return groups;
        }
    }

    /// <summary>
    /// Extracts information about the loaded modules.
    /// </summary>
    class ModuleParser : Parser
    {
        public List<ModuleInfo> Modules { get; set; }

        public override void Parse()
        {
            ModuleInfo module = null;
            Modules = new List<ModuleInfo>();
            Debug.Assert(lines.Count > 0);

            string patternModule = @"^(?<start_addr>[\w,`]+)\s(?<end_addr>[\w,`]+)\s+(?<module_name>[\w]{2,}).+?\((?<pdb_status>[\w,\s]+)?\)(?<path>.*)";

            foreach (string line in lines)
            {
                matches = Regex.Matches(line, patternModule);

                if (matches.Count == 1)
                {
                    if (module != null)
                        Modules.Add(module);
                    if (line.Contains(" System_") || line.Contains(" api_ms_win"))
                        module = null; // irrelevant for debugging
                    else
                        module = new ModuleInfo()
                        {
                            startAddr = matches[0].Groups["start_addr"].Value.Replace("`", String.Empty),
                            endAddr = matches[0].Groups["end_addr"].Value.Replace("`", String.Empty),
                            moduleName = matches[0].Groups["module_name"].Value,
                            pdbStatus = matches[0].Groups["pdb_status"].Value,
                            pdbPath = matches[0].Groups["path"].Value.Trim(' ')
                        };
                }
                else if (module != null)
                {
                    if (line.Contains("Image path:"))
                    {
                        pattern = @"Image path:\s(?<image_path>.+)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            module.imagePath = matches[0].Groups["image_path"].Value;
                    }
                    else if (line.Contains("Image name:"))
                    {
                        pattern = @"Image name:\s(?<image_name>.+)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            module.imageName = matches[0].Groups["image_name"].Value;
                    }
                    else if (line.Contains("Timestamp:") && !line.Contains("This is a reproducible build file hash, not a timestamp"))
                    {
                        pattern = @"Timestamp:.+\((?<timestamp>.+)\)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            module.timestamp = matches[0].Groups["timestamp"].Value;
                    }
                    else if (line.Contains("File version:"))
                    {
                        pattern = @"File version:\s(?<file_version>.+)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            module.fileVersion = matches[0].Groups["file_version"].Value.Trim();
                    }
                    else if (line.Contains("ProductVersion:"))
                    {
                        pattern = @"ProductVersion:\s(?<product_version>.+)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            module.productVersion = matches[0].Groups["product_version"].Value.Trim();
                    }
                    else if (line.Contains("FileDescription:"))
                    {
                        pattern = @"FileDescription:\s(?<file_description>.+)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            module.fileDescription = matches[0].Groups["file_description"].Value.Trim();
                    }
                }
            }
            if (module != null)
                Modules.Add(module);
        }

        public UInt64 GetStartAddress(string moduleName)
        {
            string moduleNameNoExt = Path.GetFileNameWithoutExtension(moduleName);
            foreach (ModuleInfo module in Modules)
            {
                if (String.Compare(moduleNameNoExt, module.moduleName, true) == 0)
                    return Utils.StrHexToUInt64(module.startAddr);
            }
            throw new Exception("Cannot get start address of module " + moduleName);
        }
    }
}
