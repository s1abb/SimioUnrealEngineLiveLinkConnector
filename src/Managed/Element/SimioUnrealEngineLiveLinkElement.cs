using System;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;

namespace SimioUnrealEngineLiveLinkConnector.Element
{
    /// <summary>
    /// Element that manages the LiveLink connection lifecycle for a simulation run.
    /// Provides connection health status and manages initialization/shutdown.
    /// </summary>
    [NullableContext(1)]
    [Nullable(0)]
    public class SimioUnrealEngineLiveLinkElement : IElement
    {
        private readonly IElementData _elementData;
        private readonly string _sourceName;

        public SimioUnrealEngineLiveLinkElement(IElementData elementData)
        {
            _elementData = elementData ?? throw new ArgumentNullException(nameof(elementData));
            
            // Read the SourceName property from the element data
            IPropertyReader sourceNameReader = elementData.Properties.GetProperty("SourceName");
            _sourceName = sourceNameReader.GetStringValue(elementData.ExecutionContext);
            
            // Ensure we have a valid source name
            if (string.IsNullOrWhiteSpace(_sourceName))
            {
                _sourceName = "SimioSimulation"; // Default fallback
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
        public string SourceName => _sourceName;

        /// <summary>
        /// Initialize the LiveLink connection when the simulation starts.
        /// Called by Simio at the beginning of simulation execution.
        /// </summary>
        public void Initialize()
        {
            if (string.IsNullOrWhiteSpace(_sourceName))
            {
                _elementData.ExecutionContext.ExecutionInformation.ReportError(
                    "Source Name must not be empty");
                return;
            }

            try
            {
                // Initialize the LiveLink connection via the singleton manager
                LiveLinkManager.Instance.Initialize(_sourceName);
                
                // Note: We don't report success here as Simio expects Initialize to be quiet on success
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
        /// </summary>
        public void Shutdown()
        {
            try
            {
                LiveLinkManager.Instance.Shutdown();
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
