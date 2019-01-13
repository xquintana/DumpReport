using System;
using System.Collections.Generic;
using System.IO;

namespace DumpReport
{
    /// <summary>
    /// Creates the report as an HTML file.
    /// The file has three main areas: CSS, the body and Javascript code.
    /// Most of its methods are used to write extracted data in HTML format.
    /// </summary>
    class Report : IDisposable
    {
        const string FRAME_STYLE_SOURCECODE = "sourcecode-frame";

        StreamWriter stream; // Stream representing the output HTML file (report)
        Config config; // Stores the paramaters of the application

        public Report(Config config)
        {
            this.config = config;
        }

        // Creates the report file
        public void Open(string file)
        {
            stream = new StreamWriter(Utils.GetAbsolutePath(file));
            BeginDocument();
        }

        // Returns true if the report file is opened
        public bool IsOpen() { return (stream != null); }

        // Closes the report file
        public void Close()
        {
            try
            {
                if (stream != null)
                {
                    EndDocument();
                    stream.Close();
                    stream.Dispose();
                }
            }
            catch (Exception ex)
            {
                Program.ShowError(ex.Message);
            }
        }

        // Releases the stream
        public void Dispose()
        {
            if (stream != null)
                stream.Dispose();
        }

        // Initializes the HTML file
        void BeginDocument()
        {
            if (stream == null) return;
            stream.WriteLine("<html>");
            WriteHTMLHeader();
            stream.WriteLine("<body>");
            stream.WriteLine("<h1>Dump Report</h1><br>");
            WriteValue("Report Timestamp", DateTime.Now.ToString());
            if (config.DumpFile != null)
                WriteValue("Dump File", config.DumpFile);
        }

        // Writes the closing HTML tags
        void EndDocument()
        {
            if (stream == null) return;
            stream.WriteLine("<br><br></body>");
            stream.WriteLine("</html>");
        }

        public void Write(string text)
        {
            if (stream == null) return;
            stream.WriteLine(text + "<br>");
        }

        public void WriteError(string text)
        {
            if (stream == null) return;
            string str = string.Format("<br><b><font color='red'>ERROR: {0}</font></b><br>", text);
            stream.WriteLine(str);
        }

        public void WriteSectionTitle(string text)
        {
            if (stream == null) return;
            string str = string.Format("<h2>{0}</h2>", text);
            stream.WriteLine(str);
        }

        void WriteValue(string name, string value)
        {
            string line = string.Format("<b>{0}: </b>{1}", name, value);
            Write(line);
        }

        void WriteHTMLHeader()
        {
            if (stream == null) return;
            stream.WriteLine(string.Format("<header><meta charset='UTF-8'><title>Dump Report [{0}]</title>", Path.GetFileName(config.DumpFile)));
            if (config.StyleFile.Length == 0 || ImportStyle(config.StyleFile) == false)
                WriteStyle(Resources.css);
            stream.WriteLine("</header>");
        }

        // Embeds the CSS code into the HTML file
        void WriteStyle(string style)
        {
            if (stream == null) return;
            stream.WriteLine(string.Format("<style>\n{0}</style>", style));
        }

        // Imports the CSS code from an external file
        bool ImportStyle(string path)
        {
            try
            {
                string style = File.ReadAllText(path);
                WriteStyle(style);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void WriteDumpInfo(DumpInfoParser dumpInfo)
        {
            if (stream == null) return;

            if (dumpInfo.DumpBitness != null)
            {
                string dumpType = dumpInfo.DumpBitness;
                if (dumpInfo.Wow64Found)
                    dumpType += " (64-bit dump)";
                WriteValue("Dump Architecture", dumpType);
            }

            if (dumpInfo.SosLoaded && dumpInfo.ClrVersion != null && dumpInfo.ClrVersion.Length > 0)
                WriteValue("CLR Version", dumpInfo.ClrVersion);
            else
                WriteValue("CLR Version", "None (SOS extension not loaded)");
        }

        public void WriteTargetInfo(TargetInfoParser targetInfo)
        {
            if (stream == null) return;

            if (targetInfo.CommandLine != null)
                WriteValue("Command Line", targetInfo.CommandLine);
            if (targetInfo.ProcessId != null)
                WriteValue("Process Id", string.Format("{0} ({1})", targetInfo.ProcessId, Utils.StrHexToUInt64(targetInfo.ProcessId)));
            if (targetInfo.ComputerName != null)
                WriteValue("Computer Name", targetInfo.ComputerName);
            if (targetInfo.UserName != null)
                WriteValue("User Name", targetInfo.UserName);

            WriteValue("Operating System", targetInfo.OsInfo);

            if (targetInfo.Environment.Keys.Count > 0)
            {
                Table table = new Table("report-table");
                table.EmphasizeFirstCol = true;
                table.AddHeader(new string[] { "Environment Variable", "Value" });
                foreach (string envvar in targetInfo.Environment.Keys)
                    table.AddRow(new string[] { envvar, targetInfo.Environment[envvar] });
                InsertToggleContent("Environment Variables", table.Serialize());
            }
        }

        public void WriteModuleInfo(List<ModuleInfo> modules)
        {
            if (stream == null) return;
            Table table = new Table("report-table");
            table.AddHeader(new string[] { "Start Address", "End Address", "Module Name", "Timestamp", "Time",
                "Path", "File Version", "Product Version", "Description", "PDB status", "PDB Path" });
            foreach (ModuleInfo module in modules)
                table.AddRow(new string[] { module.startAddr.TrimStart('0'), module.endAddr.TrimStart('0'), module.moduleName, module.timestamp, Utils.TimestampToLocalDateTime(module.timestamp),
                    module.imagePath, module.fileVersion, module.productVersion, module.fileDescription, module.pdbStatus, module.pdbPath }, "td");
            InsertToggleContent("Loaded Modules", table.Serialize());
        }

        public void WriteNotes(List<string> notes)
        {
            if (notes.Count > 0)
                Write("<br><b>Notes:</b>");
            foreach (string note in notes)
                Write(note);
        }

        public void WriteExceptionInfo(ExceptionInfo exceptionInfo)
        {
            if (exceptionInfo.description != null && exceptionInfo.description.Length > 0)
                WriteValue("Exception", exceptionInfo.description);
            if (exceptionInfo.module != null && exceptionInfo.module.Length > 0)
                WriteValue("Module", exceptionInfo.module);
            if (exceptionInfo.address > 0)
                WriteValue("Exception Address", Utils.UInt64toStringHex(exceptionInfo.address));
            if (exceptionInfo.frame != null && exceptionInfo.frame.Length > 0)
                WriteValue("Faulting Frame", EscapeSpecialChars(exceptionInfo.frame));
        }

        public void WriteFaultingThreadInfo(ThreadInfo faultThread)
        {
            List<ThreadInfo> threadList = new List<ThreadInfo>();
            threadList.Add(faultThread);
            WriteThreadInfo(threadList, true);
        }

        // Receives a list of threads with a common call stack
        public void WriteThreadInfo(List<ThreadInfo> threads, bool isFaultThread = false)
        {
            int counter = 0;
            if (stream == null) return;
            if (threads.Count == 0)
                return;

            // Expand/collapse buttons
            string divName = string.Format("divThread{0}{1}", threads[0].threadNum, isFaultThread ? "_fault" : String.Empty);
            string buttonId = string.Format("btThread{0}{1}", threads[0].threadNum, isFaultThread ? "_fault" : String.Empty);
            string threadLabel = "";
            // Thread Id and instruction pointer
            for (int i = 0; i < threads.Count; i++)
            {
                if (i > 0) threadLabel += "<br>";
                threadLabel += string.Format("Thread #{0}&nbsp;<span class='thread-id'>(<b>id=</b>0x{1}/{2}) <b>{3}:</b>{4}</span>",
                    threads[i].threadNum, threads[i].threadId, UInt32.Parse(threads[i].threadId, System.Globalization.NumberStyles.HexNumber),
                    Program.is32bitDump ? "EIP" : "RIP", threads[i].instructPtr);
            }
            // Thread stack
            List<FrameInfo> stack = threads[0].stack;
            Table table = new Table("report-table");
            table.AddHeader(new string[] { "", "Module", "Function", "File", "Line" });
            foreach (FrameInfo frame in stack)
            {
                table.AddRow(new string[] { counter.ToString(), frame.module, EscapeSpecialChars(frame.function), frame.file, frame.line },
                    "td", GetStackFrameStyle(frame.file));
                counter++;
            }
            InsertToggleContent(threadLabel, table.Serialize(), isFaultThread, buttonId, divName);
        }

        public void WriteAllThreadsMenu()
        {
            if (stream == null) return;
            stream.WriteLine("<table><tr><td><button onclick='setVisibility(true)'>Expand All</button></td>" +
                "<td><button onclick='setVisibility(false)'>Collapse All</button></td></tr></table><br>");
        }

        // Writes the javascript code at the end of the report
        public void WriteJavascript(int numThreads)
        {
            if (stream == null) return;
            stream.WriteLine(string.Format("<script>\r\nvar numThreads = {0};{1}\r\n</script>", numThreads, Resources.scripts));
        }

        // Writes 'content' in an HTML section that can be collapsed or expanded with a button
        void InsertToggleContent(string label, string content, bool show = false, string buttonId = null, string divName = null)
        {
            string id = label.Replace(" ", String.Empty);
            string displayStyle = null;
            if (buttonId == null)
                buttonId = "bt" + id;
            if (divName == null)
                divName = "div" + id;
            displayStyle = show ? "display:initial" : "display:none";

            string toggleCode = string.Format("<table class='toggle-header'><tr><td><button class='toggle-button' id='{0}' " +
                "onclick=\"toggle('{1}','{0}')\">{4}</button></td><td><b>{2}</b></td></tr></table>\n" +
                "<div id='{1}' style='{3}'>",
                buttonId, divName, label, displayStyle, show ? "-" : "+");
            stream.WriteLine(toggleCode);
            stream.WriteLine(content);
            stream.WriteLine("</div>");
        }

        string GetStackFrameStyle(string file)
        {
            if (file != null && file.Length > 0 && config.SourceCodeRoot.Length > 0)
                if (file.ToUpper().Contains(config.SourceCodeRoot))
                    return FRAME_STYLE_SOURCECODE;
            return String.Empty;
        }

        // Escape special HTML characters
        string EscapeSpecialChars(string line)
        {
            return line.Replace("&", "&amp;").
                Replace("<", "&lt;").
                Replace(">", "&gt;").
                Replace("\"", "&quot;").
                Replace("'", "&apos;");
        }

        /// <summary>
        /// Helper class that creates an HTML table
        /// </summary>
        class Table
        {
            string html;
            public bool EmphasizeFirstCol { get; set; }

            public Table(string className = null)
            {
                string attribClass = String.Empty;
                EmphasizeFirstCol = false;
                if (className != null)
                    attribClass = string.Format(" class='{0}'", className);
                html = string.Format("\r\n<table{0}>\r\n", attribClass);
            }
            public void AddHeader(string[] fields)
            {
                AddRow(fields, "th");
            }
            public void AddRow(string[] fields, string cellTag = "td", string style = "")
            {
                string field;
                string styleAttrib = String.Empty;
                if (style.Length > 0)
                    styleAttrib = string.Format(" class='{0}'", style);
                html += "<tr" + styleAttrib + ">";
                for (int i = 0; i < fields.Length; i++)
                {
                    field = fields[i];
                    if (i == 0 && EmphasizeFirstCol == true && cellTag == "td")
                        field = string.Format("<b>{0}</b>", field);
                    html += "<" + cellTag + ">" + field + "</" + cellTag + ">";
                }
                html += "</tr>\r\n";
            }
            public string Serialize(StreamWriter stream = null)
            {
                html += "</table>";
                if (stream != null)
                    stream.WriteLine(html);
                return html;
            }
        }
    }
}
