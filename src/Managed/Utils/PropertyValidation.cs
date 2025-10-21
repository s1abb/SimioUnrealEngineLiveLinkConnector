using System;
using System.IO;
using SimioAPI;

namespace SimioUnrealEngineLiveLinkConnector.Utils
{
    /// <summary>
    /// Property validation utilities for LiveLink configuration
    /// Simplified for Message Bus architecture (no manual network configuration needed)
    /// </summary>
    public static class PropertyValidation
    {
        /// <summary>
        /// Normalizes and validates file paths, creating directories if needed
        /// </summary>
        /// <param name="rawPath">Raw path from property</param>
        /// <param name="context">Execution context for error reporting</param>
        /// <returns>Normalized absolute path or empty string on error</returns>
        public static string NormalizeFilePath(string rawPath, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return string.Empty;
            }

            try
            {
                // Convert relative paths to absolute paths
                string fullPath = Path.GetFullPath(rawPath);
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                return fullPath;
            }
            catch (Exception ex)
            {
                context.ExecutionInformation.ReportError($"Invalid file path '{rawPath}': {ex.Message}");
                return string.Empty;
            }
        }
    }
}