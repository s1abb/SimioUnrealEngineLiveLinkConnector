using System.Runtime.InteropServices;

namespace SimioUnrealEngineLiveLinkConnector.UnrealIntegration
{
    /// <summary>
    /// Transform structure for LiveLink objects matching native C++ layout.
    /// All values are doubles for simplified C# marshaling.
    /// Must maintain exact byte-for-byte compatibility with ULL_Transform in native code.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ULL_Transform
    {
        /// <summary>
        /// Position in centimeters: [X, Y, Z]
        /// Unreal coordinate system: X-forward, Y-right, Z-up
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] position;

        /// <summary>
        /// Rotation as quaternion: [X, Y, Z, W]
        /// Normalized quaternion representing object orientation
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public double[] rotation;

        /// <summary>
        /// Scale factors: [X, Y, Z]
        /// Uniform scaling typically uses [1, 1, 1]
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public double[] scale;

        /// <summary>
        /// Creates a new ULL_Transform with identity values
        /// </summary>
        /// <returns>Transform at origin with no rotation and unit scale</returns>
        public static ULL_Transform Identity()
        {
            return new ULL_Transform
            {
                position = new double[] { 0.0, 0.0, 0.0 },
                rotation = new double[] { 0.0, 0.0, 0.0, 1.0 }, // Identity quaternion
                scale = new double[] { 1.0, 1.0, 1.0 }
            };
        }

        /// <summary>
        /// Creates a new ULL_Transform with specified values
        /// </summary>
        /// <param name="posX">X position in centimeters</param>
        /// <param name="posY">Y position in centimeters</param>
        /// <param name="posZ">Z position in centimeters</param>
        /// <param name="rotX">Quaternion X component</param>
        /// <param name="rotY">Quaternion Y component</param>
        /// <param name="rotZ">Quaternion Z component</param>
        /// <param name="rotW">Quaternion W component</param>
        /// <param name="scaleX">X scale factor</param>
        /// <param name="scaleY">Y scale factor</param>
        /// <param name="scaleZ">Z scale factor</param>
        /// <returns>New transform with specified values</returns>
        public static ULL_Transform Create(
            double posX, double posY, double posZ,
            double rotX, double rotY, double rotZ, double rotW,
            double scaleX = 1.0, double scaleY = 1.0, double scaleZ = 1.0)
        {
            return new ULL_Transform
            {
                position = new double[] { posX, posY, posZ },
                rotation = new double[] { rotX, rotY, rotZ, rotW },
                scale = new double[] { scaleX, scaleY, scaleZ }
            };
        }

        /// <summary>
        /// Validates that arrays are properly initialized and have correct dimensions
        /// </summary>
        /// <returns>True if transform is valid for marshaling</returns>
        public bool IsValid()
        {
            return position != null && position.Length == 3 &&
                   rotation != null && rotation.Length == 4 &&
                   scale != null && scale.Length == 3;
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        /// <returns>Human-readable transform description</returns>
        public override string ToString()
        {
            if (!IsValid())
                return "ULL_Transform(INVALID)";

            return $"ULL_Transform(pos:[{position[0]:F2},{position[1]:F2},{position[2]:F2}], " +
                   $"rot:[{rotation[0]:F3},{rotation[1]:F3},{rotation[2]:F3},{rotation[3]:F3}], " +
                   $"scale:[{scale[0]:F2},{scale[1]:F2},{scale[2]:F2}])";
        }
    }

    /// <summary>
    /// Configuration object for LiveLink connection initialization
    /// Contains all essential properties for Element configuration
    /// </summary>
    public class LiveLinkConfiguration
    {
        /// <summary>
        /// Name displayed in Unreal Engine's LiveLink window
        /// </summary>
        public string SourceName { get; set; } = "SimioSimulation";

        /// <summary>
        /// Enable logging for debugging and diagnostics
        /// </summary>
        public bool EnableLogging { get; set; } = false;

        /// <summary>
        /// Path to log file for LiveLink operations
        /// </summary>
        public string LogFilePath { get; set; } = "SimioUnrealLiveLink.log";

        /// <summary>
        /// Path to Unreal Engine installation directory
        /// </summary>
        public string UnrealEnginePath { get; set; } = string.Empty;

        /// <summary>
        /// Host name or IP address for LiveLink server
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Port number for LiveLink server
        /// </summary>
        public int Port { get; set; } = 11111;

        /// <summary>
        /// Connection timeout duration
        /// </summary>
        public System.TimeSpan ConnectionTimeout { get; set; } = System.TimeSpan.FromSeconds(5.0);

        /// <summary>
        /// Number of retry attempts for failed connections
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Validates the configuration and returns any error messages
        /// </summary>
        /// <returns>Array of validation error messages, empty if valid</returns>
        public string[] Validate()
        {
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(SourceName))
            {
                errors.Add("Source Name must not be empty");
            }

            if (string.IsNullOrWhiteSpace(Host))
            {
                errors.Add("LiveLink Host must not be empty");
            }

            if (Port < 1 || Port > 65535)
            {
                errors.Add($"LiveLink Port must be between 1 and 65535, got: {Port}");
            }

            if (ConnectionTimeout.TotalSeconds <= 0)
            {
                errors.Add("Connection Timeout must be positive");
            }

            if (ConnectionTimeout.TotalSeconds > 300)
            {
                errors.Add("Connection Timeout cannot exceed 300 seconds");
            }

            if (RetryAttempts < 0)
            {
                errors.Add("Retry Attempts cannot be negative");
            }

            if (RetryAttempts > 10)
            {
                errors.Add("Retry Attempts cannot exceed 10");
            }

            return errors.ToArray();
        }

        /// <summary>
        /// Creates a configuration with validated values
        /// </summary>
        /// <returns>New configuration with sanitized values</returns>
        public LiveLinkConfiguration CreateValidated()
        {
            return new LiveLinkConfiguration
            {
                SourceName = string.IsNullOrWhiteSpace(SourceName) ? "SimioSimulation" : SourceName.Trim(),
                EnableLogging = EnableLogging,
                LogFilePath = string.IsNullOrWhiteSpace(LogFilePath) ? "SimioUnrealLiveLink.log" : LogFilePath.Trim(),
                UnrealEnginePath = string.IsNullOrWhiteSpace(UnrealEnginePath) ? @"C:\Program Files\Epic Games\UE_5.3" : UnrealEnginePath.Trim(),
                Host = string.IsNullOrWhiteSpace(Host) ? "localhost" : Host.Trim(),
                Port = Port < 1 || Port > 65535 ? 11111 : Port,
                ConnectionTimeout = ConnectionTimeout.TotalSeconds <= 0 || ConnectionTimeout.TotalSeconds > 300 
                    ? System.TimeSpan.FromSeconds(5.0) 
                    : ConnectionTimeout,
                RetryAttempts = RetryAttempts < 0 || RetryAttempts > 10 ? 3 : RetryAttempts
            };
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        /// <returns>Human-readable configuration description</returns>
        public override string ToString()
        {
            return $"LiveLinkConfiguration(Source:'{SourceName}', Host:'{Host}:{Port}', " +
                   $"Timeout:{ConnectionTimeout.TotalSeconds:F1}s, Retries:{RetryAttempts}, " +
                   $"Logging:{EnableLogging})";
        }
    }
}
