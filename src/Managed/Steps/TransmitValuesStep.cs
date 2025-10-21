using System;
using System.Collections.Generic;
using System.Linq;
using SimioAPI;
using SimioAPI.Extensions;
using SimioUnrealEngineLiveLinkConnector.Element;
using SimioUnrealEngineLiveLinkConnector.UnrealIntegration;

namespace SimioUnrealEngineLiveLinkConnector.Steps
{
    internal class TransmitValuesStep : IStep
    {
        private readonly IPropertyReaders _readers;
        private readonly IRepeatingPropertyReader _prValues;
        private DateTime? _lastTraceTime = null; // 🆕 Add loop protection for high-frequency tracing

        public TransmitValuesStep(IPropertyReaders readers)
        {
            _readers = readers ?? throw new ArgumentNullException(nameof(readers));
            _prValues = (IRepeatingPropertyReader)_readers.GetProperty("Values");
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

                // Get values from the repeat group
                var dataValues = new Dictionary<string, double>();
                int numValues = _prValues.GetCount(context);

                for (int i = 0; i < numValues; i++)
                {
                    using (IPropertyReaders valueRow = _prValues.GetRow(i, context))
                    {
                        // Get the value name
                        var nameReader = valueRow.GetProperty("ValueName");
                        string valueName = nameReader?.GetStringValue(context) ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(valueName))
                        {
                            context.ExecutionInformation.ReportError($"Value name at row {i + 1} is empty or invalid");
                            continue;
                        }

                        // Get the value expression
                        var valueReader = valueRow.GetProperty("ValueExpression") as IExpressionPropertyReader;
                        try
                        {
                            var valueResult = valueReader?.GetExpressionValue(context);
                            if (valueResult != null && double.TryParse(valueResult.ToString(), out double numericValue))
                            {
                                dataValues[valueName] = numericValue;
                            }
                            else
                            {
                                context.ExecutionInformation.ReportError($"Value expression at row {i + 1} did not evaluate to a numeric value");
                            }
                        }
                        catch (Exception ex)
                        {
                            context.ExecutionInformation.ReportError($"Error evaluating value expression at row {i + 1}: {ex.Message}");
                        }
                    }
                }

                // Transmit the collected values to all managed LiveLink objects
                // Since we don't have a specific object name, we'll transmit to all registered objects
                if (dataValues.Count > 0)
                {
                    var manager = LiveLinkManager.Instance;
                    var objectNames = manager.ObjectNames.ToList();
                    
                    if (objectNames.Count > 0)
                    {
                        foreach (string objectName in objectNames)
                        {
                            var objectUpdater = manager.GetObject(objectName);
                            if (objectUpdater != null)
                            {
                                try
                                {
                                    objectUpdater.TransmitData(dataValues);
                                }
                                catch (Exception ex)
                                {
                                    context.ExecutionInformation.ReportError($"Error transmitting to object '{objectName}': {ex.Message}");
                                }
                            }
                        }

                        // 🆕 Loop protection: trace max once per second for high-frequency steps
                        if (!_lastTraceTime.HasValue || (DateTime.Now - _lastTraceTime.Value).TotalSeconds >= 1.0)
                        {
                            context.ExecutionInformation.TraceInformation($"LiveLink data transmitted to {objectNames.Count} objects with {dataValues.Count} values.");
                            _lastTraceTime = DateTime.Now;
                        }
                    }
                    else
                    {
                        context.ExecutionInformation.ReportError("No LiveLink objects found. Create objects first using CreateObject step.");
                        return ExitType.FirstExit;
                    }
                }
                else
                {
                    context.ExecutionInformation.ReportError("No valid data values to transmit");
                    return ExitType.FirstExit;
                }

                return ExitType.FirstExit;
            }
            catch (Exception ex)
            {
                context.ExecutionInformation.ReportError($"Unexpected error transmitting values: {ex.Message}");
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
