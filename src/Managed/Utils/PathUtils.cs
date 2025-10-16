using System;
using System.IO;

namespace SimioUnrealEngineLiveLinkConnector.Utils
{
    /// <summary>
    /// Path handling utilities for the LiveLink connector
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// Combines paths safely, handling null/empty inputs
        /// </summary>
        /// <param name="path1">First path component</param>
        /// <param name="path2">Second path component</param>
        /// <returns>Combined path or empty string if inputs invalid</returns>
        public static string SafeCombine(string path1, string path2)
        {
            if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
            {
                return string.Empty;
            }

            try
            {
                return Path.Combine(path1, path2);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets directory name safely, handling edge cases
        /// </summary>
        /// <param name="filePath">File path</param>
        /// <returns>Directory name or empty string</returns>
        public static string SafeGetDirectoryName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetDirectoryName(filePath) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Ensures directory exists, creating if necessary
        /// </summary>
        /// <param name="directoryPath">Directory to create</param>
        /// <returns>True if directory exists or was created successfully</returns>
        public static bool EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Makes a path relative to a base directory
        /// </summary>
        /// <param name="fullPath">Full path to make relative</param>
        /// <param name="basePath">Base directory</param>
        /// <returns>Relative path or original path if conversion fails</returns>
        public static string MakeRelativePath(string fullPath, string basePath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(basePath))
            {
                return fullPath;
            }

            try
            {
                var fullUri = new Uri(Path.GetFullPath(fullPath));
                var baseUri = new Uri(Path.GetFullPath(basePath) + Path.DirectorySeparatorChar);
                return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
            }
            catch
            {
                return fullPath;
            }
        }

        /// <summary>
        /// Checks if a file path has a valid extension
        /// </summary>
        /// <param name="filePath">File path to check</param>
        /// <param name="validExtensions">Array of valid extensions (with dots)</param>
        /// <returns>True if extension is valid</returns>
        public static bool HasValidExtension(string filePath, params string[] validExtensions)
        {
            if (string.IsNullOrWhiteSpace(filePath) || validExtensions == null || validExtensions.Length == 0)
            {
                return false;
            }

            try
            {
                string extension = Path.GetExtension(filePath);
                foreach (string validExt in validExtensions)
                {
                    if (string.Equals(extension, validExt, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sanitizes a filename by removing invalid characters
        /// </summary>
        /// <param name="filename">Filename to sanitize</param>
        /// <param name="replacement">Character to replace invalid chars with</param>
        /// <returns>Sanitized filename</returns>
        public static string SanitizeFilename(string filename, char replacement = '_')
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = filename;

            foreach (char invalidChar in invalidChars)
            {
                sanitized = sanitized.Replace(invalidChar, replacement);
            }

            return sanitized;
        }
    }
}