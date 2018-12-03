using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;


namespace DumpReport
{
    /**
     * The 'Info' classes represent units of information present in the dump log.
     * For example, they store data about a single thread, a loaded module,..etc
     * This information is extracted by means of classes of type 'Parser', declared later on.
     * */

    /// <summary>
    /// Stores information about a managed stack frame.
    /// </summary>
    class ManagedStackFrameInfo
    {
        public string childSP;
        public string instructPtr;
        public string callSite;
    }
    /// <summary>
    /// Stores information about a managed stack.
    /// </summary>
    class ManagedStackInfo
    {
        public string thread_id;
        public int thread_num;
        public List<ManagedStackFrameInfo> frames;
    }
    /// <summary>
    /// Stores information about a managed thread.
    /// </summary>
    class ManagedThreadInfo
    {
        public int thread_num; // Thread number in total threads
        public int thread_num_managed; // Thread number in managed threads
        public string thread_id;
        public string thread_obj;
        public string state;
        public string gc_mode;
        public string gc_alloc_ctx;
        public string domain;
        public string lock_count;
        public string apt;
        public string exception;
    }
    /// <summary>
    /// Stores information about a stack frame.
    /// </summary>
    class StackFrameInfo
    {
        public bool inline;
        public string childSP;
        public string return_address;
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
        public int thread_num; // index of the thread in the thread list
        public string thread_id;
        public string instruct_ptr;
        public List<StackFrameInfo> stack;
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
    * The 'Parser' classes extract information from a specific section in the debuggers's output file.
    */

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
        public bool   Wow64Found { get; set; }
        public bool   SosLoaded { get; set; }
        public string ClrVersion { get; set; }

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
                else if (lines[idx].Contains("eeversion"))
                {
                    while (++idx < lines.Count && lines[idx].Contains("*** WARNING")); //skip noise
                    pattern = @"(?<clr_ver>[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)";
                    matches = Regex.Matches(lines[idx], pattern);
                    if (matches.Count == 1)
                        ClrVersion = matches[0].Groups["clr_ver"].Value;
                    else ClrVersion = lines[idx];
                }
            }
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
                    while (++idx < lines.Count && !lines[idx].Contains("Debug session time"));
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
        List<ManagedThreadInfo> threads = new List<ManagedThreadInfo>();

        public override void Parse()
        {
            Debug.Assert(lines.Count > 0);
            ManagedThreadInfo managedThread = null;
            foreach (string line in lines)
            {
                pattern = @"(?<thread_num>[0-9]+)\s+(?<thread_num_man>[0-9]+)\s+(?<osid>\w+)\s+(?<thread_obj>\w+)\s+(?<state>\w+)\s+(?<gc_mode>\w+)\s+(?<gc_alloc_ctx>[\w,:]+)\s+(?<domain>\w+)\s+(?<lock_count>\w+)\s+(?<apt>\w+)\s+(?<exception>.+)";
                matches = Regex.Matches(line, pattern);

                if (matches.Count == 1)
                {
                    managedThread = new ManagedThreadInfo
                    {
                        thread_num         = Convert.ToInt32(matches[0].Groups["thread_num"].Value),
                        thread_num_managed = Convert.ToInt32(matches[0].Groups["thread_num_man"].Value),
                        thread_id          = matches[0].Groups["osid"].Value,
                        thread_obj         = matches[0].Groups["thread_obj"].Value,
                        state              = matches[0].Groups["state"].Value,
                        gc_mode            = matches[0].Groups["gc_mode"].Value,
                        gc_alloc_ctx       = matches[0].Groups["gc_alloc_ctx"].Value,
                        domain             = matches[0].Groups["domain"].Value,
                        lock_count         = matches[0].Groups["lock_count"].Value,
                        apt                = matches[0].Groups["apt"].Value,
                        exception          = matches[0].Groups["exception"].Value
                    };
                    threads.Add(managedThread);
                }
            }
        }

        public int GetFaultingThread()
        {
            foreach (ManagedThreadInfo thread in threads)
            {
                matches = Regex.Matches(thread.exception, "exception", RegexOptions.IgnoreCase);
                if (matches.Count == 1)
                    return Convert.ToInt32(thread.thread_num);
            }
            return -1;
        }
    }

    /// <summary>
    /// Extracts information about the managed stacks.
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
                        pattern = @"(?<child_SP>[\w]{16,})\s(?<ip>[\w+]{16,})\s(?<call_site>.+)";
                        matches = Regex.Matches(line, pattern);

                        if (matches.Count == 1)
                        {
                            if (stack == null)
                            {
                                stack = new ManagedStackInfo();
                                stack.thread_id = thread_id;
                                stack.thread_num = thread_num;
                                stack.frames = new List<ManagedStackFrameInfo>();
                                Stacks.Add(stack);
                            }

                            ManagedStackFrameInfo manStackFrame = new ManagedStackFrameInfo();
                            manStackFrame.childSP = matches[0].Groups["child_SP"].Value;
                            manStackFrame.instructPtr = matches[0].Groups["ip"].Value;
                            manStackFrame.callSite = matches[0].Groups["call_site"].Value;

                            if (manStackFrame.callSite.Contains("*** WARNING:"))
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
                if (stack.thread_num == threadNum)
                    return stack;
            }
            return null;
        }
    }

    /// <summary>
    /// Extracts information about the exception record, if present.
    /// </summary>
    class ExcepRecParser : Parser
    {
        public UInt64 ExceptionAddr { get; set; }    // Exception address as number
        public string ExceptionAddrHex { get; set; } // Exception address as hexadecimal string
        public string ExceptionFrame { get; set; }
        public string ExceptionCode { get; set; }
        public string ExceptionDesc { get; set; }
        public string ExceptionModule { get; set; }

        public bool ContainsExceptionRecord() { return ExceptionAddr != 0;  }

        public void GetExceptionInfo(ExceptionInfo exceptionInfo)
        {
            exceptionInfo.code = ExceptionCode;
            exceptionInfo.description = ExceptionDesc;
            exceptionInfo.address = ExceptionAddr;
            exceptionInfo.module = ExceptionModule;
            exceptionInfo.frame = ExceptionFrame;
        }

        public override void Parse()
        {
            ExceptionAddr = 0;
            ExceptionAddrHex = String.Empty;
            ExceptionFrame = String.Empty;
            ExceptionCode = String.Empty;
            ExceptionDesc = String.Empty;
            ExceptionModule = String.Empty;

            Debug.Assert(lines.Count > 0);
            foreach (string line in lines)
            {
                if (line.Contains("ExceptionAddress"))
                {
                    pattern = @"ExceptionAddress: (?<excep_addr>[\w]+)\s\((?<excep_frame>.+)\)";
                    matches = Regex.Matches(line, pattern);
                    if (matches.Count == 1)
                    {
                        ExceptionAddrHex = matches[0].Groups["excep_addr"].Value;
                        ExceptionAddr = Utils.StringHexToUInt64(ExceptionAddrHex);
                        ExceptionFrame = matches[0].Groups["excep_frame"].Value;
                        if (ExceptionFrame.Contains("!"))
                        {
                            string[] parts = ExceptionFrame.Split('!');
                            if (parts.Length >= 2) ExceptionModule = parts[0];
                        }
                    }
                }
                else if (line.Contains("ExceptionCode"))
                {
                    pattern = @"ExceptionCode: (?<excep_code>[\w]+)";
                    matches = Regex.Matches(line, pattern);
                    if (matches.Count == 1)
                        ExceptionCode = matches[0].Groups["excep_code"].Value;
                    if (line.Contains("("))
                    {
                        pattern = @"\s\((?<excep_desc>.+)\)";
                        matches = Regex.Matches(line, pattern);
                        if (matches.Count == 1)
                            ExceptionDesc = matches[0].Groups["excep_desc"].Value;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extracts information about the threads found in the dump.
    /// </summary>
    class ThreadParser : Parser
    {
        public List<ThreadInfo> Threads { get; set; }
        List<string> ExceptionKeywords = new List<string>();

        // Creates a list of keywords likely to be present in the stack of a faulting thread
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

        public bool GetUnhandledExceptionFilterInfo(ref int threadNum, out string arg1)
        {
            arg1 = String.Empty;
            if (GetFrameByKeyword("UnhandledExceptionFilter", out StackFrameInfo frameInfo, out ThreadInfo threadInfo))
            {
                threadNum = threadInfo.thread_num;
                arg1 = frameInfo.argsToChild1;
                return true;
            }
            return false;
        }

        public bool GetKiUserExceptionDispatchInfo(out string childSP)
        {
            childSP = String.Empty;
            if (GetFrameByKeyword("KiUserExceptionDispatch", out StackFrameInfo frameInfo, out ThreadInfo threadInfo))
            {
                childSP = frameInfo.childSP;
                return true;
            }
            return false;
        }

        public bool GetRtlDispatchExceptionInfo(out string arg3)
        {
            arg3 = String.Empty;
            if (GetFrameByKeyword("RtlDispatchException", out StackFrameInfo frameInfo, out ThreadInfo threadInfo))
            {
                arg3 = frameInfo.argsToChild3;
                return true;
            }
            return false;
        }

        public void ReplaceThreadStack(int threadNum, List<StackFrameInfo> stack)
        {
            foreach (ThreadInfo threadInfo in Threads)
            {
                if (threadInfo.thread_num == threadNum)
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
                        thread.stack = new List<StackFrameInfo>();
                        Threads.Add(thread);
                        thread.thread_num = Convert.ToInt32(matches[0].Groups["thread_num"].Value);
                        thread.thread_id = matches[0].Groups["thread_id"].Value;
                    }
                }
                else if (line.Contains("Inline Function"))
                {
                    string[] parts = line.Split(new string[] { "--------" }, StringSplitOptions.None);
                    StackFrameInfo frame = new StackFrameInfo();
                    frame.callSite = parts[parts.Length - 1].Trim(' ', ':');
                    frame.inline = true;
                    ParseCallSite(frame);
                    thread.stack.Add(frame);
                }
                else
                {
                    if (Program.is32bitDump) // 32-bits log
                        pattern = @"^\w+\s(?<child>\w+)\s(?<return_addr>\w+)\s(?<args_to_child1>\w+)\s(?<args_to_child2>\w+)\s(?<args_to_child3>\w+)\s(?<call_site>.*)";
                    else // 64-bits log
                        pattern = @"(?<child>[\w,`]+)\s(?<return_addr>[\w,`]+)\s:\s(?<args_to_child1>[\w,`]+)\s(?<args_to_child2>[\w,`]+)\s(?<args_to_child3>[\w,`]+)\s(?<args_to_child4>[\w,`]+)\s:\s(?<call_site>.*)";

                    matches = Regex.Matches(line, pattern);

                    if (matches.Count == 1)
                    {
                        StackFrameInfo frame = new StackFrameInfo
                        {
                            childSP = matches[0].Groups["child"].Value.Replace("`",string.Empty),
                            return_address = matches[0].Groups["return_addr"].Value.Replace("`", string.Empty),
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

        void ParseCallSite(StackFrameInfo frame)
        {
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

        public int GetThreadByRetAddr(UInt64 retAddress)
        {
            int threadCounter = 0;
            foreach (ThreadInfo thread in Threads)
            {
                foreach (StackFrameInfo frame in thread.stack)
                {
                    if (frame.inline == true)
                        continue;
                    if (Utils.StringHexToUInt64(frame.return_address) == retAddress)
                        return threadCounter;
                }
                threadCounter++;
            }
            return -1;
        }

        // Returns the index of the first thread that contains a keyword in the stack trace
        public bool GetFrameByKeyword(string keyword, out StackFrameInfo frameInfo, out ThreadInfo threadInfo)
        {
            frameInfo = null;
            threadInfo = null;
            foreach (ThreadInfo threadInfoAux in Threads)
                foreach (StackFrameInfo frameInfoAux in threadInfoAux.stack)
                    if (frameInfoAux.callSite.Contains(keyword))
                    {
                        frameInfo = frameInfoAux;
                        threadInfo = threadInfoAux;
                        return true;
                    }
            return false;
        }

        // Returns the index of the first thread that contains an exception keyword
        public int GuessFaultingThread()
        {
            int threadCounter = 0;
            foreach (ThreadInfo thread in Threads)
            {
                foreach (StackFrameInfo frame in thread.stack)
                {
                    foreach (string keyword in ExceptionKeywords)
                    if (frame.callSite.Contains(keyword))
                        return threadCounter;
                }
                threadCounter++;
            }
            return -1;
        }

        public ThreadInfo GetThread(int threadNum)
        {
            return Threads[threadNum];
        }

        public void AddManagedInfo(int threadNum, ManagedStackInfo managedStack)
        {
            // Frames are matched by childSP.
            List<StackFrameInfo> stack = Threads[threadNum].stack;
            foreach (ManagedStackFrameInfo managedFrame in managedStack.frames)
            {
                foreach (StackFrameInfo frame in stack)
                {
                    if (managedFrame.childSP == frame.childSP)
                    {
                        frame.callSite = string.Format("(managed)!{0}", managedFrame.callSite);
                        ParseCallSite(frame);
                    }
                }
            }
        }

        public void SetInstructionPointers(List<string> instPtrs)
        {
            if (instPtrs.Count != Threads.Count)
                return;
            for (int i=0; i< Threads.Count; i++)
                Threads[i].instruct_ptr = instPtrs[i];
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
                    return Utils.StringHexToUInt64(module.startAddr);
            }
            throw new Exception("Cannot get start address of module " + moduleName);
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
                if (Utils.StringHexToUInt64(hexString) == faultAddress)
                    return threadCount;
                threadCount++;
            }
            return -1;
        }
    }
}
