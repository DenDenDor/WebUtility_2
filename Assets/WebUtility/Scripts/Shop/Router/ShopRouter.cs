using UnityEngine;
using System.Collections.Generic;
using WebUtility;

public class ShopRouter : IDIRouter
{
    [Inject] private DIContainer _container;

    public List<IPresenter> Init()
    {
        ShopPresenter shopPresenter = _container.RegisterSingleton<ShopPresenter>();

        return new List<IPresenter>()
        {
            shopPresenter
        };
    }
}