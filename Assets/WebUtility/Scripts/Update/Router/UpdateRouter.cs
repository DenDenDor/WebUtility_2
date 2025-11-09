using UnityEngine;
using System.Collections.Generic;

namespace WebUtility
{
    public class UpdateRouter : IDIRouter
    {
        [Inject] private DIContainer _container;

        public List<IPresenter> Init()
        {
            UpdatePresenter updatePresenter = _container.RegisterSingleton<UpdatePresenter>();

            return new List<IPresenter>()
            {
                updatePresenter
            };
        }
    }
}
