```c#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomSimioStep
{
    public static class FileUtils
    {
        public static string SanitizeFileName(string fileName)
        {
            if (_illegalCharacters == null)
            {
                _illegalCharacters = System.IO.Path.GetInvalidFileNameChars();
                Array.Sort<char>(_illegalCharacters);
            }

            var sb = new StringBuilder(fileName.Length);

            foreach (var c in fileName)
            {
                if (Array.BinarySearch<char>(_illegalCharacters, c) < 0)
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            var validCharsFileName = sb.ToString();

            var parts = validCharsFileName.Split('.');

            return String.Join(".", parts.Select(s =>
            {
                if (IllegalFileNames.Contains(s, StringComparer.OrdinalIgnoreCase))
                    return "_" + s;

                return s;
            }));
        }

        static char[] _illegalCharacters = null;
        public static string[] IllegalFileNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
    }
}
```