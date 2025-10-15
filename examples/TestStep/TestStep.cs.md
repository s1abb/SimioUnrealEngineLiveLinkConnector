```c#
using SimioAPI;
using SimioAPI.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Xml.Linq;

namespace TestStepAndElement
{
    internal class TestStepDefinition : IStepDefinition
    {
        #region IStepDefinition Members

        /// <summary>
        /// Property returning the full name for this type of step. The name should contain no spaces.
        /// </summary>
        public string Name
        {
            get { return "TestStep"; }
        }

        /// <summary>
        /// Property returning a short description of what the step does.
        /// </summary>
        public string Description
        {
            get { return "Description text for the 'TestStep' step."; }
        }

        /// <summary>
        /// Property returning an icon to display for the step in the UI.
        /// </summary>
        public System.Drawing.Image Icon
        {
            get { return null; }
        }

        /// <summary>
        /// Property returning a unique static GUID for the step.
        /// </summary>
        public Guid UniqueID
        {
            get { return MY_ID; }
        }
        static readonly Guid MY_ID = new Guid("{2b2194b0-5ea7-4e63-ae52-5080c125e18b}");

        /// <summary>
        /// Property returning the number of exits out of the step. Can return either 1 or 2.
        /// </summary>
        public int NumberOfExits
        {
            get { return 1; }
        }

        /// <summary>
        /// Method called that defines the property schema for the step.
        /// </summary>
        public void DefineSchema(IPropertyDefinitions schema)
        {
            // Example of how to add a property definition to the step.
            IPropertyDefinition pd;
            pd = schema.AddElementProperty("TestElement", TestElementDefinition.MY_ID);
            pd = schema.AddStateProperty("ResponseValue");
        }

        /// <summary>
        /// Method called to create a new instance of this step type to place in a process.
        /// Returns an instance of the class implementing the IStep interface.
        /// </summary>
        public IStep CreateStep(IPropertyReaders properties)
        {
            return new TestStep(properties);
        }

        #endregion
    }

    internal class TestStep : IStep
    {
        IPropertyReaders _properties;
        IElementProperty _testElement;
        IPropertyReader _responseValue;

        public TestStep(IPropertyReaders properties)
        {
            _properties = properties;
            _testElement = (IElementProperty)_properties.GetProperty("TestElement");
            _responseValue = (IPropertyReader)_properties.GetProperty("ResponseValue");
        }

        #region IStep Members

        /// <summary>
        /// Method called when a process token executes the step.
        /// </summary>
        public ExitType Execute(IStepExecutionContext context)
        {
            TestElement testElement = (TestElement)_testElement.GetElement(context);            
            double realResponse = 0.0;
            realResponse = testElement.GetValue();
            IStateProperty responseStateProp = (IStateProperty)_responseValue;
            IState responseState = responseStateProp.GetState(context);
            IRealState responseRealState = responseState as IRealState;
            responseRealState.Value = realResponse;

            // Example of how to display a trace line for the step.
            context.ExecutionInformation.TraceInformation(String.Format("The value for '{0}' is '{1}'.", testElement.GetName(), realResponse.ToString()));

            return ExitType.FirstExit;
        }

        #endregion
    }
}
```