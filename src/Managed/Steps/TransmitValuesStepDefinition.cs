using System;
using System.Drawing;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    internal class TransmitValuesStepDefinition : IStepDefinition
    {
        // Unique identifier for this step type
        private static readonly Guid MY_ID = new Guid("C2D3E4F5-A6B7-8901-2345-6789ABCDEF01");

        public string Name => "TransmitValues";
        public string Description => "Transmits custom data values to a LiveLink object in Unreal Engine";
        public Image Icon => null!;
        public Guid UniqueID => MY_ID;
        public int NumberOfExits => 1;

        public void DefineSchema(IPropertyDefinitions schema)
        {
            // Element reference property to constrain to LiveLink elements
            var elementProperty = schema.AddElementProperty("Element", SimioUnrealEngineLiveLinkElementDefinition.MY_ID);
            elementProperty.DisplayName = "LiveLink Element";
            elementProperty.Description = "The LiveLink element that owns the target object";
            elementProperty.CategoryName = "Element";
            elementProperty.Required = true;

            // Repeat group for data values
            var valuesGroup = schema.AddRepeatGroupProperty("Values");
            valuesGroup.DisplayName = "Data Values";
            valuesGroup.Description = "List of data values to transmit to the LiveLink object";
            valuesGroup.CategoryName = "Data";

            // Properties within the repeat group
            var nameProperty = valuesGroup.PropertyDefinitions.AddStringProperty("ValueName", "");
            nameProperty.DisplayName = "Value Name";
            nameProperty.Description = "Name of the data property in Unreal Engine";
            nameProperty.Required = true;

            var valueProperty = valuesGroup.PropertyDefinitions.AddExpressionProperty("ValueExpression", "0");
            valueProperty.DisplayName = "Value Expression";
            valueProperty.Description = "Simio expression that evaluates to the numeric value to transmit";
            valueProperty.Required = true;
        }

        public IStep CreateStep(IPropertyReaders propertyReaders)
        {
            return new TransmitValuesStep(propertyReaders);
        }
    }
}
