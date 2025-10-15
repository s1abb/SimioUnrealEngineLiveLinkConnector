using System;
using System.Runtime.InteropServices;

namespace SimioUnrealEngineLiveLinkConnector.UnrealIntegration
{
    /// <summary>
    /// P/Invoke declarations for UnrealLiveLink.Native.dll
    /// 
    /// This class provides the interface between the C# managed layer and the native C++ LiveLink bridge.
    /// All functions use CallingConvention.Cdecl to match the native exports.
    /// 
    /// Return codes:
    /// - ULL_OK (0): Success
    /// - ULL_ERROR (-1): General error
    /// - ULL_NOT_CONNECTED (-2): Not connected to Unreal
    /// - ULL_NOT_INITIALIZED (-3): Not initialized
    /// </summary>
    public static class UnrealLiveLinkNative
    {
        private const string DLL_NAME = "UnrealLiveLink.Native.dll";

        // Return codes matching native definitions
        public const int ULL_OK = 0;
        public const int ULL_ERROR = -1;
        public const int ULL_NOT_CONNECTED = -2;
        public const int ULL_NOT_INITIALIZED = -3;

        //=============================================================================
        // Lifecycle Management
        //=============================================================================

        /// <summary>
        /// Initialize LiveLink system with provider name.
        /// Must be called before any other functions.
        /// </summary>
        /// <param name="providerName">Name displayed in Unreal's LiveLink window (e.g., "SimioSimulation")</param>
        /// <returns>ULL_OK on success, ULL_ERROR on failure</returns>
        /// <remarks>
        /// Call only once per process. Multiple calls return ULL_OK if already initialized.
        /// NOT thread-safe - call from main thread only.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int ULL_Initialize([MarshalAs(UnmanagedType.LPStr)] string providerName);

        /// <summary>
        /// Shutdown LiveLink system.
        /// Flushes all messages and releases resources.
        /// </summary>
        /// <remarks>
        /// Safe to call multiple times.
        /// Blocks briefly to ensure clean shutdown.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ULL_Shutdown();

        /// <summary>
        /// Get API version number
        /// </summary>
        /// <returns>Version number for compatibility checking</returns>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ULL_GetVersion();

        //=============================================================================
        // Connection Status
        //=============================================================================

        /// <summary>
        /// Check if connected to Unreal Engine
        /// </summary>
        /// <returns>ULL_OK if connected, ULL_NOT_CONNECTED if no connection, ULL_NOT_INITIALIZED if not initialized</returns>
        /// <remarks>
        /// Connection may take 1-2 seconds after initialization.
        /// Unreal must be running with LiveLink window open.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ULL_IsConnected();

        //=============================================================================
        // Transform Subjects (3D Objects)
        //=============================================================================

        /// <summary>
        /// Register a transform subject (3D object).
        /// Call once per object before sending updates.
        /// </summary>
        /// <param name="subjectName">Unique identifier for this object (e.g., "Forklift_01")</param>
        /// <remarks>
        /// Subsequent calls with same name are ignored (no error).
        /// Name is case-sensitive.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_RegisterObject([MarshalAs(UnmanagedType.LPStr)] string subjectName);

        /// <summary>
        /// Register a transform subject with custom properties.
        /// Properties will be available in Unreal Blueprints.
        /// </summary>
        /// <param name="subjectName">Unique identifier for this object</param>
        /// <param name="propertyNames">Array of property names (e.g., ["Speed", "Load", "Battery"])</param>
        /// <param name="propertyCount">Number of properties</param>
        /// <remarks>
        /// Property names are case-sensitive.
        /// All subsequent updates must include same number of property values.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_RegisterObjectWithProperties(
            [MarshalAs(UnmanagedType.LPStr)] string subjectName,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] propertyNames,
            int propertyCount);

        /// <summary>
        /// Update transform for an object.
        /// Registers object automatically if not already registered.
        /// </summary>
        /// <param name="subjectName">Object identifier</param>
        /// <param name="transform">Transform data (position, rotation, scale)</param>
        /// <remarks>
        /// Call at your desired update rate (e.g., 30 Hz).
        /// If object not registered, auto-registers without properties.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_UpdateObject(
            [MarshalAs(UnmanagedType.LPStr)] string subjectName,
            ref ULL_Transform transform);

        /// <summary>
        /// Update transform and property values for an object.
        /// </summary>
        /// <param name="subjectName">Object identifier</param>
        /// <param name="transform">Transform data</param>
        /// <param name="propertyValues">Array of property values (must match registration order)</param>
        /// <param name="propertyCount">Number of property values (must match registration count)</param>
        /// <remarks>
        /// Property count must match registration count, or update is ignored.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_UpdateObjectWithProperties(
            [MarshalAs(UnmanagedType.LPStr)] string subjectName,
            ref ULL_Transform transform,
            [MarshalAs(UnmanagedType.LPArray)] float[] propertyValues,
            int propertyCount);

        /// <summary>
        /// Remove an object from LiveLink.
        /// Object becomes "stale" in Unreal after ~5 seconds.
        /// </summary>
        /// <param name="subjectName">Object identifier to remove</param>
        /// <remarks>
        /// Safe to call on non-existent objects (no error).
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_RemoveObject([MarshalAs(UnmanagedType.LPStr)] string subjectName);

        //=============================================================================
        // Data Subjects (Metrics/KPIs) - NEW!
        //=============================================================================

        /// <summary>
        /// Register a data-only subject (no 3D representation).
        /// Used for streaming simulation metrics, KPIs, system state.
        /// </summary>
        /// <param name="subjectName">Unique identifier (e.g., "SystemMetrics", "ProductionKPIs")</param>
        /// <param name="propertyNames">Array of property names (e.g., ["Throughput", "Utilization", "WIP"])</param>
        /// <param name="propertyCount">Number of properties</param>
        /// <remarks>
        /// Subject name is arbitrary - use descriptive names for clarity.
        /// Properties are read in Unreal via "Get LiveLink Property Value" Blueprint node.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_RegisterDataSubject(
            [MarshalAs(UnmanagedType.LPStr)] string subjectName,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] propertyNames,
            int propertyCount);

        /// <summary>
        /// Update property values for a data subject.
        /// Registers automatically if not already registered.
        /// </summary>
        /// <param name="subjectName">Subject identifier</param>
        /// <param name="propertyNames">Array of property names (if auto-registering, can be null if already registered)</param>
        /// <param name="propertyValues">Array of property values</param>
        /// <param name="propertyCount">Number of properties</param>
        /// <remarks>
        /// If subject not registered, auto-registers with provided property names.
        /// If already registered, propertyNames can be NULL (ignored).
        /// Property count must match registration count if already registered.
        /// </remarks>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_UpdateDataSubject(
            [MarshalAs(UnmanagedType.LPStr)] string subjectName,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[]? propertyNames,
            [MarshalAs(UnmanagedType.LPArray)] float[] propertyValues,
            int propertyCount);

        /// <summary>
        /// Remove a data subject from LiveLink.
        /// </summary>
        /// <param name="subjectName">Subject identifier to remove</param>
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern void ULL_RemoveDataSubject([MarshalAs(UnmanagedType.LPStr)] string subjectName);

        //=============================================================================
        // Helper Methods for Error Handling
        //=============================================================================

        /// <summary>
        /// Checks if a return code indicates success
        /// </summary>
        /// <param name="returnCode">Return code from native function</param>
        /// <returns>True if operation succeeded</returns>
        public static bool IsSuccess(int returnCode)
        {
            return returnCode == ULL_OK;
        }

        /// <summary>
        /// Gets a human-readable description of a return code
        /// </summary>
        /// <param name="returnCode">Return code from native function</param>
        /// <returns>Description of the error or success</returns>
        public static string GetReturnCodeDescription(int returnCode)
        {
            switch (returnCode)
            {
                case ULL_OK:
                    return "Success";
                case ULL_ERROR:
                    return "General error";
                case ULL_NOT_CONNECTED:
                    return "Not connected to Unreal Engine";
                case ULL_NOT_INITIALIZED:
                    return "LiveLink not initialized";
                default:
                    return $"Unknown return code: {returnCode}";
            }
        }

        /// <summary>
        /// Validates that an array has the expected size for marshaling
        /// </summary>
        /// <param name="array">Array to validate</param>
        /// <param name="expectedSize">Expected array length</param>
        /// <param name="paramName">Parameter name for error messages</param>
        /// <returns>True if array is valid</returns>
        public static bool ValidateArraySize(Array array, int expectedSize, string paramName)
        {
            if (array == null)
            {
                throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
            }

            if (array.Length != expectedSize)
            {
                throw new ArgumentException(
                    $"{paramName} must have exactly {expectedSize} elements, but has {array.Length}",
                    paramName);
            }

            return true;
        }

        /// <summary>
        /// Safely checks if the native DLL is available
        /// </summary>
        /// <returns>True if DLL can be loaded and basic functions are available</returns>
        public static bool IsDllAvailable()
        {
            try
            {
                // Try to call the version function as a connectivity test
                int version = ULL_GetVersion();
                return version > 0; // Valid version number indicates DLL is working
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                // Other exceptions might indicate the DLL is present but not working correctly
                return false;
            }
        }

        /// <summary>
        /// Gets the expected API version that this managed layer was built for
        /// </summary>
        /// <returns>Expected API version number</returns>
        public static int GetExpectedApiVersion()
        {
            return 1; // Should match ULL_API_VERSION in native code
        }

        /// <summary>
        /// Validates API compatibility between managed and native layers
        /// </summary>
        /// <returns>True if versions are compatible</returns>
        /// <exception cref="InvalidOperationException">Thrown if DLL is not available</exception>
        public static bool IsApiVersionCompatible()
        {
            if (!IsDllAvailable())
            {
                throw new InvalidOperationException(
                    "Cannot check API version compatibility: UnrealLiveLink.Native.dll is not available. " +
                    "Ensure the DLL is in the same directory as the executable or in the system PATH.");
            }

            int nativeVersion = ULL_GetVersion();
            int expectedVersion = GetExpectedApiVersion();

            return nativeVersion == expectedVersion;
        }
    }
}
