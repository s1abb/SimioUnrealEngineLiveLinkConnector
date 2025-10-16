using System;
using System.Drawing;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioUnrealEngineLiveLinkConnector.Element
{
    /// <summary>
    /// Element definition for the Unreal Engine LiveLink Connector.
    /// Defines the schema, properties, and factory method for creating connector elements.
    /// </summary>
    public class SimioUnrealEngineLiveLinkElementDefinition : IElementDefinition
    {
        // Unique identifier for this element type
        public static readonly Guid MY_ID = new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

        public string Name => "UnrealEngineLiveLinkConnector";

        public string Description => "Element that manages a LiveLink connection to Unreal Engine for streaming real-time simulation data";

        public Image Icon => null!;

        public Guid UniqueID => MY_ID;

        public void DefineSchema(IElementSchema schema)
        {
            // === LiveLink Connection Category ===
            var sourceNameProperty = schema.PropertyDefinitions.AddStringProperty("SourceName", "SimioSimulation");
            sourceNameProperty.DisplayName = "Source Name";
            sourceNameProperty.Description = "Name displayed in Unreal Engine's LiveLink Sources window";
            sourceNameProperty.CategoryName = "LiveLink Connection";

            var liveLinkHostProperty = schema.PropertyDefinitions.AddStringProperty("LiveLinkHost", "localhost");
            liveLinkHostProperty.DisplayName = "LiveLink Host";
            liveLinkHostProperty.Description = "IP address or hostname of the Unreal Engine LiveLink server";
            liveLinkHostProperty.CategoryName = "LiveLink Connection";

            var liveLinkPortProperty = schema.PropertyDefinitions.AddRealProperty("LiveLinkPort", 11111);
            liveLinkPortProperty.DisplayName = "LiveLink Port";
            liveLinkPortProperty.Description = "Port number for the LiveLink server (typically 11111)";
            liveLinkPortProperty.CategoryName = "LiveLink Connection";

            var connectionTimeoutProperty = schema.PropertyDefinitions.AddRealProperty("ConnectionTimeout", 5.0);
            connectionTimeoutProperty.DisplayName = "Connection Timeout (s)";
            connectionTimeoutProperty.Description = "Maximum time in seconds to wait for LiveLink connection";
            connectionTimeoutProperty.CategoryName = "LiveLink Connection";

            var retryAttemptsProperty = schema.PropertyDefinitions.AddRealProperty("RetryAttempts", 3);
            retryAttemptsProperty.DisplayName = "Retry Attempts";
            retryAttemptsProperty.Description = "Number of times to retry failed LiveLink connections";
            retryAttemptsProperty.CategoryName = "LiveLink Connection";

            // === Logging Category ===
                var enableLoggingProperty = schema.PropertyDefinitions.AddExpressionProperty("EnableLogging", "True");
            enableLoggingProperty.DisplayName = "Enable Logging";
                enableLoggingProperty.Description = "Enable or disable detailed logging for troubleshooting (True/False, default True)";
            enableLoggingProperty.CategoryName = "Logging";

            var logFilePathProperty = schema.PropertyDefinitions.AddStringProperty("LogFilePath", "SimioUnrealLiveLink.log");
            logFilePathProperty.DisplayName = "Log File Path";
            logFilePathProperty.Description = "Path to log file for LiveLink operations (relative to simulation or absolute)";
            logFilePathProperty.CategoryName = "Logging";

            // === Unreal Engine Category ===
            var unrealEnginePathProperty = schema.PropertyDefinitions.AddStringProperty("UnrealEnginePath", string.Empty);
            unrealEnginePathProperty.DisplayName = "Unreal Engine Path";
            unrealEnginePathProperty.Description = "Full path to Unreal Engine installation directory (e.g., 'C:\\Program Files\\Epic Games\\UE_5.6')";
            unrealEnginePathProperty.CategoryName = "Unreal Engine";
        }

        public IElement CreateElement(IElementData elementData)
        {
            return new SimioUnrealEngineLiveLinkElement(elementData);
        }
    }
}
