using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    /// <summary>
    /// Step definition for creating objects in Unreal Engine LiveLink.
    /// Defines schema for object name, position, and orientation properties.
    /// </summary>
    [NullableContext(1)]
    [Nullable(0)]
    public class CreateObjectStepDefinition : IStepDefinition
    {
        // Unique identifier for this step type
        private static readonly Guid MY_ID = new Guid("B2C3D4E5-F6A7-8901-BCDE-F23456789012");

        public string Name => "CreateObject";

        public string Description => "Creates a new object in Unreal Engine LiveLink with specified position and orientation";

        public Image? Icon => null;

        public Guid UniqueID => MY_ID;

        public void DefineSchema(IStepSchema schema)
        {
            // Element reference to the LiveLink connector
            var connectorProperty = schema.PropertyDefinitions.AddElementProperty("UnrealEngineConnector");
            connectorProperty.DisplayName = "Unreal Engine Connector";
            connectorProperty.Description = "Reference to the Unreal Engine LiveLink Connector element";
            connectorProperty.CategoryName = "LiveLink Connection";

            // Object name - what this object will be called in Unreal
            var objectNameProperty = schema.PropertyDefinitions.AddExpressionProperty("ObjectName", "Entity.Name");
            objectNameProperty.DisplayName = "Object Name";
            objectNameProperty.Description = "Name of the object as it appears in Unreal Engine LiveLink";
            objectNameProperty.CategoryName = "Object Properties";

            // Position properties (in Simio coordinate system - meters)
            var xProperty = schema.PropertyDefinitions.AddExpressionProperty("X", "Entity.Location.X");
            xProperty.DisplayName = "X Position";
            xProperty.Description = "X coordinate in Simio coordinate system (meters)";
            xProperty.CategoryName = "Transform";

            var yProperty = schema.PropertyDefinitions.AddExpressionProperty("Y", "Entity.Location.Y");
            yProperty.DisplayName = "Y Position";
            yProperty.Description = "Y coordinate in Simio coordinate system (meters)";
            yProperty.CategoryName = "Transform";

            var zProperty = schema.PropertyDefinitions.AddExpressionProperty("Z", "Entity.Location.Z");
            zProperty.DisplayName = "Z Position";
            zProperty.Description = "Z coordinate in Simio coordinate system (meters)";
            zProperty.CategoryName = "Transform";

            // Orientation properties (in degrees)
            var orientXProperty = schema.PropertyDefinitions.AddExpressionProperty("OrientationX", "0");
            orientXProperty.DisplayName = "X Rotation";
            orientXProperty.Description = "Rotation around X axis in degrees";
            orientXProperty.CategoryName = "Transform";

            var orientYProperty = schema.PropertyDefinitions.AddExpressionProperty("OrientationY", "0");
            orientYProperty.DisplayName = "Y Rotation";
            orientYProperty.Description = "Rotation around Y axis in degrees";
            orientYProperty.CategoryName = "Transform";

            var orientZProperty = schema.PropertyDefinitions.AddExpressionProperty("OrientationZ", "Entity.Heading");
            orientZProperty.DisplayName = "Z Rotation";
            orientZProperty.Description = "Rotation around Z axis in degrees (typically heading)";
            orientZProperty.CategoryName = "Transform";
        }

        public IStep CreateStep(IPropertyReaders propertyReaders)
        {
            return new CreateObjectStep(propertyReaders);
        }
    }
}
