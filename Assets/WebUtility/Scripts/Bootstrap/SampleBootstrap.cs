using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WebUtility;
using UnityEngine;

public class SampleBootstrap : MonoBehaviour
{
    private List<IDIRouter> _routers = new List<IDIRouter>()
    {
        new UpdateRouter(),
        new ShopRouter()
    };
    
    private DIContainer _container;
    
    private void Awake()
    {
        _container = new DIContainer();
        
        _container.RegisterInstance(_container);

        RegisterSceneWindows();

        foreach (var router in _routers)
        {
            _container.InjectDependencies(router);
            
            List<IPresenter> presenters = router.Init();

            foreach (var presenter in presenters)
            {
                _container.InjectDependencies(presenter);
                
                presenter.Init();
            }
        }
    }

    private void RegisterSceneWindows()
    {
        AbstractWindowUi[] windows = FindObjectsByType<AbstractWindowUi>(FindObjectsSortMode.None);
        
        foreach (var window in windows)
        {
            if (window == null) 
               continue;
            
            _container.RegisterInstance<AbstractWindowUi>(window);
            
            var windowType = window.GetType();
            var registerMethod = typeof(DIContainer).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "RegisterInstance" && m.IsGenericMethod && m.GetParameters().Length == 2);
            
            if (registerMethod != null)
            {
                var genericMethod = registerMethod.MakeGenericMethod(windowType);
                genericMethod.Invoke(_container, new object[] { window, null });
            }
            
            _container.InjectDependencies(window);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
        }
    }
}
