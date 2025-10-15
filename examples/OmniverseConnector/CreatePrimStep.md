```c#

using System;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000003 RID: 3
	[NullableContext(1)]
	[Nullable(0)]
	internal class CreatePrimStep : IStep
	{
		// Token: 0x0600000A RID: 10 RVA: 0x0000219A File Offset: 0x0000039A
		public CreatePrimStep(IPropertyReaders readers)
		{
			this._readers = readers;
		}

		// Token: 0x0600000B RID: 11 RVA: 0x000021AC File Offset: 0x000003AC
		public ExitType Execute(IStepExecutionContext context)
		{
			OmniverseElement connectorElement = ((IElementProperty)this._readers.GetProperty("OmniverseConnector")).GetElement(context) as OmniverseElement;
			if (connectorElement == null)
			{
				context.ExecutionInformation.ReportError("Failed to resolve 'Omniverse Connector' element reference");
				return 1;
			}
			if (connectorElement.UsdSafeHandle == null)
			{
				context.ExecutionInformation.ReportError("Omniverse Connector element is not in a valid state");
				return 1;
			}
			object expressionValue = ((IExpressionPropertyReader)this._readers.GetProperty("PrimName")).GetExpressionValue(context);
			string primName = ((expressionValue != null) ? expressionValue.ToString() : null) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(primName))
			{
				context.ExecutionInformation.ReportError("Did not provide a prim name");
				return 1;
			}
			object expressionValue2 = ((IExpressionPropertyReader)this._readers.GetProperty("MeshName")).GetExpressionValue(context);
			string meshName = ((expressionValue2 != null) ? expressionValue2.ToString() : null) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(meshName))
			{
				context.ExecutionInformation.ReportError("Did not provide a mesh name");
				return 1;
			}
			double xvalue;
			double yvalue;
			double zvalue;
			try
			{
				xvalue = (double)((IExpressionPropertyReader)this._readers.GetProperty("X")).GetExpressionValue(context);
				yvalue = (double)((IExpressionPropertyReader)this._readers.GetProperty("Y")).GetExpressionValue(context);
				zvalue = (double)((IExpressionPropertyReader)this._readers.GetProperty("Z")).GetExpressionValue(context);
			}
			catch
			{
				context.ExecutionInformation.ReportError("Failed to get the x,y,z coordinates");
				return 1;
			}
			double xorientvalue;
			double yorientvalue;
			double zorientvalue;
			try
			{
				xorientvalue = (double)((IExpressionPropertyReader)this._readers.GetProperty("OrientationX")).GetExpressionValue(context);
				yorientvalue = (double)((IExpressionPropertyReader)this._readers.GetProperty("OrientationY")).GetExpressionValue(context);
				zorientvalue = (double)((IExpressionPropertyReader)this._readers.GetProperty("OrientationZ")).GetExpressionValue(context);
			}
			catch
			{
				context.ExecutionInformation.ReportError("Failed to get the x,y,z orientation");
				return 1;
			}
			OmniverseMethods.CreatePrim(connectorElement.UsdSafeHandle, primName, meshName, (float)xvalue, (float)yvalue, (float)zvalue, (float)xorientvalue, (float)yorientvalue, (float)zorientvalue);
			return 1;
		}

		// Token: 0x04000002 RID: 2
		private readonly IPropertyReaders _readers;
	}
}
```