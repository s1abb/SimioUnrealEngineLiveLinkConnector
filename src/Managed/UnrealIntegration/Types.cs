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
    /// Uses Message Bus (UDP multicast) for automatic discovery - no manual network configuration needed
    /// Logging output can be viewed using DebugView++ (https://github.com/CobaltFusion/DebugViewPP)
    /// </summary>
    public class LiveLinkConfiguration
    {
        /// <summary>
        /// Name displayed in Unreal Engine's LiveLink window
        /// </summary>
        public string SourceName { get; set; } = "SimioSimulation";

        /// <summary>
        /// Enable native logging for debugging and diagnostics (view with DebugView++)
        /// </summary>
        public bool EnableLogging { get; set; } = false;

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
                EnableLogging = EnableLogging
            };
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        /// <returns>Human-readable configuration description</returns>
        public override string ToString()
        {
            return $"LiveLinkConfiguration(Source:'{SourceName}', Logging:{EnableLogging})";
        }
    }
}
