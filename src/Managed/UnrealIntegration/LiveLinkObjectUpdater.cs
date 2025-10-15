using System;
using System.Linq;

namespace SimioUnrealEngineLiveLinkConnector.UnrealIntegration
{
    /// <summary>
    /// Per-object wrapper managing LiveLink object registration and update state.
    /// Handles lazy registration, property management, and coordinate conversion.
    /// </summary>
    public class LiveLinkObjectUpdater : IDisposable
    {
        private readonly string _objectName;
        private bool _isRegistered;
        private bool _hasProperties;
        private string[]? _registeredPropertyNames;
        private float[]? _propertyBuffer; // Reused to avoid allocations
        private bool _disposed;

        /// <summary>
        /// Gets the object name this updater manages
        /// </summary>
        public string ObjectName => _objectName;

        /// <summary>
        /// Gets whether this object is registered with LiveLink
        /// </summary>
        public bool IsRegistered => _isRegistered;

        /// <summary>
        /// Gets whether this object has custom properties
        /// </summary>
        public bool HasProperties => _hasProperties;

        /// <summary>
        /// Gets the registered property names (read-only copy)
        /// </summary>
        public string[]? PropertyNames => _registeredPropertyNames?.ToArray();

        /// <summary>
        /// Creates a new LiveLink object updater
        /// </summary>
        /// <param name="objectName">Unique object identifier</param>
        /// <exception cref="ArgumentException">Thrown if objectName is null or empty</exception>
        public LiveLinkObjectUpdater(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                throw new ArgumentException("Object name cannot be null or empty", nameof(objectName));
            }

            _objectName = objectName;
            _isRegistered = false;
            _hasProperties = false;
            _registeredPropertyNames = null;
            _propertyBuffer = null;
        }

        /// <summary>
        /// Updates object transform only (no properties)
        /// Auto-registers object if not already registered
        /// </summary>
        /// <param name="simioX">Simio X position in meters</param>
        /// <param name="simioY">Simio Y position in meters</param>
        /// <param name="simioZ">Simio Z position in meters</param>
        /// <param name="simioRotX">Simio X rotation in degrees</param>
        /// <param name="simioRotY">Simio Y rotation in degrees</param>
        /// <param name="simioRotZ">Simio Z rotation in degrees</param>
        /// <param name="simioScaleX">Simio X scale factor (optional, default 1.0)</param>
        /// <param name="simioScaleY">Simio Y scale factor (optional, default 1.0)</param>
        /// <param name="simioScaleZ">Simio Z scale factor (optional, default 1.0)</param>
        /// <exception cref="ObjectDisposedException">Thrown if updater has been disposed</exception>
        /// <exception cref="InvalidOperationException">Thrown if object was registered with properties</exception>
        public void UpdateTransform(
            double simioX, double simioY, double simioZ,
            double simioRotX, double simioRotY, double simioRotZ,
            double simioScaleX = 1.0, double simioScaleY = 1.0, double simioScaleZ = 1.0)
        {
            ThrowIfDisposed();

            // Ensure object is registered without properties
            EnsureRegistered(withProperties: false);

            // Validate that this object wasn't registered with properties
            if (_hasProperties)
            {
                throw new InvalidOperationException(
                    $"Object '{_objectName}' was registered with properties. " +
                    "Use UpdateWithProperties() instead of UpdateTransform().");
            }

            // Convert coordinates
            var transform = CoordinateConverter.SimioToUnrealTransform(
                simioX, simioY, simioZ,
                simioRotX, simioRotY, simioRotZ,
                simioScaleX, simioScaleY, simioScaleZ);

            // Update via P/Invoke
            UnrealLiveLinkNative.ULL_UpdateObject(_objectName, ref transform);
        }

        /// <summary>
        /// Updates object transform with custom properties
        /// Auto-registers object with properties if not already registered
        /// </summary>
        /// <param name="simioX">Simio X position in meters</param>
        /// <param name="simioY">Simio Y position in meters</param>
        /// <param name="simioZ">Simio Z position in meters</param>
        /// <param name="simioRotX">Simio X rotation in degrees</param>
        /// <param name="simioRotY">Simio Y rotation in degrees</param>
        /// <param name="simioRotZ">Simio Z rotation in degrees</param>
        /// <param name="propertyNames">Array of property names (used for registration if needed)</param>
        /// <param name="propertyValues">Array of property values matching the names</param>
        /// <param name="simioScaleX">Simio X scale factor (optional, default 1.0)</param>
        /// <param name="simioScaleY">Simio Y scale factor (optional, default 1.0)</param>
        /// <param name="simioScaleZ">Simio Z scale factor (optional, default 1.0)</param>
        /// <exception cref="ObjectDisposedException">Thrown if updater has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown if propertyNames or propertyValues are null</exception>
        /// <exception cref="ArgumentException">Thrown if property arrays have different lengths or property count mismatch</exception>
        public void UpdateWithProperties(
            double simioX, double simioY, double simioZ,
            double simioRotX, double simioRotY, double simioRotZ,
            string[] propertyNames, float[] propertyValues,
            double simioScaleX = 1.0, double simioScaleY = 1.0, double simioScaleZ = 1.0)
        {
            ThrowIfDisposed();

            // Validate input arrays
            if (propertyNames == null)
                throw new ArgumentNullException(nameof(propertyNames));
            if (propertyValues == null)
                throw new ArgumentNullException(nameof(propertyValues));
            if (propertyNames.Length != propertyValues.Length)
            {
                throw new ArgumentException(
                    $"Property names and values arrays must have the same length. " +
                    $"Names: {propertyNames.Length}, Values: {propertyValues.Length}");
            }

            // Ensure object is registered with properties
            EnsureRegisteredWithProperties(propertyNames);

            // Validate property count matches registration
            if (_registeredPropertyNames != null && _registeredPropertyNames.Length != propertyNames.Length)
            {
                throw new ArgumentException(
                    $"Property count mismatch for object '{_objectName}'. " +
                    $"Expected {_registeredPropertyNames.Length} properties, got {propertyNames.Length}.");
            }

            // Convert coordinates
            var transform = CoordinateConverter.SimioToUnrealTransform(
                simioX, simioY, simioZ,
                simioRotX, simioRotY, simioRotZ,
                simioScaleX, simioScaleY, simioScaleZ);

            // Prepare property buffer (reuse to avoid allocations)
            PreparePropertyBuffer(propertyValues.Length);
            Array.Copy(propertyValues, _propertyBuffer, propertyValues.Length);

            // Update via P/Invoke
            UnrealLiveLinkNative.ULL_UpdateObjectWithProperties(
                _objectName, ref transform, _propertyBuffer!, propertyValues.Length);
        }

        /// <summary>
        /// Explicitly registers the object without properties
        /// Usually not needed as UpdateTransform() auto-registers
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if updater has been disposed</exception>
        /// <exception cref="InvalidOperationException">Thrown if already registered with properties</exception>
        public void RegisterObject()
        {
            ThrowIfDisposed();
            EnsureRegistered(withProperties: false);
        }

        /// <summary>
        /// Explicitly registers the object with properties
        /// Usually not needed as UpdateWithProperties() auto-registers
        /// </summary>
        /// <param name="propertyNames">Array of property names</param>
        /// <exception cref="ObjectDisposedException">Thrown if updater has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown if propertyNames is null</exception>
        /// <exception cref="InvalidOperationException">Thrown if already registered with different properties</exception>
        public void RegisterObjectWithProperties(string[] propertyNames)
        {
            ThrowIfDisposed();
            
            if (propertyNames == null)
                throw new ArgumentNullException(nameof(propertyNames));

            EnsureRegisteredWithProperties(propertyNames);
        }

        /// <summary>
        /// Removes this object from LiveLink
        /// Object becomes stale in Unreal after ~5 seconds
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if updater has been disposed</exception>
        public void RemoveObject()
        {
            ThrowIfDisposed();

            if (_isRegistered)
            {
                UnrealLiveLinkNative.ULL_RemoveObject(_objectName);
                _isRegistered = false;
                _hasProperties = false;
                _registeredPropertyNames = null;
            }
        }

        /// <summary>
        /// Transmits custom data properties to the object
        /// Auto-registers object with properties if needed
        /// </summary>
        /// <param name="dataValues">Dictionary of property names and values to transmit</param>
        /// <exception cref="ObjectDisposedException">Thrown if updater has been disposed</exception>
        /// <exception cref="ArgumentNullException">Thrown if dataValues is null</exception>
        /// <exception cref="InvalidOperationException">Thrown if object was registered without properties</exception>
        public void TransmitData(System.Collections.Generic.Dictionary<string, double> dataValues)
        {
            ThrowIfDisposed();

            if (dataValues == null)
                throw new ArgumentNullException(nameof(dataValues));

            if (dataValues.Count == 0)
                return; // Nothing to transmit

            // Extract property names and values
            string[] propertyNames = new string[dataValues.Count];
            float[] propertyValues = new float[dataValues.Count];
            
            int index = 0;
            foreach (var kvp in dataValues)
            {
                propertyNames[index] = kvp.Key;
                propertyValues[index] = (float)kvp.Value;
                index++;
            }

            // Ensure object is registered with these properties
            EnsureRegisteredWithProperties(propertyNames);

            // Validate that this object was registered with properties
            if (!_hasProperties)
            {
                throw new InvalidOperationException(
                    $"Object '{_objectName}' was registered without properties. " +
                    "Use UpdateTransform() for objects without custom data.");
            }

            // Update properties using zero transform (position/rotation won't change)
            var transform = new ULL_Transform
            {
                position = new double[] { 0, 0, 0 },
                rotation = new double[] { 0, 0, 0, 1 }, // Identity quaternion
                scale = new double[] { 1, 1, 1 }
            };

            // Copy values to our reusable buffer (matching registered property order)
            Array.Copy(propertyValues, _propertyBuffer!, Math.Min(propertyValues.Length, _propertyBuffer!.Length));

            // Update via P/Invoke (transform stays at origin, only properties change)
            UnrealLiveLinkNative.ULL_UpdateObjectWithProperties(
                _objectName, ref transform, _propertyBuffer!, propertyValues.Length);
        }

        /// <summary>
        /// Gets debug information about this updater
        /// </summary>
        /// <returns>Debug string with registration status and properties</returns>
        public override string ToString()
        {
            if (_disposed)
                return $"LiveLinkObjectUpdater('{_objectName}', DISPOSED)";

            string status = _isRegistered ? "REGISTERED" : "NOT_REGISTERED";
            string props = _hasProperties ? $", Props: [{string.Join(", ", _registeredPropertyNames ?? new string[0])}]" : "";
            
            return $"LiveLinkObjectUpdater('{_objectName}', {status}{props})";
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the updater and removes the object from LiveLink
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Remove object from LiveLink
                    try
                    {
                        RemoveObject();
                    }
                    catch (Exception)
                    {
                        // Ignore errors during disposal
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup if Dispose() wasn't called
        /// </summary>
        ~LiveLinkObjectUpdater()
        {
            Dispose(false);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Ensures object is registered (without properties)
        /// </summary>
        /// <param name="withProperties">Whether to register with properties</param>
        private void EnsureRegistered(bool withProperties)
        {
            if (_isRegistered)
            {
                // Already registered - validate consistency
                if (withProperties && !_hasProperties)
                {
                    throw new InvalidOperationException(
                        $"Object '{_objectName}' is already registered without properties. " +
                        "Cannot change to properties mode.");
                }
                if (!withProperties && _hasProperties)
                {
                    throw new InvalidOperationException(
                        $"Object '{_objectName}' is already registered with properties. " +
                        "Use UpdateWithProperties() instead of UpdateTransform().");
                }
                return;
            }

            // Register for the first time
            if (withProperties)
            {
                throw new InvalidOperationException(
                    "Cannot register with properties without specifying property names. " +
                    "Use EnsureRegisteredWithProperties() instead.");
            }

            UnrealLiveLinkNative.ULL_RegisterObject(_objectName);
            _isRegistered = true;
            _hasProperties = false;
        }

        /// <summary>
        /// Ensures object is registered with specified properties
        /// </summary>
        /// <param name="propertyNames">Property names to register</param>
        private void EnsureRegisteredWithProperties(string[] propertyNames)
        {
            if (_isRegistered)
            {
                // Already registered - validate property consistency
                if (!_hasProperties)
                {
                    throw new InvalidOperationException(
                        $"Object '{_objectName}' is already registered without properties. " +
                        "Cannot add properties to an existing registration.");
                }

                // Check if property names match
                if (_registeredPropertyNames != null && 
                    !PropertyNamesMatch(_registeredPropertyNames, propertyNames))
                {
                    throw new InvalidOperationException(
                        $"Object '{_objectName}' is already registered with different properties. " +
                        $"Registered: [{string.Join(", ", _registeredPropertyNames)}], " +
                        $"Requested: [{string.Join(", ", propertyNames)}]");
                }
                return;
            }

            // Register for the first time with properties
            UnrealLiveLinkNative.ULL_RegisterObjectWithProperties(
                _objectName, propertyNames, propertyNames.Length);

            _isRegistered = true;
            _hasProperties = true;
            _registeredPropertyNames = propertyNames.ToArray(); // Store a copy
        }

        /// <summary>
        /// Prepares the property buffer with the specified size
        /// </summary>
        /// <param name="requiredSize">Required buffer size</param>
        private void PreparePropertyBuffer(int requiredSize)
        {
            if (_propertyBuffer == null || _propertyBuffer.Length < requiredSize)
            {
                _propertyBuffer = new float[requiredSize];
            }
        }

        /// <summary>
        /// Checks if two property name arrays are equivalent
        /// </summary>
        /// <param name="array1">First array</param>
        /// <param name="array2">Second array</param>
        /// <returns>True if arrays contain the same names in the same order</returns>
        private static bool PropertyNamesMatch(string[] array1, string[] array2)
        {
            if (array1.Length != array2.Length)
                return false;

            for (int i = 0; i < array1.Length; i++)
            {
                if (!string.Equals(array1[i], array2[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Throws ObjectDisposedException if this updater has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(LiveLinkObjectUpdater),
                    $"LiveLinkObjectUpdater for '{_objectName}' has been disposed");
            }
        }

        #endregion
    }
}
