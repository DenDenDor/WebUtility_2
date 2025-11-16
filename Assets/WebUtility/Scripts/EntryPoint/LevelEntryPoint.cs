using UnityEngine;
using System.Collections.Generic;
using WebUtility;

public class LevelEntryPoint : AbstractEntryPoint
{
	public static readonly string SceneGUID = "5093348c5b7211e4c9dfb724249260c0";
	public static readonly string ScenePath = "Assets/WebUtility/Scenes/Level.unity";
	
	protected override List<IDIRouter> Routers => new List<IDIRouter>()
	{
		new SDKAdapterRouter(),
		new LevelRouter()
	};
}
