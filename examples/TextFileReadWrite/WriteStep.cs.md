```c#
using System;
using System.Text;
using SimioAPI;
using SimioAPI.Extensions;

namespace CustomSimioStep
{
    class WriteStepDefinition : IStepDefinition
    {
        #region IStepDefinition Members

        /// <summary>
        /// Property returning the full name for this type of step. The name should contain no spaces. 
        /// </summary>
        public string Name
        {
            get { return "MyWriteText"; }
        }

        /// <summary>
        /// Property returning a short description of what the step does.  
        /// </summary>
        public string Description
        {
            get { return "The Write step may be used to write values to an output file. The user defined File element is used to specify the file."; }
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
        static readonly Guid MY_ID = new Guid("{37BE1266-3E23-4974-BA91-AF5BF7D72E19}"); //Jan2024/danH

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
            IPropertyDefinition pd;

            // Reference to the file to write to
            pd = schema.AddElementProperty("FileInfo", FileElementDefinition.MY_ID);

            // And a format specifier
            pd = schema.AddStringProperty("Format", String.Empty);
            pd.Description = "The format of the string to write out in C# string format syntax. Expressions defined in the 'Items' repeat group may " +
                "be included as data parameters in the formatted string using zero-based, sequentially numbered format characters within curly braces (e.g., the format character '{3}' indicates output of the fourth item in the 'Items' repeat group). " +
                "If this property is not specified, then a comma-delimited format of '{0},{1}, ,{N}' is assumed. Refer to a C# reference for more information on string format syntax in C#.";
            pd.Required = false;

            pd = schema.AddStringProperty("Delimiter", ",");
            pd.Description = "The delimiter added between each token when using the default format.";
            pd.Required = false;

            // A repeat group of values to write out
            IRepeatGroupPropertyDefinition parts = schema.AddRepeatGroupProperty("Items");
            parts.Description = "The expression items to be written out.";

            pd = parts.PropertyDefinitions.AddExpressionProperty("Expression", String.Empty);
            pd.Description = "Expression value to be written out.";
        }

        /// <summary>
        /// Method called to create a new instance of this step type to place in a process. 
        /// Returns an instance of the class implementing the IStep interface.
        /// </summary>
        public IStep CreateStep(IPropertyReaders properties)
        {
            return new WriteStep(properties);
        }

        #endregion
    }

    class WriteStep : IStep
    {
        // For efficiency, define and access the PropertyReaders in the constructor
        IPropertyReaders _properties;
        IElementProperty _prFileElement;
        IRepeatingPropertyReader _prItems;
        IPropertyReader _prFormat;
        IPropertyReader _prDelimiter;

        public WriteStep(IPropertyReaders properties)
        {
            _properties = properties;
            _prFileElement = (IElementProperty)_properties.GetProperty("FileInfo");
            _prItems = (IRepeatingPropertyReader)_properties.GetProperty("Items");
            _prFormat = _properties.GetProperty("Format");
            _prDelimiter = _properties.GetProperty("Delimiter");
        }

        #region IStep Members

        /// <summary>
        /// Method called when a process token executes the step.
        /// </summary>
        public ExitType Execute(IStepExecutionContext context)
        {
            // Get the file Element
            FileElement fileElement = (FileElement)_prFileElement.GetElement(context);
            if (fileElement == null)
            {
                context.ExecutionInformation.ReportError("FileInfo Element is null.  Makes sure FilePath is defined correctly.");
            }
            else
            {
                // Get an array of double values from the repeat group's list of expressions
                object[] paramsArray = new object[_prItems.GetCount(context)];
                for (int i = 0; i < _prItems.GetCount(context); i++)
                {
                    // The thing returned from GetRow is IDisposable, so we use the using() pattern here
                    using (IPropertyReaders row = _prItems.GetRow(i, context))
                    {
                        // Get the expression property
                        IExpressionPropertyReader expressionProp = row.GetProperty("Expression") as IExpressionPropertyReader;
                        // Resolve the expression to get the value
                        paramsArray[i] = expressionProp.GetExpressionValue(context);
                    }
                }

                string format = _prFormat.GetStringValue(context);
                string delimiter = _prDelimiter.GetStringValue(context);
                if (string.IsNullOrEmpty(delimiter))
                    delimiter = ",";
                // If the user didn't provide a format we will just make our own in the form {0},{1},{2},.. {n}
                // assuming in this case that the delimiter was a comma.
                if (String.IsNullOrEmpty(format))
                {
                    format = "";
                    var itemCount = _prItems.GetCount(context);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < itemCount; i++)
                    {
                        sb.Append($"{{{i}}}"); // e.g. "{3}"
                        if (i != itemCount - 1)
                            sb.Append(delimiter);
                    }
                    format = sb.ToString();
                }

                string writeOut;
                try
                {
                    writeOut = String.Format(format, paramsArray);
                }
                catch (FormatException)
                {
                    writeOut = null;
                    context.ExecutionInformation.ReportError($"Bad format=[{format}] provided in Write step.");
                }

                if (writeOut != null)
                {
                    // Write out the formatted line to the file
                    if (fileElement.Writer != null)
                    {
                        try
                        {
                            fileElement.Writer.WriteLine(writeOut);
                            IElement elmt = _prFileElement.GetElement(context);
                            string myPath = (elmt as FileElement).FilePath;
                            context.ExecutionInformation.TraceInformation($"Writing=[{writeOut}] to file={myPath}");
                        }
                        catch (Exception)
                        {
                            context.ExecutionInformation.ReportError("Error writing out information to file using Write step.");
                        }
                    }
                }
            }

            // We are done writing, have the token proceed out of the primary exit
            return ExitType.FirstExit;
        }

        #endregion
    }
}
```