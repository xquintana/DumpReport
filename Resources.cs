namespace DumpReport
{
    class Resources
    {
        static public string appTitle   = "DumpReport v1.0";
        static public string configFile = "DumpReportCfg.xml";

        #region css
        static public string css = @"body {
	font-family: Verdana;
	font-size: 12px;
    margin-left: 25px;
}
h1 {
	color: DarkBlue;
	font-family: Verdana;
	font-size: 20px;
    margin-left: -15px;
    margin-bottom: 0px;
}
h2 {
	color: DarkBlue;
	font-family: Verdana;
	font-size: 14px;
    margin-left: -15px;
}
a:link {
	color: #000000;
	text-decoration: none;
	font-size: 12px
}
a:visited {
	color: #000000;
	text-decoration: none;
	font-size: 12px
}
a:hover {
	color: #0000FF;
	text-decoration: none;
	font-size: 12px
}
a:active {
	color: #000000;
	text-decoration: none;
	font-size: 12px
}
.toggleButton {
    padding: 0px 0px;
    font-size: 10px;
	font-weight: bold;
	height: 15px;
	width: 15px;
	font-family: 'Courier New';
    }
.toggleHeader {
	margin-left: 0;
	padding: 0;
    font-size: 12px;
}
.toggleHeader td
{
    padding: 0;
    font-family: Verdana;
}
.report_table {
	margin-left: 30px;
	margin-top: 0px;
	margin-bottom: 0px;
	padding: 0.5em;
}
.report_table td {
	text-align: left;
	padding: 0.3em;
    font-size: 11px;
}
.report_table th {
	font-size: 12px;
	text-align: left;
	padding: 0.3em;
	background-color: #4f81BD;
	color: white;
}
.report_table tr {
	font-size: 12px;
	height: 1em;
}
.report_table tr:nth-child(even)
{
    background-color: #eee;
}
.report_table tr:nth-child(odd)
{
    background-color:#fff;
}
.sourcecode_frame {
	font-weight: bold;
    color: black;
}
";
        #endregion

        #region scripts
        static public string scripts = @"
function expand(divName, buttonName) {
	document.getElementById(divName).style.display = 'block';
	document.getElementById(buttonName).firstChild.data  = '-';
}
function collapse(divName, buttonName) {
	document.getElementById(divName).style.display = 'none';
	document.getElementById(buttonName).firstChild.data  = '+';
}
function toggle(divName, buttonName) {
	var x = document.getElementById(divName);
	if (x.style.display === 'none') {
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
    <Debugger exe64="""" exe32="""" timeout=""10"" visible=""0"" />
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
Creates an HTML report from a user-mode dump file using WinDBG or CDB. It shows the stack trace of all threads,
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
Providing the PDB files is not necessary but improves the information of the stack traces.
The location of the debbuggers to use and other options must be defined in the XML
configuration file ({0}).
Run 'DumpReport /CONFIG HELP' for more information on the XML configuration file.
Run 'DumpReport /STYLE HELP' for information on customizing the report's style.";

        static public string xmlHelp = @"
A file named '{0}' must exist together with the executable.
This file contains the default values of the parameters.
Some can be overriden by command line.

Sample:

{1}
Nodes:

<Config>: Main node.
<Debugger>:  Supported debuggers are WinDbg.exe and CDB.exe.
    exe64:   Full path of the 64-bit version debugger.
    exe32:   Full path of the 32-bit version debugger.
    timeout: Maximum number of minutes to wait for the debugger to finish.
    visible: Runs the debugger in visible (value 1) or hidden (value 0) mode.
             This feature is only supported by the CDB debugger.
<Pdb>:
    folder:  Folder containing the PDB files. If not specified, PDB files are expected to be
             in the same location as the dump file.
<Style>:
    file:    Full path of a custom CSS file to use.
             Run 'DumpReport /STYLE HELP' for more information about the report's CSS style.
<Report>:
    file:    Full path of the report to be created.
    show:    If set to 1, the report will be displayed automatically in the default browser.
<Log>:
    folder:  Folder where the debug log files will be created.
             If not specified, the log files are created in the same location as the dump file.
             The name of the log files is the name of the dump file appended with '.log'
    clean:   Indicates whether the log files should be deleted after being processed.
<SymbolCache>:
    folder:  Folder to use as symbol cache. If not specified, the debugger will use its default
             symbol cache (e.g: C:\ProgramData\dbg)
<SourceCodeRoot>:
    folder:  The report will emphasize the frames whose source file contains this root folder.

Run 'DumpReport /CONFIG CREATE' to create a default config file.
";
        static public string cssHelp = @"
CSS styles:

body:         Default style for the HTML document
h1:           Title header
h2:           Section header
a:            Hyperlink
toggleButton: Style of the Expand/Collapse button (+/-)
toggleHeader: Auxiliary table that contains a toggle button and a label
              that describes the area that can be expanded or collapsed.
report_table: Style for tables showing thread stacks, loaded modules or
              environment variables. By default, a striped style is used.
sourcecode_frame: Stack frame associated to a source code root.

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
.foreach (module {lm1m} ) { .if ($sicmp(""${module}"",""wow64"") == 0) { .echo WOW64 found; .effmach x86; } }
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
.echo >>> EXCEPTION RECORD
.exr -1
.echo >>> INSTRUCTION POINTERS
.block { ~* e ? [INSTRUCT_PTR] }
.echo >>> THREAD STACKS
~* kv n
.echo >>> LOADED MODULES
lmov
.echo >>> END OF LOG
.logclose
";

        static public string dbgScriptExcepRec32 = @".logopen /u ""[LOG_FILE]""
||
.block { .effmach x86 }
.lines -e
~[NUM_THREAD]s
r @$t0 = 0;
.foreach(value {dd[FIRST_PARAM]}){ .if (@$t0 == 1) { .exr value }; .if (@$t0 == 2) { .cxr value }; r @$t0 = @$t0 + 1; }
.logclose
";

        static public string dbgScriptExcepRec64 = @".logopen /u ""[LOG_FILE]""
||
.exr [CHILD_SP] + @@c++(sizeof(ntdll!_CONTEXT)) + 0x20
.logclose
";

        static public string dbgScriptExcepRecRtl = @".logopen /u ""[LOG_FILE]""
||
.exr [THIRD_PARAM]
.logclose
";
        public static string GetDbgScriptMain(bool is32bitDump)
        {
            return dbgScriptMain.Replace("[INSTRUCT_PTR]", is32bitDump ? "@eip" : "@rip");
        }

        public static string GetDbgScriptExcepRec32(int threadNum, string arg1)
        {
            return dbgScriptExcepRec32.Replace("[NUM_THREAD]", threadNum.ToString()).Replace("[FIRST_PARAM]", arg1);
        }

        public static string GetDbgScriptExcepRec64(string childSP)
        {
            return dbgScriptExcepRec64.Replace("[CHILD_SP]", childSP);
        }

        public static string GetDbgScriptExcepRecRtl(string arg3)
        {
            return dbgScriptExcepRecRtl.Replace("[THIRD_PARAM]", arg3);
        }

        #endregion
    }
}
