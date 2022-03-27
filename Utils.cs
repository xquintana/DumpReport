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

        // Returns a string with the UTC time, based on the input local time and the current UTC offset.
        public static string GetUtcTimeFromLocalTime(DateTime localTime)
        {
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(localTime);
            string sign = ((utcOffset < TimeSpan.Zero) ? "-" : "+");
            string offset = utcOffset.ToString("hh");
            if (utcOffset.Minutes > 0)
                offset += ":" + utcOffset.ToString("mm");
            return String.Format($"{localTime.ToString()} (UTC {sign} {offset})");
        }

        // Converts a timestamp string containing a 32-bit hexadecimal value to a human-readable time.
        // The timestamp represents the number of seconds since January 1, 1970 UTC, so the output time is also UTC.
        // The output is formatted according to the current culture.
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

        // Returns a string with the dump's creation time formatted with the current culture.
        // If the input time contains a UTC offset, the output is normalized to UTC + 0 and '(UTC)' is appended.
        // The input is expected to be in the "en-US" culture. Otherwise, it is returned without conversion.        
        public static string GetNormalizedDumpTime(string debuggerDumpTime)
        {
            string timeStr = String.Empty;
            bool withOffset = false;
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
                DateTime time = new DateTime(year, month, day, hour, min, sec);
                // If it contains a UTC offset, normalize to UTC + 0
                pattern = @"\(UTC(?<offset>.*)\)";
                matches = Regex.Matches(debuggerDumpTime, pattern);
                if (matches.Count == 1)
                {
                    withOffset = true;
                    string offset = matches[0].Groups["offset"].Value.Replace(" ", String.Empty);
                    if (offset.Length > 0)
                    {
                        pattern = @"(?<sign>[+,-])(?<hour>[0-9]+):(?<min>[0-9]+)";
                        matches = Regex.Matches(offset, pattern);
                        if (matches.Count == 1)
                        {
                            int offsetHour = Int32.Parse(matches[0].Groups["hour"].Value);
                            int offsetMin = Int32.Parse(matches[0].Groups["min"].Value);
                            int offsetTotalMin = offsetMin + 60 * offsetHour;
                            if (matches[0].Groups["sign"].Value == "+")
                                offsetTotalMin *= -1;
                            time = time.AddMinutes(offsetTotalMin);
                        }
                    }
                }
                timeStr = time.ToString();
                if (withOffset) { timeStr += " (UTC)"; }
            }
            return (timeStr.Length > 0) ? timeStr : debuggerDumpTime;
        }

        // Returns a full path based on the input path.
        // If the input path is null or empty, returns the input.
        public static string GetAbsolutePath(string path)
        {
            if (path != null && path.Length > 0 && !Path.IsPathRooted(path))
            {
                path = Path.Combine(Program.appDirectory, path);
                path = path.Replace("\\.\\", "\\");
            }
            return path;
        }
    }
}
