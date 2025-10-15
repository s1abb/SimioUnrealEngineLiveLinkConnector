using System;
using System.Drawing;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    /// <summary>
    /// Step definition for creating objects in Unreal Engine LiveLink.
    /// Defines schema for object name, position, and orientation properties.
    /// </summary>
    public class CreateObjectStepDefinition : IStepDefinition
    {
        // Unique identifier for this step type
        private static readonly Guid MY_ID = new Guid("B2C3D4E5-F6A7-8901-BCDE-F23456789012");

        public string Name => "CreateObject";

        public string Description => "Creates a new object in Unreal Engine LiveLink with specified position and orientation";

        public Image Icon => null!;

        public Guid UniqueID => MY_ID;

        public int NumberOfExits => 1;

        public void DefineSchema(IPropertyDefinitions schema)
        {
            // Element reference to the LiveLink connector  
            var connectorProperty = schema.AddElementProperty("UnrealEngineConnector", SimioUnrealEngineLiveLinkElementDefinition.MY_ID);
            connectorProperty.DisplayName = "Unreal Engine Connector";
            connectorProperty.Description = "Reference to the Unreal Engine LiveLink Connector element";
            connectorProperty.CategoryName = "LiveLink Connection";

            // Object name - what this object will be called in Unreal
            var objectNameProperty = schema.AddExpressionProperty("ObjectName", "Entity.Name");
            objectNameProperty.DisplayName = "Object Name";
            objectNameProperty.Description = "Name of the object as it appears in Unreal Engine LiveLink";
            objectNameProperty.CategoryName = "Object Properties";

            // Position properties (in Simio coordinate system - meters)
            var xProperty = schema.AddExpressionProperty("X", "Entity.Location.X");
            xProperty.DisplayName = "X Position";
            xProperty.Description = "X coordinate in Simio coordinate system (meters)";
            xProperty.CategoryName = "Transform";

            var yProperty = schema.AddExpressionProperty("Y", "Entity.Location.Y");
            yProperty.DisplayName = "Y Position";
            yProperty.Description = "Y coordinate in Simio coordinate system (meters)";
            yProperty.CategoryName = "Transform";

            var zProperty = schema.AddExpressionProperty("Z", "Entity.Location.Z");
            zProperty.DisplayName = "Z Position";
            zProperty.Description = "Z coordinate in Simio coordinate system (meters)";
            zProperty.CategoryName = "Transform";

            // Rotation properties using Simio movement conventions (in degrees)
            var headingProperty = schema.AddExpressionProperty("Heading", "Entity.Movement.Heading");
            headingProperty.DisplayName = "Heading";
            headingProperty.Description = "Rotation around vertical axis (0° = North, 90° = East, 180° = South, 270° = West)";
            headingProperty.CategoryName = "Rotation";

            var pitchProperty = schema.AddExpressionProperty("Pitch", "Entity.Movement.Pitch");
            pitchProperty.DisplayName = "Pitch";
            pitchProperty.Description = "Rotation relative to floor (positive = climb, negative = descend)";
            pitchProperty.CategoryName = "Rotation";

            var rollProperty = schema.AddExpressionProperty("Roll", "Entity.Movement.Roll");
            rollProperty.DisplayName = "Roll";
            rollProperty.Description = "Banking/tilting rotation (standard aviation roll convention)";
            rollProperty.CategoryName = "Rotation";
        }

        public IStep CreateStep(IPropertyReaders propertyReaders)
        {
            return new CreateObjectStep(propertyReaders);
        }
    }
}
