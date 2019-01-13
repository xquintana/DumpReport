using System;
using System.IO;

namespace DumpReport
{
    class Utils
    {
        // Converts a value from hexadecimal string format to unsigned 64-bit integer
        public static UInt64 StrHexToUInt64(string hexString)
        {
            UInt64 result;
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
