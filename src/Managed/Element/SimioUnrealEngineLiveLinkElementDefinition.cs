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
            // Source name property - appears in Unreal's LiveLink window
            var sourceNameProperty = schema.PropertyDefinitions.AddStringProperty("SourceName", "SimioSimulation");
            sourceNameProperty.DisplayName = "Source Name";
            sourceNameProperty.Description = "Name that appears in Unreal Engine's LiveLink Sources panel";
            sourceNameProperty.CategoryName = "LiveLink Connection";
        }

        public IElement CreateElement(IElementData elementData)
        {
            return new SimioUnrealEngineLiveLinkElement(elementData);
        }
    }
}
