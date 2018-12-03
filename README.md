
_DumpReport_ is a console application that creates an HTML report from a Windows user-mode dump file, using WinDBG or CDB debuggers.  
It shows the stack traces of all threads, the exception details (if any), the modules loaded in the process and the environment details of the target machine.  
Since it's a command-line application, it can also be called by a script that, for example, sends the report by e-mail or publishes it in the intranet.  
Although it's been mainly designed to analyze crash dumps of Windows applications developed in C++ (managed and/or unmanaged), it can also be used to read hang dumps or .Net dumps.  


## How it works

Basically, the application sends scripts to the debugger and reads the output files in order to extract the information and write it in an HTML file.  


## The Report

The report has the following appearance:

![Report](/screenshot.png)

It begins with the time and date the report was generated.  
Then follow details about the dump itself (path and bitness), the process and the environment of the target machine:

* __CLR Version:__ Version of the loaded .Net framework  (only for dumps with managed code).
* __Command Line:__ Shows the full command line of the process being debugged, including the arguments.
* __Process Id:__ Id that the process was using.
* __Computer Name:__ Name of the target machine.
* __User Name:__ The Windows username that was running the process.
* __Operating System:__ Windows version of the target machine.
* __Environment Variables:__ The values of the environment variables at the target machine may help figuring out the cause of the crash.
* __Loaded Modules:__ A detailed list of the loaded modules (address range, timestamp, path, version, etc). It also indicates whether the corresponding PDB file could be loaded during debugging.

Since the lists of environment variables and loaded modules are usually long, these sections show collapsed by default. Click on the '+' button to unfold.

In case an exception has been found, it is reported in section 'Exception Information' showing the faulting thread.  
Thread details include: thread id, the instruction pointer and the stack trace.
Each frame of the stack shows the module, function and, if available, the source file and line number.

The last section reports the stack trace for all threads. They show collapsed by default. Click on the '+' button of one thread to show its stack, or use the buttons 'Expand All' and 'Collapse All'.

Optionally, frames with source files under a specific root folder can show emphasized. 
If we are the developers of the application being debugged, this can be useful to easily distinguish the frames that belong to our application.


## Command Line

Executing _DumpReport_ without parameters shows the help.  
Parameters are passed in the form '_/PARAMETER value_'

Usage:

	DumpReport /DUMPFILE dump_file [/PDBFOLDER pdb_folder] [/REPORTFILE html_file] [/SHOWREPORT value] [/QUIET value]

    /DUMPFILE:   Full path of the dump file to read.
    /PDBFOLDER:  (optional) Folder containing the corresponding PDBs.
                 If not specified, PDB files are expected to be in the dump's folder.
    /REPORTFILE: (optional) Full path of the HTML report file. It can also be specified in the config file.
                 By default, a file named 'DumpReport.html' is created in the execution folder.
    /SHOWREPORT: (optional) If the value is 1, the report automatically opens in the default browser.
    /QUIET:      (optional) If 1, the console window does not show progress messages.

Example:

    DumpReport /DUMPFILE "C:\dump\crash.dmp" /PDBFOLDER "C:\dump" /SHOWREPORT 1

If the dump file is the only parameter, the call can be simplified:

	DumpReport "C:\dump\crash.dmp"

It is also possible to drag and drop the dump directly onto the executable.

Any value containing spaces must be enclosed in double quotes.  

Providing the PDB files is not necessary but improves the information of the stack traces.

The location of the debbuggers to use and other options must be defined in the XML configuration file, explained in the next section.

Errors are displayed in the console window and also added to the report (provided that the report file could be created).

Please note that the debugger may take several minutes to process the dump, especially if it has to download PDBs.


## Configuration file

Settings are stored in a configuration file named _DumpReportCfg.xml_, which must be located together with the executable.  
This file can be created by typing:

	DumpReport /CONFIG CREATE

It must be edited to specify the location of the debuggers. The rest of the parameters can be left as default.  
Some parameters like the PDB folder or the output file name can be overriden by command line.  

### Nodes

The nodes in the configuration file are:

* __Config:__ Main node.
* __Debugger:__  Supported debuggers are WinDbg.exe and CDB.exe.
	* __exe64:__   Full path of the 64-bit version debugger.
	* __exe32:__   Full path of the 32-bit version debugger.
	* __timeout:__ Maximum number of minutes to wait for the debugger to finish.
	* __visible:__ Runs the debugger in visible (value 1) or hidden (value 0) mode.	This feature is only supported by the CDB debugger.
* __Pdb:__
	* __folder:__  Folder containing the PDB files. If not specified, PDB files are expected to be in the same location as the dump file.
* __Style:__
	* __file:__    Full path of a custom CSS file to use.             
* __Report:__
	* __file:__    Full path of the report file to be created.
	* __show:__    If set to 1, the report will be displayed automatically in the default browser.
* __Log:__
	* __folder:__  Folder where the debug log files will be created. If not specified, log files are created in the same location as the dump file. The name of the log files is the name of the dump file appended with '.log'
	* __clean:__   Indicates whether the log files should be deleted after being processed.
* __SymbolCache:__
	* __folder:__  Folder to use as symbol cache. If not specified, the debugger will use its default symbol cache (e.g: C:\ProgramData\dbg) 
* __SourceCodeRoot:__
	* __folder:__  The report will emphasize the frames whose source file contains this root folder.  

At least one of the attributes _exe64_ or _exe32_ must not be empty. If it is expected to process dumps of both 32 and 64 bits, it is recommended to set both.  

This information can be displayed in the console by typing:

	DumpReport /CONFIG HELP


## About dump bitness

Dumps of 32-bit processes running in 64-bit computers should be captured using tools able to generate 32-bit dumps.  
For example: ProcDump, ADPlus, DebugDiag,.. or the 32-bit Task Manager located in _C:\Windows\SysWOW64_.  
Otherwise, the resulting report may be incomplete or inaccurate.

If both 32-bit and 64-bit debuggers are set, the application chooses the one to use according to the dump bitness.  
If only the 64-bit debugger is set, 32-bit dumps can still be processed by automatically switching the debugger's processor mode to 32bit.  


## Exception detection

The application reads the exception record stored in the dump. Depending on the tool used to create it, the exception record may not be useful. For example, Task Manager usually stores a 'Break Instruction' exception.
In this case, the application tries to retrieve a more meaningful exception record by launching the debugger again with a new script. This script is based on specific data from the stack traces which may not always be available.  

The faulting thread is obtained from the exception address (if found) or by looking for specific keywords in the stack traces, like '_KiUserExceptionDispatcher_' or '_RtlDispatchException_'.  


## Code Structure

This application has been developed in C# (Visual Studio 2017).  
The main modules are:  

* __Config.__ Retrieves and validates the parameters from the configuration file and the command-line.
* __Program.__ It's the entry point of the application. It launches the debugger, which creates a log file divided into sections that will be processed by the next modules.
* __Parser.__ A _Parser_ object parses a section of the debugger's log file and stores the extracted data. The base class implements a method to collect the lines to parse, and declares an abstract method to parse them. Each derived class knows how to parse a specific section in the log. For example, the _ModuleParser_ is in charge of parsing the section where the loaded modules are listed.
* __Report.__ Encapsulates methods to write HTML code from the information stored in the _Parser_ objects.
* __LogManager.__ Reads the debugger's output file and distributes the lines of each section among the corresponding _Parser_ objects. Once all lines are assigned, it makes the _Parser_ objects do their job. Finally, it passes the extracted information to the _Report_ object in order to fill in the report.
* __Resources.__ Contains resources such as the help text to show in the console window, javascript and CSS code used by the report, as well as the debugger scripts.


## Main Debugger Script

This section shows the main debugger script, used to retrieve information about the target machine environment, threads and loaded modules.
It can be found in the file _Resources.cs_, among other auxiliary scripts used to obtain the dump bitness or the exception record.

	.logopen /u "[LOG_FILE]"
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


Notes:  

* [LOG_FILE] is replaced with the debugger's log file name.  
* The effective machine is changed to x86 if the module _wow64.dll_ is found. This has only effect when debugging a 32-bit process with a x64 debugger.  
* '_echo_' commands are used to simplify the parsing of the log.  

## How to extend the report

You can show additional information in the report by adding commands to the main script. These are the main steps:

* In file _Resources.cs_, insert a new section in the main debugger script using the _echo_ command. Section names must begin with ">>> ". For example:  
	`.echo >>> MY SECTION`	
* Next, add the debugger commands that produce the information you need.
* In file _Parser.cs_, create a new _Parser_ class that inherits from _Parser_ (e.g: `class MyParser : Parser`).
* Add to the new class the necessary member variables that will hold the extracted data.
* Implement the `Parse()` method by iterating through the lines of the new section in the log file and using regular expressions to extract the data and store it in the member variables. You can take other `Parse()` methods as an example.
* In the _LogManager_ class, declare and instantiate a member variable of the new class and declare a `const string` with the name of the new section. For example:  	

	`const string MY_SECTION = ">>> MY SECTION";`
	`MyParser myParser = new MyParser();`  		
	
* In the method `LogManager.MapParsers()`, update the map that associates _Parser_ objects with section names (`m_parsers`). For example:  	

	`m_parsers.Add(MY_SECTION, myParser);`  	

* Add a method to the _Report_ class (e.g: `Report.WriteMySection(...)`) with the data to show as input parameter. There you must format the input data as HTML and write it to the report using the _stream_ member variable.
* Finally, in the `LogManager.WriteReport()` method, call `report.WriteSectionTitle()` with the title of the new section and `report.WriteMySection()` passing the data to display.

## Custom Report Style

The style of the report is defined by a default CSS code embedded into the HTML file.  
However, it is possible to embed a custom CSS file instead.  
This file must be specified in the _file_ attribute of the node `<Style>` in the configuration file.  

The styles that can be configured are:

* __body:__         Default style for the HTML document
* __h1:__           Title header
* __h2:__           Section header
* __a:__            Hyperlink
* __toggleButton:__ Style of the Expand/Collapse button (+/-)
* __toggleHeader:__ Auxiliary table that contains a toggle button and a label that describes the area that can be expanded or collapsed.
* __report_table:__ Style for tables showing thread stacks, loaded modules or environment variables. By default, a striped style is used. 
* __sourcecode_frame:__ Stack frame associated to a source code root.

This information can be displayed in the console by typing:

	DumpReport /STYLE HELP

Run the following command to create a sample CSS file (_style.css_):

	DumpReport /STYLE CREATE


## Download

The executable can be downloaded from [here](/Releases/DumpReport.zip).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details
