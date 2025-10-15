```c#
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000004 RID: 4
	[NullableContext(1)]
	[Nullable(0)]
	public class DestroyPrimStepDefinition : IStepDefinition
	{
		// Token: 0x17000006 RID: 6
		// (get) Token: 0x0600000C RID: 12 RVA: 0x000023E0 File Offset: 0x000005E0
		public string Name
		{
			get
			{
				return "DestroyPrim";
			}
		}

		// Token: 0x17000007 RID: 7
		// (get) Token: 0x0600000D RID: 13 RVA: 0x000023E7 File Offset: 0x000005E7
		public string Description
		{
			get
			{
				return "Removes a USD 'prim' from a USD file referenced from a given OmniverseConnector element";
			}
		}

		// Token: 0x17000008 RID: 8
		// (get) Token: 0x0600000E RID: 14 RVA: 0x000023EE File Offset: 0x000005EE
		public Image Icon
		{
			get
			{
				return null;
			}
		}

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x0600000F RID: 15 RVA: 0x000023F1 File Offset: 0x000005F1
		public Guid UniqueID
		{
			get
			{
				return DestroyPrimStepDefinition.MY_ID;
			}
		}

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x06000010 RID: 16 RVA: 0x000023F8 File Offset: 0x000005F8
		public int NumberOfExits
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x06000011 RID: 17 RVA: 0x000023FC File Offset: 0x000005FC
		public void DefineSchema(IPropertyDefinitions propertyDefinitions)
		{
			propertyDefinitions.AddElementProperty("OmniverseConnector", OmniverseElementDefinition.MY_ID).DisplayName = "Omniverse Connector";
			IPropertyDefinition propertyDefinition = propertyDefinitions.AddExpressionProperty("PrimName", string.Empty);
			propertyDefinition.Description = "The path of the prim to get or create. For example '/World/Object/Object1'";
			propertyDefinition.DisplayName = "Prim Name";
		}

		// Token: 0x06000012 RID: 18 RVA: 0x00002448 File Offset: 0x00000648
		public IStep CreateStep(IPropertyReaders properties)
		{
			return new DestroyPrimStep(properties);
		}

		// Token: 0x04000003 RID: 3
		private static readonly Guid MY_ID = new Guid("9C86FF66-854C-4C74-98DF-C463D11744B2");
	}
}
```