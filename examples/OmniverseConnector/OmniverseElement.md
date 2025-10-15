```c#
using System;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000009 RID: 9
	[NullableContext(1)]
	[Nullable(0)]
	public class OmniverseElement : IElement
	{
		// Token: 0x0600002A RID: 42 RVA: 0x0000293C File Offset: 0x00000B3C
		public OmniverseElement(IElementData elementData)
		{
			this._elementData = elementData;
			IPropertyReader usdLocationReader = elementData.Properties.GetProperty("USDLocation");
			this._usdLocation = usdLocationReader.GetStringValue(elementData.ExecutionContext);
			IPropertyReader liveSessionNameReader = elementData.Properties.GetProperty("LiveSessionName");
			this._liveSessionName = liveSessionNameReader.GetStringValue(elementData.ExecutionContext);
			double timeoutInSeconds = elementData.Properties.GetProperty("TimeoutInSeconds").GetDoubleValue(elementData.ExecutionContext);
			this._loadTimeout = TimeSpan.FromSeconds(timeoutInSeconds);
		}

		// Token: 0x17000014 RID: 20
		// (get) Token: 0x0600002B RID: 43 RVA: 0x000029C4 File Offset: 0x00000BC4
		[Nullable(2)]
		internal OmniverseMethods.UsdSafeHandle UsdSafeHandle
		{
			[NullableContext(2)]
			get
			{
				return this._usdSafeHandle;
			}
		}

		// Token: 0x0600002C RID: 44 RVA: 0x000029CC File Offset: 0x00000BCC
		public void Initialize()
		{
			if (string.IsNullOrWhiteSpace(this._usdLocation))
			{
				return;
			}
			if (this._loadTimeout.Ticks <= 0L)
			{
				this._elementData.ExecutionContext.ExecutionInformation.ReportError("Timeout must be a positive value");
			}
			this._usdSafeHandle = OmniverseMethods.LoadUSD(this._usdLocation, this._liveSessionName, this._loadTimeout);
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00002A2D File Offset: 0x00000C2D
		public void Shutdown()
		{
			OmniverseMethods.UsdSafeHandle usdSafeHandle = this._usdSafeHandle;
			if (usdSafeHandle != null)
			{
				usdSafeHandle.Dispose();
			}
			this._usdSafeHandle = null;
		}

		// Token: 0x04000008 RID: 8
		private readonly string _usdLocation;

		// Token: 0x04000009 RID: 9
		private readonly string _liveSessionName;

		// Token: 0x0400000A RID: 10
		private readonly TimeSpan _loadTimeout;

		// Token: 0x0400000B RID: 11
		private readonly IElementData _elementData;

		// Token: 0x0400000C RID: 12
		[Nullable(2)]
		private OmniverseMethods.UsdSafeHandle _usdSafeHandle;
	}
}
```