using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SimioUnrealEngineLiveLinkConnector.UnrealIntegration
{
    /// <summary>
    /// Singleton managing global LiveLink connection state and object registry.
    /// Provides high-level API for Simio Steps and Elements to interact with LiveLink.
    /// 
    /// Thread-safe implementation using ConcurrentDictionary for object registry.
    /// </summary>
    public sealed class LiveLinkManager
    {
        private static readonly Lazy<LiveLinkManager> _instance = 
            new Lazy<LiveLinkManager>(() => new LiveLinkManager(), true);

        private readonly object _initializationLock = new object();
        private readonly ConcurrentDictionary<string, LiveLinkObjectUpdater> _objects = 
            new ConcurrentDictionary<string, LiveLinkObjectUpdater>(StringComparer.Ordinal);

        private bool _isInitialized;
        private string? _currentSourceName;
        private volatile bool _lastConnectionCheck;
        private DateTime _lastConnectionCheckTime = DateTime.MinValue;

        // Connection check caching (avoid expensive P/Invoke calls)
        private const double CONNECTION_CHECK_CACHE_SECONDS = 1.0;

        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static LiveLinkManager Instance => _instance.Value;

        /// <summary>
        /// Gets whether LiveLink has been initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets the current source name (null if not initialized)
        /// </summary>
        public string? SourceName => _currentSourceName;

        /// <summary>
        /// Gets whether LiveLink connection is healthy (cached for performance)
        /// Cached for 1 second to avoid excessive P/Invoke calls
        /// </summary>
        public bool IsConnectionHealthy
        {
            get
            {
                if (!_isInitialized)
                    return false;

                // Use cached result if recent
                var now = DateTime.UtcNow;
                if ((now - _lastConnectionCheckTime).TotalSeconds < CONNECTION_CHECK_CACHE_SECONDS)
                {
                    return _lastConnectionCheck;
                }

                // Check connection status
                try
                {
                    int result = UnrealLiveLinkNative.ULL_IsConnected();
                    _lastConnectionCheck = UnrealLiveLinkNative.IsSuccess(result);
                    _lastConnectionCheckTime = now;
                    return _lastConnectionCheck;
                }
                catch (Exception)
                {
                    _lastConnectionCheck = false;
                    _lastConnectionCheckTime = now;
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the number of registered objects
        /// </summary>
        public int ObjectCount => _objects.Count;

        /// <summary>
        /// Gets the names of all registered objects (snapshot)
        /// </summary>
        public IEnumerable<string> ObjectNames => _objects.Keys;

        /// <summary>
        /// Private constructor for singleton pattern
        /// </summary>
        private LiveLinkManager()
        {
            _isInitialized = false;
            _currentSourceName = null;
        }

        /// <summary>
        /// Initializes LiveLink with the specified source name
        /// Safe to call multiple times - subsequent calls are ignored if already initialized with same source name
        /// </summary>
        /// <param name="sourceName">Name displayed in Unreal's LiveLink window</param>
        /// <returns>True if initialization succeeded or was already initialized</returns>
        /// <exception cref="ArgumentException">Thrown if sourceName is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown if already initialized with different source name</exception>
        /// <exception cref="DllNotFoundException">Thrown if native DLL is not available</exception>
        /// <exception cref="LiveLinkInitializationException">Thrown if native initialization fails</exception>
        public bool Initialize(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
            {
                throw new ArgumentException("Source name cannot be null or empty", nameof(sourceName));
            }

            lock (_initializationLock)
            {
                // Check if already initialized
                if (_isInitialized)
                {
                    if (string.Equals(_currentSourceName, sourceName, StringComparison.Ordinal))
                    {
                        return true; // Already initialized with same source name
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"LiveLink is already initialized with source name '{_currentSourceName}'. " +
                            $"Cannot reinitialize with '{sourceName}'. Call Shutdown() first if you need to change the source name.");
                    }
                }

                // Check if native DLL is available
                if (!UnrealLiveLinkNative.IsDllAvailable())
                {
                    throw new DllNotFoundException(
                        "UnrealLiveLink.Native.dll is not available. " +
                        "Ensure the DLL is in the same directory as the executable or in the system PATH. " +
                        "Check that all required dependencies (Unreal Engine runtime) are installed.");
                }

                // Check API version compatibility
                try
                {
                    if (!UnrealLiveLinkNative.IsApiVersionCompatible())
                    {
                        int nativeVersion = UnrealLiveLinkNative.ULL_GetVersion();
                        int expectedVersion = UnrealLiveLinkNative.GetExpectedApiVersion();
                        throw new LiveLinkInitializationException(
                            $"API version mismatch. Native DLL version: {nativeVersion}, " +
                            $"Expected version: {expectedVersion}. " +
                            "Please update the native DLL to match this managed layer version.");
                    }
                }
                catch (Exception ex) when (!(ex is LiveLinkInitializationException))
                {
                    throw new LiveLinkInitializationException(
                        "Failed to check API version compatibility. " +
                        "The native DLL may be corrupted or incompatible.", ex);
                }

                // Initialize native LiveLink
                try
                {
                    int result = UnrealLiveLinkNative.ULL_Initialize(sourceName);
                    if (!UnrealLiveLinkNative.IsSuccess(result))
                    {
                        string errorDescription = UnrealLiveLinkNative.GetReturnCodeDescription(result);
                        throw new LiveLinkInitializationException(
                            $"Native LiveLink initialization failed: {errorDescription} (Code: {result})");
                    }

                    _isInitialized = true;
                    _currentSourceName = sourceName;
                    _lastConnectionCheck = false; // Will be checked on first IsConnectionHealthy call
                    _lastConnectionCheckTime = DateTime.MinValue;

                    return true;
                }
                catch (Exception ex) when (!(ex is LiveLinkInitializationException))
                {
                    throw new LiveLinkInitializationException(
                        "Unexpected error during native LiveLink initialization. " +
                        "Check that Unreal Engine runtime dependencies are installed.", ex);
                }
            }
        }

        /// <summary>
        /// Shuts down LiveLink and releases all resources
        /// Safe to call multiple times
        /// </summary>
        public void Shutdown()
        {
            lock (_initializationLock)
            {
                if (!_isInitialized)
                {
                    return; // Already shut down
                }

                try
                {
                    // Dispose all managed objects
                    var objectsToDispose = new List<LiveLinkObjectUpdater>(_objects.Values);
                    _objects.Clear();

                    foreach (var obj in objectsToDispose)
                    {
                        try
                        {
                            obj.Dispose();
                        }
                        catch (Exception)
                        {
                            // Ignore errors during shutdown
                        }
                    }

                    // Shutdown native LiveLink
                    UnrealLiveLinkNative.ULL_Shutdown();
                }
                catch (Exception)
                {
                    // Ignore errors during shutdown
                }
                finally
                {
                    _isInitialized = false;
                    _currentSourceName = null;
                    _lastConnectionCheck = false;
                    _lastConnectionCheckTime = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// Gets an existing object updater or creates a new one
        /// Thread-safe operation
        /// </summary>
        /// <param name="objectName">Unique object identifier</param>
        /// <returns>LiveLinkObjectUpdater for the specified object</returns>
        /// <exception cref="ArgumentException">Thrown if objectName is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown if not initialized</exception>
        public LiveLinkObjectUpdater GetOrCreateObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            }

            ThrowIfNotInitialized();

            return _objects.GetOrAdd(objectName, name => new LiveLinkObjectUpdater(name));
        }

        /// <summary>
        /// Gets an existing object updater (does not create if not found)
        /// Thread-safe operation
        /// </summary>
        /// <param name="objectName">Object identifier to find</param>
        /// <returns>LiveLinkObjectUpdater if found, null otherwise</returns>
        /// <exception cref="ArgumentException">Thrown if objectName is null or empty</exception>
        public LiveLinkObjectUpdater GetObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            }

            _objects.TryGetValue(objectName, out LiveLinkObjectUpdater updater);
            return updater;
        }

        /// <summary>
        /// Checks if an object is registered
        /// </summary>
        /// <param name="objectName">Object identifier to check</param>
        /// <returns>True if object exists</returns>
        /// <exception cref="ArgumentException">Thrown if objectName is null or empty</exception>
        public bool HasObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            }

            return _objects.ContainsKey(objectName);
        }

        /// <summary>
        /// Removes an object from the registry and LiveLink
        /// Thread-safe operation
        /// </summary>
        /// <param name="objectName">Object identifier to remove</param>
        /// <returns>True if object was found and removed</returns>
        /// <exception cref="ArgumentException">Thrown if objectName is null or empty</exception>
        public bool RemoveObject(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            }

            if (_objects.TryRemove(objectName, out LiveLinkObjectUpdater updater))
            {
                try
                {
                    updater.Dispose(); // This will call RemoveObject() on the native side
                    return true;
                }
                catch (Exception)
                {
                    // Ignore errors during removal
                    return true; // Still removed from our registry
                }
            }

            return false; // Object not found
        }

        /// <summary>
        /// Removes all objects and clears the registry
        /// </summary>
        public void RemoveAllObjects()
        {
            var objectsToRemove = new List<LiveLinkObjectUpdater>(_objects.Values);
            _objects.Clear();

            foreach (var obj in objectsToRemove)
            {
                try
                {
                    obj.Dispose();
                }
                catch (Exception)
                {
                    // Ignore errors during cleanup
                }
            }
        }

        /// <summary>
        /// Updates a data subject (metrics/KPIs without 3D representation)
        /// This is a convenience method that directly calls the native API
        /// </summary>
        /// <param name="subjectName">Unique identifier for the data subject</param>
        /// <param name="propertyNames">Property names (null if already registered)</param>
        /// <param name="propertyValues">Property values</param>
        /// <exception cref="ArgumentException">Thrown if subjectName is null/empty or arrays are invalid</exception>
        /// <exception cref="InvalidOperationException">Thrown if not initialized</exception>
        public void UpdateDataSubject(string subjectName, string[]? propertyNames, float[] propertyValues)
        {
            if (string.IsNullOrWhiteSpace(subjectName))
            {
                throw new ArgumentException("Subject name cannot be null or empty", nameof(subjectName));
            }

            if (propertyValues == null)
            {
                throw new ArgumentNullException(nameof(propertyValues));
            }

            if (propertyNames != null && propertyNames.Length != propertyValues.Length)
            {
                throw new ArgumentException(
                    "Property names and values must have the same length if names are provided");
            }

            ThrowIfNotInitialized();

            UnrealLiveLinkNative.ULL_UpdateDataSubject(
                subjectName, propertyNames, propertyValues, propertyValues.Length);
        }

        /// <summary>
        /// Removes a data subject from LiveLink
        /// </summary>
        /// <param name="subjectName">Subject identifier to remove</param>
        /// <exception cref="ArgumentException">Thrown if subjectName is null or empty</exception>
        /// <exception cref="InvalidOperationException">Thrown if not initialized</exception>
        public void RemoveDataSubject(string subjectName)
        {
            if (string.IsNullOrWhiteSpace(subjectName))
            {
                throw new ArgumentException("Subject name cannot be null or empty", nameof(subjectName));
            }

            ThrowIfNotInitialized();

            UnrealLiveLinkNative.ULL_RemoveDataSubject(subjectName);
        }

        /// <summary>
        /// Gets debug information about the manager state
        /// </summary>
        /// <returns>Debug string with initialization status and object count</returns>
        public override string ToString()
        {
            string status = _isInitialized ? "INITIALIZED" : "NOT_INITIALIZED";
            string source = _isInitialized ? $", Source: '{_currentSourceName}'" : "";
            return $"LiveLinkManager({status}{source}, Objects: {_objects.Count})";
        }

        /// <summary>
        /// Throws InvalidOperationException if not initialized
        /// </summary>
        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException(
                    "LiveLink is not initialized. Call Initialize(sourceName) first.");
            }
        }
    }

    /// <summary>
    /// Exception thrown when LiveLink initialization fails
    /// </summary>
    public class LiveLinkInitializationException : Exception
    {
        public LiveLinkInitializationException(string message) : base(message) { }
        public LiveLinkInitializationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
