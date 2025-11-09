using UnityEngine;
using System.Collections.Generic;
using WebUtility;

public class LevelRouter : IDIRouter
{
    [Inject] private DIContainer _container;

    public List<IPresenter> Init()
    {
        LevelPresenter levelPresenter = _container.RegisterSingleton<LevelPresenter>();
        
        return new List<IPresenter>()
        {
            levelPresenter
        };
    }
}