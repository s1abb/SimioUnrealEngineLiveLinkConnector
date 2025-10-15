using System;
using System.Drawing;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    /// <summary>
    /// Step definition for updating object position and orientation in Unreal Engine LiveLink.
    /// Schema is identical to CreateObject - used for frequent transform updates during simulation.
    /// </summary>
    public class SetObjectPositionOrientationStepDefinition : IStepDefinition
    {
        // Unique identifier for this step type
        private static readonly Guid MY_ID = new Guid("C3D4E5F6-A7B8-9012-CDEF-345678901234");

        public string Name => "SetObjectPositionOrientation";

        public string Description => "Updates the position and orientation of an existing object in Unreal Engine LiveLink";

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

            // Object name - identifies which object to update
            var objectNameProperty = schema.AddExpressionProperty("ObjectName", "Entity.Name");
            objectNameProperty.DisplayName = "Object Name";
            objectNameProperty.Description = "Name of the object to update in Unreal Engine LiveLink";
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

            // Orientation properties (in degrees)
            var orientXProperty = schema.AddExpressionProperty("OrientationX", "0");
            orientXProperty.DisplayName = "X Rotation";
            orientXProperty.Description = "Rotation around X axis in degrees";
            orientXProperty.CategoryName = "Transform";

            var orientYProperty = schema.AddExpressionProperty("OrientationY", "0");
            orientYProperty.DisplayName = "Y Rotation";
            orientYProperty.Description = "Rotation around Y axis in degrees";
            orientYProperty.CategoryName = "Transform";

            var orientZProperty = schema.AddExpressionProperty("OrientationZ", "Entity.Heading");
            orientZProperty.DisplayName = "Z Rotation";
            orientZProperty.Description = "Rotation around Z axis in degrees (typically heading)";
            orientZProperty.CategoryName = "Transform";
        }

        public IStep CreateStep(IPropertyReaders propertyReaders)
        {
            return new SetObjectPositionOrientationStep(propertyReaders);
        }
    }
}
