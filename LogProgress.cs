using System;
using System.IO;
using System.Text.RegularExpressions;

namespace DumpReport
{
    /// <summary>
    /// Shows the progress of the debugger's log generation in the console.
    /// </summary>
    // The progress of the creation of the debugger's log file is measured by reading the number of bytes of an auxiliar 
    // file. For example, if the size is 5 and the total number of steps is 10, we know that the progress is 50%.
    // This auxiliary file is created by using the command 'writemem', and its size is increased by one byte in every
    // progress step.
    class LogProgress
    {
        const string progressMark = "{PROGRESS_STEP}";
        string progressCommand = @".writemem ""{file}"" {reg} L?{step}";
        double maxSteps;
        string progressFile; // Auxiliary file used to measure the progress of the creation of the log

        // Inserts the command to measure the progress of the creation of the log
        public string PrepareScript(string script, string outFile, bool is32bitDump)
        {
            // Create a temporary progress file
            progressFile = outFile + ".prg";
            File.Delete(progressFile); // Delete any older file
            progressFile = progressFile.Replace("\\", "\\\\");

            // Format the command used to measure the progress
            progressCommand = progressCommand.Replace("{reg}", is32bitDump ? "@esp" : "@rsp");
            progressCommand = progressCommand.Replace("{file}", progressFile);

            // Get the number of progress steps in the script
            maxSteps = Regex.Matches(script, progressMark).Count;
            if (maxSteps == 0)
                return script;

            // Separate the script into progress steps
            string[] steps = script.Split(new string[] { progressMark }, StringSplitOptions.None);
            
            // Insert the command used to measure the progress into the main script
            int numStep = 1;
            string scriptWithProgress = "";
            foreach (string step in steps)
            {
                scriptWithProgress += step;
                if (numStep <= maxSteps)
                    scriptWithProgress += progressCommand.Replace("{step}", numStep.ToString());
                ++numStep;
            }
            return scriptWithProgress;
        }

        // Shows the progress of the creation of the log file by reading the size of the auxiliary file
        public void ShowLogProgress()
        {
            double progress = 0;
            if (File.Exists(progressFile))
            {
                // The number of bytes in the file indicates the current progress step 
                FileInfo fileInfo = new FileInfo(progressFile);
                progress = 100.0 * (double)fileInfo.Length / maxSteps;
            }
            Console.Write("\rCreating log... {0}%", (int)progress);
        }

        public void DeleteProgressFile()
        {
            if (progressFile.Length > 0)
                File.Delete(progressFile);
        }
    }
}
