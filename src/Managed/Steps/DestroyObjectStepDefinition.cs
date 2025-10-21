using System;
using System.Drawing;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    internal class DestroyObjectStepDefinition : IStepDefinition
    {
        // Unique identifier for this step type
        private static readonly Guid MY_ID = new Guid("A1B2C3D4-E5F6-7890-1234-56789ABCDEF0");

        public string Name => "DestroyObject";
        public string Description => "Destroys a LiveLink object in the Unreal Engine editor.";
        public Image Icon => null!;
        public Guid UniqueID => MY_ID;
        public int NumberOfExits => 1;

        public void DefineSchema(IPropertyDefinitions schema)
        {
            // Element reference property to constrain to LiveLink elements
            var elementProperty = schema.AddElementProperty("Element", SimioUnrealEngineLiveLinkElementDefinition.MY_ID);
            elementProperty.DisplayName = "LiveLink Element";
            elementProperty.Description = "The LiveLink element that owns the object to destroy.";
            elementProperty.CategoryName = "Element";
            elementProperty.Required = true;

            // Object name property
            var objectNameProperty = schema.AddExpressionProperty("ObjectName", "Entity.Name");
            objectNameProperty.DisplayName = "Object Name";
            objectNameProperty.Description = "Name of the LiveLink object to destroy in Unreal Engine.";
            objectNameProperty.CategoryName = "Object";
            objectNameProperty.Required = true;
        }

        public IStep CreateStep(IPropertyReaders propertyReaders)
        {
            return new DestroyObjectStep(propertyReaders);
        }
    }
}
