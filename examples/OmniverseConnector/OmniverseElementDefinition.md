```c#
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000008 RID: 8
	[NullableContext(1)]
	[Nullable(0)]
	public class OmniverseElementDefinition : IElementDefinition
	{
		// Token: 0x06000022 RID: 34 RVA: 0x0000286B File Offset: 0x00000A6B
		static OmniverseElementDefinition()
		{
			OmniverseMethods.SetupEnvironmentForOmniverse();
		}

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x06000023 RID: 35 RVA: 0x00002881 File Offset: 0x00000A81
		public string Name
		{
			get
			{
				return "OmniverseConnector";
			}
		}

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x06000024 RID: 36 RVA: 0x00002888 File Offset: 0x00000A88
		public string Description
		{
			get
			{
				return "Element that allows a connection to a USD stage on disk or in an Omniverse Nucleus server";
			}
		}

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x06000025 RID: 37 RVA: 0x0000288F File Offset: 0x00000A8F
		public Image Icon
		{
			get
			{
				return null;
			}
		}

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x06000026 RID: 38 RVA: 0x00002892 File Offset: 0x00000A92
		public Guid UniqueID
		{
			get
			{
				return OmniverseElementDefinition.MY_ID;
			}
		}

		// Token: 0x06000027 RID: 39 RVA: 0x0000289C File Offset: 0x00000A9C
		public void DefineSchema(IElementSchema schema)
		{
			IPropertyDefinition propertyDefinition = schema.PropertyDefinitions.AddStringProperty("USDLocation", string.Empty);
			propertyDefinition.Description = "The location of the USD file on disk or in an Omniverse Nucleus server.";
			propertyDefinition.DisplayName = "USD Location";
			IPropertyDefinition propertyDefinition2 = schema.PropertyDefinitions.AddStringProperty("LiveSessionName", string.Empty);
			propertyDefinition2.Description = "The name of a live session defined in an Omniverse Nucleus server for the given USD file.";
			propertyDefinition2.DisplayName = "Live Session Name";
			IPropertyDefinition propertyDefinition3 = schema.PropertyDefinitions.AddRealProperty("TimeoutInSeconds", 5.0);
			propertyDefinition3.Description = "The amount of time to retry connecting to an Omniverse Nucleus server before giving up.";
			propertyDefinition3.DisplayName = "Timeout In Seconds";
		}

		// Token: 0x06000028 RID: 40 RVA: 0x0000292B File Offset: 0x00000B2B
		public IElement CreateElement(IElementData data)
		{
			return new OmniverseElement(data);
		}

		// Token: 0x04000007 RID: 7
		internal static readonly Guid MY_ID = new Guid("1BCF6CF6-A033-4A6A-95E7-1A819DA22DB4");
	}
}
```