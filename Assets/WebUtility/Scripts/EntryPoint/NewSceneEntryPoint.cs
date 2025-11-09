// Auto-generated EntryPoint for scene
// SceneGUID: 1960564c578067a4fa14ebf29fddfa98
using UnityEngine;
using System.Collections.Generic;
using WebUtility;

public class NewSceneEntryPoint : AbstractEntryPoint
{
	protected override List<IDIRouter> Routers => new List<IDIRouter>()
	{
		new SDKAdapterRouter(),
		new UpdateRouter(),
		new ShopRouter()
	};
}
