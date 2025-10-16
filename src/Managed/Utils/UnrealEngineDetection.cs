using System;
using System.IO;

namespace SimioUnrealEngineLiveLinkConnector.Utils
{
    /// <summary>
    /// Validation utilities for Unreal Engine installations
    /// Verifies that provided UE paths contain required executables and DLLs
    /// </summary>
    public static class UnrealEngineDetection
    {
        private static readonly string[] EngineExecutables = new[]
        {
            @"Engine\Binaries\Win64\UnrealEditor.exe",  // UE5
            @"Engine\Binaries\Win64\UE4Editor.exe"     // UE4
        };

        /// <summary>
        /// Validates that required Unreal Engine DLLs and executables are present
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <returns>Detailed validation result</returns>
        public static UnrealEngineValidationResult ValidateInstallation(string path)
        {
            var result = new UnrealEngineValidationResult { Path = path };

            if (string.IsNullOrWhiteSpace(path))
            {
                result.IsValid = false;
                result.ErrorMessage = "Path cannot be null or empty";
                return result;
            }

            if (!Directory.Exists(path))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Directory does not exist: {path}";
                return result;
            }

            // Check for required executables
            bool hasUE4Editor = File.Exists(Path.Combine(path, EngineExecutables[1])); // UE4Editor.exe
            bool hasUE5Editor = File.Exists(Path.Combine(path, EngineExecutables[0])); // UnrealEditor.exe

            if (!hasUE4Editor && !hasUE5Editor)
            {
                result.IsValid = false;
                result.ErrorMessage = $"No Unreal Engine executable found. Expected UE4Editor.exe or UnrealEditor.exe in Engine/Binaries/Win64/";
                return result;
            }

            // Check for additional required DLLs and files that LiveLink might need
            string engineBinariesPath = Path.Combine(path, "Engine", "Binaries", "Win64");
            
            // Check for core engine DLLs
            string[] requiredDLLs = {
                "UnrealEditor-Core.dll",
                "UnrealEditor-ApplicationCore.dll"
            };

            foreach (string dll in requiredDLLs)
            {
                string dllPath = Path.Combine(engineBinariesPath, dll);
                if (!File.Exists(dllPath))
                {
                    // For UE4, DLL names are different - check alternative names
                    string ue4DllName = dll.Replace("UnrealEditor-", "UE4Editor-");
                    string ue4DllPath = Path.Combine(engineBinariesPath, ue4DllName);
                    
                    if (!File.Exists(ue4DllPath))
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"Required Unreal Engine DLL not found: {dll} or {ue4DllName} in {engineBinariesPath}";
                        return result;
                    }
                }
            }

            // Determine version and set details
            result.IsValid = true;
            result.HasUE4Editor = hasUE4Editor;
            result.HasUE5Editor = hasUE5Editor;
            result.Version = GetEngineVersion(path);
            result.ExecutablePath = hasUE5Editor 
                ? Path.Combine(path, EngineExecutables[0])
                : Path.Combine(path, EngineExecutables[1]);

            return result;
        }

        /// <summary>
        /// Validates if a path contains a valid Unreal Engine installation
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if valid UE installation</returns>
        public static bool IsValidUnrealEngineInstallation(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            foreach (string executable in EngineExecutables)
            {
                string fullPath = Path.Combine(path, executable);
                if (File.Exists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the version of Unreal Engine from installation path
        /// </summary>
        /// <param name="installPath">Installation path</param>
        /// <returns>Version string or "Unknown"</returns>
        public static string GetEngineVersion(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath))
            {
                return "Unknown";
            }

            try
            {
                // Try to extract version from path (e.g., "UE_5.3", "UE_4.27")
                string folderName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar));
                if (folderName.StartsWith("UE_", StringComparison.OrdinalIgnoreCase))
                {
                    return folderName.Substring(3);
                }
                
                // Check for UE5 vs UE4 based on executables present
                bool hasUE5Editor = File.Exists(Path.Combine(installPath, EngineExecutables[0]));
                bool hasUE4Editor = File.Exists(Path.Combine(installPath, EngineExecutables[1]));
                
                if (hasUE5Editor && !hasUE4Editor)
                {
                    return "5.x";
                }
                else if (hasUE4Editor && !hasUE5Editor)
                {
                    return "4.x";
                }
                else if (hasUE5Editor && hasUE4Editor)
                {
                    return "5.x (with UE4 compatibility)";
                }

                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Validation result for Unreal Engine installation
        /// </summary>
        public class UnrealEngineValidationResult
        {
            public string Path { get; set; } = string.Empty;
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
            public string Version { get; set; } = "Unknown";
            public string ExecutablePath { get; set; } = string.Empty;
            public bool HasUE4Editor { get; set; }
            public bool HasUE5Editor { get; set; }
            
            public override string ToString()
            {
                if (IsValid)
                {
                    return $"Valid UE installation: {Version} at {Path}";
                }
                else
                {
                    return $"Invalid UE installation: {ErrorMessage}";
                }
            }
        }
    }
}