```c#
using SimioAPI;
using SimioAPI.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestStepAndElement
{
    internal class TestElementDefinition : IElementDefinition
    {
        #region IElementDefinition Members

        /// <summary>
        /// Property returning the full name for this type of element. The name should contain no spaces.
        /// </summary>
        public string Name
        {
            get { return "TestElement"; }
        }

        /// <summary>
        /// Property returning a short description of what the element does.
        /// </summary>
        public string Description
        {
            get { return "Description text for the 'TestElement' element."; }
        }

        /// <summary>
        /// Property returning an icon to display for the element in the UI.
        /// </summary>
        public System.Drawing.Image Icon
        {
            get { return null; }
        }

        /// <summary>
        /// Property returning a unique static GUID for the element.
        /// </summary>
        public Guid UniqueID
        {
            get { return MY_ID; }
        }
        public static readonly Guid MY_ID = new Guid("{dc57708f-032a-4a13-b5a0-74b593e7354b}");

        /// <summary>
        /// Method called that defines the property, state, and event schema for the element.
        /// </summary>
        public void DefineSchema(IElementSchema schema)
        {
            // Example of how to add a property definition to the element.
            IPropertyDefinition pd;
            pd = schema.PropertyDefinitions.AddExpressionProperty("MyExpression", "0.0");
            pd.DisplayName = "My Expression";
            pd.Description = "An expression property for this element.";
            pd.Required = true;

            // Example of how to add a state definition to the element.
            IStateDefinition sd;
            sd = schema.StateDefinitions.AddState("MyState");
            sd.Description = "A state owned by this element";

            // Example of how to add an event definition to the element.
            IEventDefinition ed;
            ed = schema.EventDefinitions.AddEvent("MyEvent");
            ed.Description = "An event owned by this element";
        }

        /// <summary>
        /// Method called to add a new instance of this element type to a model.
        /// Returns an instance of the class implementing the IElement interface.
        /// </summary>
        public IElement CreateElement(IElementData data)
        {
            return new TestElement(data);
        }

        #endregion
    }

    internal class TestElement : IElement
    {
        IElementData _data;

        public TestElement(IElementData data)
        {
            _data = data;
        }

        #region IElement Members

        /// <summary>
        /// Method called when the simulation run is initialized.
        /// </summary>
        public void Initialize()
        {
        }

        /// <summary>
        /// Method called when the simulation run is terminating.
        /// </summary>
        public void Shutdown()
        {
        }

        public string GetName()
        {
            return _data.HierarchicalDisplayName;
        }

        public double GetValue()
        {
            return _data.States[0].StateValue;
        }

        #endregion
    }
}
```