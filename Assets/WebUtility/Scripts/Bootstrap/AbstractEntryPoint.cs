using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WebUtility
{
    public abstract class AbstractEntryPoint : MonoBehaviour
    {
        private DIContainer _container;
        
        protected abstract List<IDIRouter> Routers { get; }

        private void Awake()
        {
            _container = new DIContainer();
        
            _container.RegisterInstance(_container);

            RegisterSceneWindows();
            
            foreach (var router in Routers)
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
            
                _container.RegisterInstance(window);
            
                var windowType = window.GetType();
                var registerMethod = typeof(DIContainer).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "RegisterInstance" && m.IsGenericMethod && m.GetParameters().Length == 2);
            
                if (registerMethod != null)
                {
                    var genericMethod = registerMethod.MakeGenericMethod(windowType);
                    genericMethod.Invoke(_container, new object[] { window, null });
                }
                
                window.Init();
            
                _container.InjectDependencies(window);
            }
        }
    }
}