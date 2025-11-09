using UnityEngine;
using System.Collections.Generic;
using WebUtility;

public class SDKAdapterRouter : IDIRouter
{
    [Inject] private DIContainer _container;

    public List<IPresenter> Init()
    {
        SDKAdapterPresenter sdkAdapterPresenter = _container.RegisterSingleton<SDKAdapterPresenter>();
        
        return new List<IPresenter>()
        {
            sdkAdapterPresenter
        };
    }
}