using System;
using System.IO;

namespace DumpReport
{
    class Utils
    {
        // Converts a value from hexadecimal string format to unsigned 64-bit integer
        public static UInt64 StringHexToUInt64(string hexString)
        {
            return UInt64.Parse(hexString.Replace("0x", String.Empty), System.Globalization.NumberStyles.HexNumber);
        }

        // Converts a value from unsigned 64-bit integer to hexadecimal string format
        public static string UInt64toStringHex(UInt64 value)
        {
            string valueHex = (Program.is32bitDump) ? value.ToString("X8") : value.ToString("X16");
            return "0x" + valueHex;
        }

        // Converts a timestamp string from hexadecimal format (POSIX) to format "dd/MM/yyyy HH:mm:ss"
        public static string TimestampToLocalDateTime(string timestamp)
        {
            try
            {
                if (timestamp == null) return String.Empty;
                int secondsAfterEpoch = Int32.Parse(timestamp, System.Globalization.NumberStyles.HexNumber);
                DateTime epoch = new DateTime(1970, 1, 1); // Unix Epoch
                return epoch.AddSeconds(secondsAfterEpoch).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch (Exception)
            {
                return String.Empty;
            }
        }

        // If the input path is not rooted (e.g: a relative path), returns the path rooted
        // with the application directory.
        public static string GetAbsolutePath(string path)
        {
            if (path == null) return null;
            if (path == String.Empty) return String.Empty;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Program.appDirectory, path);
            return path;
        }
    }
}
