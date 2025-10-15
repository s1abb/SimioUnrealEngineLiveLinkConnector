```c#
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using SimioAPI;
using SimioAPI.Extensions;

namespace SimioOmniverseConnector
{
	// Token: 0x02000002 RID: 2
	[NullableContext(1)]
	[Nullable(0)]
	public class CreatePrimStepDefinition : IStepDefinition
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public string Name
		{
			get
			{
				return "CreatePrim";
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000002 RID: 2 RVA: 0x00002057 File Offset: 0x00000257
		public string Description
		{
			get
			{
				return "Gets or creates a USD 'prim' inside a USD file referenced from a given OmniverseConnector element";
			}
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000003 RID: 3 RVA: 0x0000205E File Offset: 0x0000025E
		public Image Icon
		{
			get
			{
				return null;
			}
		}

		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000004 RID: 4 RVA: 0x00002061 File Offset: 0x00000261
		public Guid UniqueID
		{
			get
			{
				return CreatePrimStepDefinition.MY_ID;
			}
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000005 RID: 5 RVA: 0x00002068 File Offset: 0x00000268
		public int NumberOfExits
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x06000006 RID: 6 RVA: 0x0000206C File Offset: 0x0000026C
		public void DefineSchema(IPropertyDefinitions propertyDefinitions)
		{
			propertyDefinitions.AddElementProperty("OmniverseConnector", OmniverseElementDefinition.MY_ID).DisplayName = "Omniverse Connector";
			IPropertyDefinition propertyDefinition = propertyDefinitions.AddExpressionProperty("PrimName", string.Empty);
			propertyDefinition.Description = "The path of the prim to get or create. For example '/World/Object/Object1'";
			propertyDefinition.DisplayName = "Prim Name";
			IPropertyDefinition propertyDefinition2 = propertyDefinitions.AddExpressionProperty("MeshName", string.Empty);
			propertyDefinition2.Description = "The path of the pre-defined mesh to reference under the prim, and use for its visualization. For example '/World/Meshes/Wagon'";
			propertyDefinition2.DisplayName = "Mesh Name";
			propertyDefinitions.AddExpressionProperty("X", "0.0").Description = "Prim's new X position";
			propertyDefinitions.AddExpressionProperty("Y", "0.0").Description = "Prim's new Y position";
			propertyDefinitions.AddExpressionProperty("Z", "0.0").Description = "Prim's new Z position";
			propertyDefinitions.AddExpressionProperty("OrientationX", "0.0").Description = "Prim's new X rotation in degrees";
			propertyDefinitions.AddExpressionProperty("OrientationY", "0.0").Description = "Prim's new Y rotation in degrees";
			propertyDefinitions.AddExpressionProperty("OrientationZ", "0.0").Description = "Prim's new Z rotation in degrees";
		}

		// Token: 0x06000007 RID: 7 RVA: 0x00002179 File Offset: 0x00000379
		public IStep CreateStep(IPropertyReaders properties)
		{
			return new CreatePrimStep(properties);
		}

		// Token: 0x04000001 RID: 1
		private static readonly Guid MY_ID = new Guid("A60D87AC-05E0-4DDF-A458-90A8EC118585");
	}
}
```