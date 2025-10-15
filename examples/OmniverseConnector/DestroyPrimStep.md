```c#

using System;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000005 RID: 5
	[NullableContext(1)]
	[Nullable(0)]
	internal class DestroyPrimStep : IStep
	{
		// Token: 0x06000015 RID: 21 RVA: 0x00002469 File Offset: 0x00000669
		public DestroyPrimStep(IPropertyReaders readers)
		{
			this._readers = readers;
		}

		// Token: 0x06000016 RID: 22 RVA: 0x00002478 File Offset: 0x00000678
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
			OmniverseMethods.DestroyPrim(connectorElement.UsdSafeHandle, primName);
			return 1;
		}

		// Token: 0x04000004 RID: 4
		private readonly IPropertyReaders _readers;
	}
}
```