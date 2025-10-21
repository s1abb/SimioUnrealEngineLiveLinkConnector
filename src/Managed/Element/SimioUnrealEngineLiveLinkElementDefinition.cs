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

        public string Description => "Element that manages a LiveLink Message Bus connection to Unreal Engine for streaming real-time simulation data via UDP multicast auto-discovery.";

        public Image Icon => null!;

        public Guid UniqueID => MY_ID;

        public void DefineSchema(IElementSchema schema)
        {
            // === LiveLink Connection Category ===
            var sourceNameProperty = schema.PropertyDefinitions.AddStringProperty("SourceName", "SimioSimulation");
            sourceNameProperty.DisplayName = "Source Name";
            sourceNameProperty.Description = "Name displayed in Unreal Engine's LiveLink Sources window.";
            sourceNameProperty.CategoryName = "LiveLink Connection";

            // === Logging Category ===
            var enableLoggingProperty = schema.PropertyDefinitions.AddExpressionProperty("EnableLogging", "True");
            enableLoggingProperty.DisplayName = "Enable Logging";
            enableLoggingProperty.Description = "Enable or disable detailed native logging for troubleshooting. View logs using DebugView++ (https://github.com/CobaltFusion/DebugViewPP).";
            enableLoggingProperty.CategoryName = "Logging";
        }

        public IElement CreateElement(IElementData elementData)
        {
            return new SimioUnrealEngineLiveLinkElement(elementData);
        }
    }
}
