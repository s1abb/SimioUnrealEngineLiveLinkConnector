```c#
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000014 RID: 20
	[NullableContext(1)]
	[Nullable(0)]
	public class SetPrimPositionAndOrientationStepDefinition : IStepDefinition
	{
		// Token: 0x17000029 RID: 41
		// (get) Token: 0x06000097 RID: 151 RVA: 0x00003E20 File Offset: 0x00002020
		public string Name
		{
			get
			{
				return "SetPrimPositionAndOrientation";
			}
		}

		// Token: 0x1700002A RID: 42
		// (get) Token: 0x06000098 RID: 152 RVA: 0x00003E27 File Offset: 0x00002027
		public string Description
		{
			get
			{
				return "Sets a new position and orientation for a USD 'prim' inside a USD file referenced from a given OmniverseConnector element";
			}
		}

		// Token: 0x1700002B RID: 43
		// (get) Token: 0x06000099 RID: 153 RVA: 0x00003E2E File Offset: 0x0000202E
		public Image Icon
		{
			get
			{
				return null;
			}
		}

		// Token: 0x1700002C RID: 44
		// (get) Token: 0x0600009A RID: 154 RVA: 0x00003E31 File Offset: 0x00002031
		public Guid UniqueID
		{
			get
			{
				return SetPrimPositionAndOrientationStepDefinition.MY_ID;
			}
		}

		// Token: 0x1700002D RID: 45
		// (get) Token: 0x0600009B RID: 155 RVA: 0x00003E38 File Offset: 0x00002038
		public int NumberOfExits
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x0600009C RID: 156 RVA: 0x00003E3C File Offset: 0x0000203C
		public void DefineSchema(IPropertyDefinitions propertyDefinitions)
		{
			propertyDefinitions.AddElementProperty("OmniverseConnector", OmniverseElementDefinition.MY_ID).DisplayName = "Omniverse Connector";
			IPropertyDefinition propertyDefinition = propertyDefinitions.AddExpressionProperty("PrimName", string.Empty);
			propertyDefinition.Description = "The path of the prim to get or create. For example '/World/Object/Object1'";
			propertyDefinition.DisplayName = "Prim Name";
			propertyDefinitions.AddExpressionProperty("X", "0.0").Description = "Prim's new X position";
			propertyDefinitions.AddExpressionProperty("Y", "0.0").Description = "Prim's new Y position";
			propertyDefinitions.AddExpressionProperty("Z", "0.0").Description = "Prim's new Z position";
			propertyDefinitions.AddExpressionProperty("OrientationX", "0.0").Description = "Prim's new X rotation in degrees";
			propertyDefinitions.AddExpressionProperty("OrientationY", "0.0").Description = "Prim's new Y rotation in degrees";
			propertyDefinitions.AddExpressionProperty("OrientationZ", "0.0").Description = "Prim's new Z rotation in degrees";
		}

		// Token: 0x0600009D RID: 157 RVA: 0x00003F24 File Offset: 0x00002124
		public IStep CreateStep(IPropertyReaders properties)
		{
			return new SetPrimPositionAndOrientationStep(properties);
		}

		// Token: 0x04000016 RID: 22
		private static readonly Guid MY_ID = new Guid("F26470CF-1698-42FA-965A-EBB85BDBABC6");
	}
}
```