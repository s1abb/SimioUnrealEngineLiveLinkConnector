using System;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    /// <summary>
    /// Step that updates an object's position and orientation in Unreal Engine LiveLink.
    /// Called frequently during simulation to stream real-time transform updates.
    /// </summary>
    [NullableContext(1)]
    [Nullable(0)]
    internal class SetObjectPositionOrientationStep : IStep
    {
        private readonly IPropertyReaders _readers;

        public SetObjectPositionOrientationStep(IPropertyReaders readers)
        {
            _readers = readers ?? throw new ArgumentNullException(nameof(readers));
        }

        public ExitType Execute(IStepExecutionContext context)
        {
            try
            {
                // Get the connector element and validate connection
                var connectorElement = ((IElementProperty)_readers.GetProperty("UnrealEngineConnector"))
                    .GetElement(context) as SimioUnrealEngineLiveLinkElement;

                if (connectorElement == null)
                {
                    context.ExecutionInformation.ReportError("Failed to resolve 'Unreal Engine Connector' element reference");
                    return ExitType.FirstExit;
                }

                if (!connectorElement.IsConnectionHealthy)
                {
                    context.ExecutionInformation.ReportError("Unreal Engine Connector element is not in a healthy state");
                    return ExitType.FirstExit;
                }

                // Read object name
                string objectName = GetStringProperty("ObjectName", context);
                if (string.IsNullOrWhiteSpace(objectName))
                {
                    context.ExecutionInformation.ReportError("Object name must not be empty");
                    return ExitType.FirstExit;
                }

                // Read position coordinates
                double x, y, z;
                if (!TryGetDoubleProperty("X", context, out x) ||
                    !TryGetDoubleProperty("Y", context, out y) ||
                    !TryGetDoubleProperty("Z", context, out z))
                {
                    context.ExecutionInformation.ReportError("Failed to read position coordinates (X, Y, Z)");
                    return ExitType.FirstExit;
                }

                // Read orientation values
                double orientX, orientY, orientZ;
                if (!TryGetDoubleProperty("OrientationX", context, out orientX) ||
                    !TryGetDoubleProperty("OrientationY", context, out orientY) ||
                    !TryGetDoubleProperty("OrientationZ", context, out orientZ))
                {
                    context.ExecutionInformation.ReportError("Failed to read orientation values (OrientationX, OrientationY, OrientationZ)");
                    return ExitType.FirstExit;
                }

                // Get or create the object updater and update transform
                // Note: GetOrCreateObject handles both new objects and existing ones
                var objectUpdater = LiveLinkManager.Instance.GetOrCreateObject(objectName);
                objectUpdater.UpdateTransform(x, y, z, orientX, orientY, orientZ);

                return ExitType.FirstExit;
            }
            catch (Exception ex)
            {
                context.ExecutionInformation.ReportError($"Unexpected error updating object position: {ex.Message}");
                return ExitType.FirstExit;
            }
        }

        /// <summary>
        /// Helper method to safely read string properties from expressions.
        /// </summary>
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

        /// <summary>
        /// Helper method to safely read double properties from expressions.
        /// </summary>
        private bool TryGetDoubleProperty(string propertyName, IStepExecutionContext context, out double value)
        {
            try
            {
                var reader = (IExpressionPropertyReader)_readers.GetProperty(propertyName);
                var expressionValue = reader.GetExpressionValue(context);
                
                if (expressionValue is double doubleValue)
                {
                    value = doubleValue;
                    return true;
                }
                
                if (double.TryParse(expressionValue?.ToString(), out double parsedValue))
                {
                    value = parsedValue;
                    return true;
                }
                
                value = 0.0;
                return false;
            }
            catch
            {
                value = 0.0;
                return false;
            }
        }
    }
}
