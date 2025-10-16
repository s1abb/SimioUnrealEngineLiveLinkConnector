using System;
using System.IO;
using SimioAPI;

namespace SimioUnrealEngineLiveLinkConnector.Utils
{
    /// <summary>
    /// Property validation utilities following TextFileReadWrite patterns from Simio API
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

        /// <summary>
        /// Validates Unreal Engine installation path provided by user
        /// </summary>
        /// <param name="path">Path to Unreal Engine installation</param>
        /// <param name="context">Execution context for error reporting</param>
        /// <returns>Normalized path or empty string on error</returns>
        public static string ValidateUnrealEnginePath(string path, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                context.ExecutionInformation.ReportError("Unreal Engine installation path is required. Please specify the full path to your Unreal Engine installation directory (e.g., 'C:\\Program Files\\Epic Games\\UE_5.3').");
                return string.Empty;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                
                if (!Directory.Exists(fullPath))
                {
                    context.ExecutionInformation.ReportError($"Unreal Engine directory does not exist: '{fullPath}'. Please verify the installation path is correct.");
                    return string.Empty;
                }

                // Use UnrealEngineDetection to validate the installation
                if (!UnrealEngineDetection.IsValidUnrealEngineInstallation(fullPath))
                {
                    context.ExecutionInformation.ReportError($"Invalid Unreal Engine installation at '{fullPath}'. Expected UE4Editor.exe or UnrealEditor.exe in Engine/Binaries/Win64/ subdirectory.");
                    return string.Empty;
                }
                
                return fullPath;
            }
            catch (Exception ex)
            {
                context.ExecutionInformation.ReportError($"Invalid Unreal Engine path '{path}': {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates network endpoint for LiveLink connection
        /// </summary>
        /// <param name="host">Host name or IP address</param>
        /// <param name="port">Port number</param>
        /// <param name="context">Execution context for error reporting</param>
        public static void ValidateNetworkEndpoint(string host, int port, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                context.ExecutionInformation.ReportError("LiveLink host must not be empty");
                return;
            }

            // Validate port range
            if (port < 1 || port > 65535)
            {
                context.ExecutionInformation.ReportError($"LiveLink port must be between 1 and 65535, got: {port}");
                return;
            }

            // Check for common configuration issues
            if (host.Contains("://"))
            {
                context.ExecutionInformation.ReportError($"LiveLink host should be hostname/IP only, not a URL. Remove protocol prefix from: '{host}'");
                return;
            }

            // Validate common localhost variations
            if (host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                host.Equals("::1", StringComparison.OrdinalIgnoreCase))
            {
                // Valid localhost addresses - no further validation needed
                return;
            }

            // For remote hosts, basic format validation
            if (host.Contains(" "))
            {
                context.ExecutionInformation.ReportError($"LiveLink host contains invalid spaces: '{host}'");
                return;
            }
        }

        /// <summary>
        /// Validates timeout value
        /// </summary>
        /// <param name="timeoutSeconds">Timeout in seconds</param>
        /// <param name="context">Execution context for error reporting</param>
        /// <returns>Validated timeout or default value</returns>
        public static double ValidateTimeout(double timeoutSeconds, IExecutionContext context)
        {
            if (timeoutSeconds <= 0)
            {
                context.ExecutionInformation.ReportError($"Connection timeout must be positive, got: {timeoutSeconds}. Using default of 5.0 seconds.");
                return 5.0;
            }

            if (timeoutSeconds > 300) // 5 minutes max
            {
                context.ExecutionInformation.ReportError($"Connection timeout too large: {timeoutSeconds}s. Using maximum of 300 seconds.");
                return 300.0;
            }

            return timeoutSeconds;
        }

        /// <summary>
        /// Validates retry attempts
        /// </summary>
        /// <param name="retryAttempts">Number of retry attempts</param>
        /// <param name="context">Execution context for error reporting</param>
        /// <returns>Validated retry attempts or default value</returns>
        public static int ValidateRetryAttempts(int retryAttempts, IExecutionContext context)
        {
            if (retryAttempts < 0)
            {
                context.ExecutionInformation.ReportError($"Retry attempts cannot be negative, got: {retryAttempts}. Using default of 3.");
                return 3;
            }

            if (retryAttempts > 10)
            {
                context.ExecutionInformation.ReportError($"Retry attempts too high: {retryAttempts}. Using maximum of 10.");
                return 10;
            }

            return retryAttempts;
        }
    }
}