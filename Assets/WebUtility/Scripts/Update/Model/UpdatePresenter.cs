using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace WebUtility
{
    public class UpdatePresenter : IPresenter
    {
        [Inject] private readonly DIContainer _container;

        private readonly List<IUpdatable> _updatables = new();
        private readonly List<IFixedUpdatable> _fixedUpdatables = new();
        private readonly List<ILateUpdatable> _lateUpdatables = new();

        public void Init()
        {
            _container.OnInstanceRegistered += RegisterUpdatable;
            FindAndRegisterAllUpdateables();

            AutoUpdateManager autoUpdateManager =
                new GameObject("AutoUpdateManager").gameObject.AddComponent<AutoUpdateManager>();

            autoUpdateManager.Updated += OnUpdate;
            autoUpdateManager.FixedUpdated += OnFixedUpdate;
            autoUpdateManager.LateUpdated += OnLateUpdate;
        }

        private void OnUpdate()
        {
            foreach (var updatable in _updatables)
            {
                updatable.Update();
            }
        }

        private void OnFixedUpdate()
        {
            foreach (var updatable in _fixedUpdatables)
            {
                updatable.FixedUpdate();
            }
        }

        private void OnLateUpdate()
        {
            foreach (var updatable in _lateUpdatables)
            {
                updatable.LateUpdate();
            }
        }

        private List<Type> GetAllRegisteredTypes()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .ToList();
        }

        private void FindAndRegisterAllUpdateables()
        {
            var registeredTypes = GetAllRegisteredTypes();

            foreach (var type in registeredTypes)
            {
                if (typeof(IUpdatable).IsAssignableFrom(type))
                {
                    if (_container.TryResolve(type, out var updatable))
                    {
                        _updatables.Add((IUpdatable) updatable);
                    }
                }

                if (typeof(IFixedUpdatable).IsAssignableFrom(type))
                {
                    if (_container.TryResolve(type, out var fixedUpdatable))
                    {
                        _fixedUpdatables.Add((IFixedUpdatable) fixedUpdatable);
                    }
                }

                if (typeof(ILateUpdatable).IsAssignableFrom(type))
                {
                    if (_container.TryResolve(type, out var lateUpdatable))
                    {
                        _lateUpdatables.Add((ILateUpdatable) lateUpdatable);
                    }
                }
            }
        }

        private void RegisterUpdatable(object obj)
        {
            if (obj == null) return;

            if (obj is IUpdatable updatable && !_updatables.Contains(updatable))
            {
                _updatables.Add(updatable);
            }

            if (obj is IFixedUpdatable fixedUpdatable && !_fixedUpdatables.Contains(fixedUpdatable))
            {
                _fixedUpdatables.Add(fixedUpdatable);
            }

            if (obj is ILateUpdatable lateUpdatable && !_lateUpdatables.Contains(lateUpdatable))
            {
                _lateUpdatables.Add(lateUpdatable);
            }
        }

        public void Exit()
        {

        }
    }
}