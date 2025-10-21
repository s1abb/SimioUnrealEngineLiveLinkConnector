using System;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;
using SimioUnrealEngineLiveLinkConnector.Utils;

namespace SimioUnrealEngineLiveLinkConnector.Element
{
    /// <summary>
    /// Element that manages the LiveLink connection lifecycle for a simulation run.
    /// Provides connection health status and manages initialization/shutdown.
    /// Enhanced with 7 essential properties for comprehensive configuration.
    /// </summary>
    public class SimioUnrealEngineLiveLinkElement : IElement
    {
        private readonly IElementData _elementData;
        private readonly LiveLinkConfiguration _configuration;

        public SimioUnrealEngineLiveLinkElement(IElementData elementData)
        {
            _elementData = elementData ?? throw new ArgumentNullException(nameof(elementData));
            
            // Read ALL 7 essential properties with validation
            _configuration = ReadAndValidateProperties(elementData);
        }

        /// <summary>
        /// Reads all essential properties from ElementData and creates validated configuration
        /// </summary>
        private LiveLinkConfiguration ReadAndValidateProperties(IElementData elementData)
        {
            var context = elementData.ExecutionContext;
            
            // Read properties with defaults
            string sourceName = ReadStringProperty("SourceName", elementData, "SimioSimulation");
            bool enableLogging = ReadBooleanProperty("EnableLogging", elementData, false);

            // Validate and create configuration
            var config = new LiveLinkConfiguration
            {
                SourceName = sourceName,
                EnableLogging = enableLogging
            };

            return config;
        }

        /// <summary>
        /// Helper method to read string properties with defaults
        /// </summary>
        private string ReadStringProperty(string propertyName, IElementData elementData, string defaultValue)
        {
            try
            {
                IPropertyReader reader = elementData.Properties.GetProperty(propertyName);
                string value = reader.GetStringValue(elementData.ExecutionContext);
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Helper method to read boolean properties with defaults
        /// </summary>
        private bool ReadBooleanProperty(string propertyName, IElementData elementData, bool defaultValue)
        {
            try
            {
                IPropertyReader reader = elementData.Properties.GetProperty(propertyName);
                // Convert double to boolean (0 = false, non-zero = true)
                double value = reader.GetDoubleValue(elementData.ExecutionContext);
                return Math.Abs(value) > 1e-10;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Helper method to read integer properties with defaults
        /// </summary>
        private int ReadIntegerProperty(string propertyName, IElementData elementData, int defaultValue)
        {
            try
            {
                IPropertyReader reader = elementData.Properties.GetProperty(propertyName);
                return (int)reader.GetDoubleValue(elementData.ExecutionContext);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Helper method to read real properties with defaults
        /// </summary>
        private double ReadRealProperty(string propertyName, IElementData elementData, double defaultValue)
        {
            try
            {
                IPropertyReader reader = elementData.Properties.GetProperty(propertyName);
                return reader.GetDoubleValue(elementData.ExecutionContext);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Gets whether the LiveLink connection is currently healthy.
        /// Used by Steps to validate connectivity before attempting operations.
        /// </summary>
        public bool IsConnectionHealthy
        {
            get
            {
                try
                {
                    return LiveLinkManager.Instance.IsConnectionHealthy;
                }
                catch
                {
                    // If anything fails checking connection health, assume unhealthy
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the source name configured for this LiveLink connection.
        /// </summary>
        public string SourceName => _configuration.SourceName;

        /// <summary>
        /// Gets the complete configuration for this Element
        /// </summary>
        public LiveLinkConfiguration Configuration => _configuration;

        /// <summary>
        /// Initialize the LiveLink connection when the simulation starts.
        /// Called by Simio at the beginning of simulation execution.
        /// Enhanced with TraceInformation for user visibility.
        /// </summary>
        public void Initialize()
        {
            // Validate configuration first
            var validationErrors = _configuration.Validate();
            if (validationErrors.Length > 0)
            {
                foreach (string error in validationErrors)
                {
                    _elementData.ExecutionContext.ExecutionInformation.ReportError(error);
                }
                return;
            }

            try
            {
                // Initialize the LiveLink connection via the singleton manager
                LiveLinkManager.Instance.Initialize(_configuration);
                
                // Report successful initialization with Message Bus discovery info
                _elementData.ExecutionContext.ExecutionInformation.TraceInformation(
                    $"LiveLink Message Bus provider '{_configuration.SourceName}' initialized (UDP multicast auto-discovery).");
            }
            catch (Exception ex)
            {
                _elementData.ExecutionContext.ExecutionInformation.ReportError(
                    $"Failed to initialize LiveLink connection: {ex.Message}");
            }
        }

        /// <summary>
        /// Shutdown the LiveLink connection when the simulation ends.
        /// Called by Simio at the end of simulation execution.
        /// Enhanced with TraceInformation for user visibility.
        /// </summary>
        public void Shutdown()
        {
            try
            {
                LiveLinkManager.Instance.Shutdown();
                
                // Note: TraceInformation removed from shutdown to prevent overwriting simulation traces
                // Shutdown happens after simulation ends and may clear the trace file
            }
            catch (Exception ex)
            {
                // Log shutdown errors but don't fail the simulation
                _elementData.ExecutionContext.ExecutionInformation.ReportError(
                    $"Warning: Error during LiveLink shutdown: {ex.Message}");
            }
        }
    }
}
