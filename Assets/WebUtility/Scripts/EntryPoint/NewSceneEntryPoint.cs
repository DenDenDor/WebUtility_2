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
