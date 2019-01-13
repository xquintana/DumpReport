namespace DumpReport
{
    class Resources
    {
        static public string appTitle   = "DumpReport v1.0";
        static public string configFile = "DumpReportCfg.xml";

        #region css
        static public string css = @"body {
    font-family: verdana, arial, sans-serif;
    font-size: 12px;
    margin-left: 25px;
}
h1 {
    color: DarkBlue;
    font-family: verdana, arial, sans-serif;
    font-size: 20px;
    margin-left: -15px;
    margin-bottom: 0px;
}
h2 {
    color: DarkBlue;
    font-family: verdana, arial, sans-serif;
    font-size: 14px;
    margin-left: -15px;
}
button {
    padding: 2px 10px;
    font-family: verdana, arial, sans-serif;
    font-size: 12px;
    border-radius: 3px;
    border: 1px solid #7F7F7F;
}
button:hover {
    background-color: DarkGray;
    color: white;
}
button:focus {
    outline:0;
}
.toggle-button {
    padding: 0px 0px;
    margin-right: 3px;
    font-size: 10px;
    font-weight: bold;
    height: 15px;
    width: 15px;
    font-family: 'Courier New', monospace;
    text-align: center;
    vertical-align: middle;
    }
.toggle-header {
    margin-bottom: 0;
    margin-left: 0;
    margin-top: 3px;
    padding: 0;
    font-size: 12px;
    vertical-align: top;
}
.toggle-header td {
    margin-bottom: 0;
    padding: 0;
    font-family: verdana, arial, sans-serif;
    vertical-align: top;
}
.report-table {
    margin-left: 15px;
    margin-top: 0px;
    margin-bottom: 0px;
    padding: 0.5em;
}
.report-table td {
    text-align: left;
    padding: 0.3em;
    font-size: 11px;
}
.report-table th {
    font-size: 12px;
    text-align: left;
    padding: 0.3em;
    background-color: #4f81BD;
    color: white;
}
.report-table tr {
    font-size: 12px;
    height: 1em;
}
.report-table tr:nth-child(even)
{
    background-color: #eee;
}
.report-table tr:nth-child(odd)
{
    background-color:#fff;
}
.sourcecode-frame {
    font-weight: bold;
    color: black;
}
.thread-id {
    font-family: 'Consolas', 'Courier New', monospace;
    font-weight: normal;
}
";
        #endregion

        #region javascript
        static public string scripts = @"
function expand(divName, buttonName) {
    if (document.getElementById(divName) === null) return;
    document.getElementById(divName).style.display = 'block';
    document.getElementById(buttonName).firstChild.data  = '-';
}
function collapse(divName, buttonName) {
    if (document.getElementById(divName) === null) return;
    document.getElementById(divName).style.display = 'none';
    document.getElementById(buttonName).firstChild.data  = '+';
}
function toggle(divName, buttonName) {
    var div = document.getElementById(divName);
    if (div === null) return;
    if (div.style.display === 'none') {
        expand(divName, buttonName);
    }
    else {
        collapse(divName, buttonName);
    }
}
function setVisibility(show) {
    var divName = '';
    var buttonId = '';
    for (var i = 0; i < numThreads; i++)
    {
        divName = 'divThread' + i.toString();
        buttonId = 'btThread' + i.toString();
        if (show == true) {
            expand(divName, buttonId);
        }
        else {
            collapse(divName, buttonId);
        }
    }
}
";
        #endregion

        #region xml
        static public string xml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<Config>
    <Debugger exe64="""" exe32="""" timeout=""10"" />
    <Pdb folder="""" />
    <Style file="""" />
    <Report file="".\DumpReport.html"" show=""1"" />
    <Log folder="""" clean=""0""/>
    <SymbolCache folder="""" />
    <SourceCodeRoot folder="""" />
</Config >
";
        #endregion

        #region help

        static public string appHelp = @"
Creates an HTML report from a user-mode dump file using WinDBG or CDB. It shows the call stacks of all threads,
exception details (if any), the loaded modules and the environment details of the target machine.

DumpReport /DUMPFILE dump_file [/PDBFOLDER pdb_folder] [/REPORTFILE html_file] [/SHOWREPORT value] [/QUIET value]

    /DUMPFILE:   Full path of the dump file to read.
    /PDBFOLDER:  (optional) Folder containing the corresponding PDBs.
                 If not specified, PDB files are expected to be in the dump's folder.
    /REPORTFILE: (optional) Full path of the HTML report file. It can also be specified in the config file.
                 By default, a file named 'DumpReport.html' is created in the execution folder.
    /SHOWREPORT: (optional) If the value is 1, the report automatically opens in the default browser.
    /QUIET:      (optional) If 1, the console window does not show progress messages.

Example:
    DumpReport /DUMPFILE ""C:\dump\crash.dmp"" /PDBFOLDER ""C:\dump"" /SHOWREPORT 1

If the dump file is the only parameter, the call can be simplified:
    DumpReport ""C:\dump\crash.dmp""

It is also possible to drag and drop the dump directly onto the executable.

Any value containing spaces must be enclosed in double quotes.
Providing the PDB files is not necessary but improves the information of the call stack traces.
The location of the debbuggers to use and other options must be defined in the XML
configuration file ({0}).

Run 'DumpReport /CONFIG HELP' for more information on the XML configuration file.
Run 'DumpReport /STYLE HELP' for information on customizing the report's style.";

        static public string xmlHelpIntro = @"
A file named '{0}' must exist together with the executable.
This file contains the default values of the parameters.
Some can be overriden by command line.";

        static public string xmlHelpNodes = @"
<Config>: Main node.
<Debugger>:  Supported debuggers are WinDbg.exe and CDB.exe.
    exe64:   Full path of the 64-bit version debugger.
    exe32:   Full path of the 32-bit version debugger.
    timeout: Maximum number of minutes to wait for the debugger to finish.
<Pdb>:
    folder:  Folder containing the PDB files. If not specified, PDB files are expected to be
             in the same location as the dump file.
<Style>:
    file:    Full path of a custom CSS file to use.
             Run 'DumpReport /STYLE HELP' for more information about the report's CSS style.
<Report>:
    file:    Full path of the report file to be created.
    show:    If set to 1, the report will be displayed automatically in the default browser.
<Log>:
    folder:  Folder where the debugger log files will be created.
             If not specified, log files are created in the same location as the dump file.
             The name of the log files is the name of the dump file appended with '.log'
    clean:   Indicates whether the log files should be deleted after being processed.
<SymbolCache>:
    folder:  Folder to use as symbol cache. If not specified, the debugger will use its default
             symbol cache (e.g: C:\ProgramData\dbg)
<SourceCodeRoot>:
    folder:  The report will emphasize the frames whose source file's path contains this folder.

Run 'DumpReport /CONFIG CREATE' to create a default config file.
";
        static public string cssHelp = @"
CSS styles:

body:          Default style for the HTML document.
h1:            Title header.
h2:            Section header.
button:        Default button style.
toggle-button: Style of the Expand/Collapse button (+/-)
toggle-header: Auxiliary table that contains a toggle button and a label
               that describes an area that can be expanded or collapsed.
report-table:  Style for tables showing thread call stacks, loaded modules or
               environment variables. By default, a striped style is used.
sourcecode-frame: Call stack frame associated to the source code root.
thread-id:     Style for the thread identifier and intruction pointer.

Run 'DumpReport /STYLE CREATE' to create a sample CSS file (style.css).";

        #endregion

        #region debuggerScripts

        static public string dbgScriptInit = @".logopen /u ""[LOG_FILE]""
||
.foreach (module {lm1m} ) { .if ($sicmp(""${module}"",""wow64"") == 0) { .echo WOW64 found; } }
.effmach
.logclose
";
        static public string dbgScriptMain = @".logopen /u ""[LOG_FILE]""
||
.lines -e
.foreach (module {lm1m} ) { .if ($sicmp(""${module}"",""wow64"") == 0) { .load soswow64; .echo WOW64 found; .effmach x86;  } }
.effmach
.cordll -ve -u -l
.chain
.echo > !eeversion
!eeversion
.echo >>> TARGET INFO
!envvar COMPUTERNAME
!envvar USERNAME
.echo PROCESS_ID:
|.
.echo TARGET:
vertarget
!peb
.echo >>> MANAGED THREADS
!Threads
.echo >>> MANAGED STACKS
.block { ~* e !clrstack }
.echo >>> EXCEPTION INFO
.exr -1
.echo EXCEPTION THREAD:
~#
.echo >>> HEAP
!heap
.echo >>> INSTRUCTION POINTERS
.block { ~* e ? [INSTRUCT_PTR] }
.echo >>> THREAD STACKS
~* kv n
.echo >>> LOADED MODULES
lmov
.echo >>> END OF LOG
.logclose
";
        static public string dbgUnhandledExceptionFilter32 = @".logopen /u ""[LOG_FILE]""
||
.block { .effmach x86 }
.lines -e
r @$t0 = 0;
.foreach(value {dd[FIRST_PARAM]}){ .if (@$t0 == 1) { .exr value }; r @$t0 = @$t0 + 1; }
.logclose
";
        static public string dbgKiUserExceptionDispatch = @".logopen /u ""[LOG_FILE]""
||
.exr [CHILD_SP] + @@c++(sizeof(ntdll!_CONTEXT)) + 0x20
.logclose
";
        // Used both with 32 and 64 bits dumps
        static public string dbgRtlDispatchException = @".logopen /u ""[LOG_FILE]""
||
.exr [THIRD_PARAM]
.logclose
";
        static public string dbgWerpReportFault64 = @".logopen /u ""[LOG_FILE]""
||
r @$t0 = 0;
.foreach(value {dq[FOURTH_PARAM]}){ .if (@$t0 == 1) { .exr value; .break; }; r @$t0 = @$t0 + 1; }
.logclose
";
        public static string GetDbgScriptMain(bool is32bitDump)
        {
            return dbgScriptMain.Replace("[INSTRUCT_PTR]", is32bitDump ? "@eip" : "@rip");
        }

        #endregion
    }
}
