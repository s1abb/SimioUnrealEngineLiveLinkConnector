using System;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    internal class DestroyObjectStep : IStep
    {
        private readonly IPropertyReaders _readers;

        public DestroyObjectStep(IPropertyReaders readers)
        {
            _readers = readers ?? throw new ArgumentNullException(nameof(readers));
        }

        public ExitType Execute(IStepExecutionContext context)
        {
            try
            {
                // Get the connector element and validate connection
                var connectorElement = ((IElementProperty)_readers.GetProperty("Element"))
                    .GetElement(context) as SimioUnrealEngineLiveLinkElement;

                if (connectorElement == null)
                {
                    context.ExecutionInformation.ReportError("Invalid or missing LiveLink element reference");
                    return ExitType.FirstExit;
                }

                // Get the object name to destroy
                string objectName = GetStringProperty("ObjectName", context);

                if (string.IsNullOrEmpty(objectName))
                {
                    context.ExecutionInformation.ReportError("Object name cannot be null or empty");
                    return ExitType.FirstExit;
                }

                // Remove the object through LiveLinkManager
                bool removed = LiveLinkManager.Instance.RemoveObject(objectName);
                
                if (removed)
                {
                    // 🆕 ADD TRACE INFORMATION - Currently missing!
                    context.ExecutionInformation.TraceInformation($"LiveLink object '{objectName}' destroyed successfully");
                }
                else
                {
                    // This is just a warning since the object might already be destroyed
                    context.ExecutionInformation.TraceInformation($"LiveLink object '{objectName}' was not found (may have already been destroyed)");
                }

                return ExitType.FirstExit;
            }
            catch (Exception ex)
            {
                context.ExecutionInformation.ReportError($"Unexpected error destroying object: {ex.Message}");
                return ExitType.FirstExit;
            }
        }

        private string GetStringProperty(string propertyName, IStepExecutionContext context)
        {
            try
            {
                var reader = (IExpressionPropertyReader)_readers.GetProperty(propertyName);
                var value = reader.GetExpressionValue(context);
                return value?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
