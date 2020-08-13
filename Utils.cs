using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DumpReport
{
    class Utils
    {
        // Converts a value from hexadecimal string format to unsigned 64-bit integer
        public static UInt64 StrHexToUInt64(string hexString)
        {
            UInt64 result = 0;
            try
            {
                result = UInt64.Parse(hexString.Replace("0x", String.Empty), System.Globalization.NumberStyles.HexNumber);
            }
            catch (Exception)
            {
                throw new Exception("Cannot convert address " + hexString);
            }
            return result;
        }

        // Converts a value from unsigned 64-bit integer to hexadecimal string format
        public static string UInt64toStringHex(UInt64 value, bool prefix = true)
        {
            string valueHex = (Program.is32bitDump) ? value.ToString("X8") : value.ToString("X16");
            return prefix ? "0x" + valueHex : valueHex;
        }

        // Returns true if the input strings represent the same hexadecimal address
        public static bool SameStrAddress(string addr1, string addr2)
        {
            // Normalize strings
            string addr1Norm = addr1.Replace("0x", String.Empty).ToUpper().TrimStart('0');
            string addr2Norm = addr2.Replace("0x", String.Empty).ToUpper().TrimStart('0');
            return addr1Norm == addr2Norm;
        }

        // Returns true if the address represented by the input string is not zero
        public static bool NotZeroAddress(string addr)
        {
            return (addr.Replace("0x", String.Empty).TrimStart('0').Length > 0);
        }
        
        // Returns a string with the UTC time, based on the input local time and the current UTC offset
        public static string GetUtcTimeFromLocalTime(DateTime localTime)
        {
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(localTime);
            return String.Format("{0} (UTC {1} {2})", localTime.ToString(), ((utcOffset < TimeSpan.Zero) ? "-" : "+"), utcOffset.ToString("hh"));
        }        

        // Converts a timestamp string from hexadecimal format (POSIX) to UTC time
        public static string GetUtcTimeFromTimestamp(string timestamp)
        {
            try
            {
                if (timestamp == null) return String.Empty;
                int secondsAfterEpoch = Int32.Parse(timestamp, System.Globalization.NumberStyles.HexNumber);
                DateTime epoch = new DateTime(1970, 1, 1); // Unix Epoch
                return epoch.AddSeconds(secondsAfterEpoch).ToString();
            }
            catch (Exception)
            {
                return String.Empty;
            }
        }

        // Returns a string with the dump's UTC creation time. 
        public static string GetDumpUtcTime(string debuggerDumpTime)
        {
            string time = String.Empty;            
            string pattern = @"\w+\s+(?<month>\w+)\s+(?<day>[0-9]+)\s+(?<hour>[0-9]+):(?<min>[0-9]+):(?<sec>[0-9]+).+(?<year>[0-9]{4})";
            MatchCollection matches = Regex.Matches(debuggerDumpTime, pattern);
            if (matches.Count == 1)
            {
                int month = DateTime.ParseExact(matches[0].Groups["month"].Value, "MMM", new CultureInfo("en-US", false)).Month;
                int day = Int32.Parse(matches[0].Groups["day"].Value);
                int hour = Int32.Parse(matches[0].Groups["hour"].Value);
                int min = Int32.Parse(matches[0].Groups["min"].Value);
                int sec = Int32.Parse(matches[0].Groups["sec"].Value);
                int year = Int32.Parse(matches[0].Groups["year"].Value);                
                time = new DateTime(year, month, day, hour, min, sec).ToUniversalTime().ToString();
                time += " (UTC)";
            }
            return time;
        }

        // If the input path is not rooted (e.g: a relative path), returns the path rooted
        // with the application directory.
        public static string GetAbsolutePath(string path)
        {
            if (path == null) return null;
            if (path == String.Empty) return String.Empty;
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Program.appDirectory, path);
                path = path.Replace("\\.\\", "\\");
            }
            return path;
        }
    }
}
