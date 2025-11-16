using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WebUtility;
using UnityEngine;

namespace WebUtility
{
    public class DIContainer
    {
        private readonly Dictionary<Type, Dictionary<string, Func<object>>> _services = new();
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly Dictionary<Type, Dictionary<string, Lifetime>> _lifetimes = new();
        private const string DEFAULT_KEY = "";
        
        public event Action<object> OnInstanceRegistered;

        public enum Lifetime
        {
            Transient,
            Singleton
        }

        public void Register<TInterface, TImplementation>(Lifetime lifetime = Lifetime.Transient, string key = null)
            where TImplementation : class, TInterface
        {
            Register(typeof(TInterface), typeof(TImplementation), lifetime, key);
        }

        public void Register<TInterface>(Func<TInterface> factory, Lifetime lifetime = Lifetime.Transient, string key = null)
        {
            var type = typeof(TInterface);
            key = key ?? DEFAULT_KEY;
            EnsureKeyDictionary(type, key);

            if (lifetime == Lifetime.Singleton)
            {
                var singleton = factory();
                _singletons[GetSingletonKey(type, key)] = singleton;
                _services[type][key] = () => singleton;
            }
            else
            {
                _services[type][key] = () => factory();
            }

            _lifetimes[type][key] = lifetime;
        }

        public void RegisterInstance<TInterface>(TInterface instance, string key = null)
        {
            var type = typeof(TInterface);
            key = key ?? DEFAULT_KEY;
            EnsureKeyDictionary(type, key);
            _singletons[GetSingletonKey(type, key)] = instance;
            _services[type][key] = () => instance;
            _lifetimes[type][key] = Lifetime.Singleton;
            
            OnInstanceRegistered?.Invoke(instance);
        }

        public T Resolve<T>(string key = null)
        {
            return (T)Resolve(typeof(T), key);
        }

        public bool TryResolve<T>(out T service, string key = null)
        {
            service = default(T);
            if (TryResolve(typeof(T), out var obj, key))
            {
                service = (T)obj;
                return true;
            }
            return false;
        }

        public object Resolve(Type type, string key = null)
        {
            if (TryResolve(type, out var service, key))
            {
                return service;
            }

            throw new InvalidOperationException($"Service of type {type.Name} with key '{key ?? "null"}' is not registered.");
        }

        public bool TryResolve(Type type, out object service, string key = null)
        {
            service = null;

            key = key ?? DEFAULT_KEY;

            if (!_services.ContainsKey(type))
            {
                return false;
            }

            if (!_services[type].ContainsKey(key))
            {
                return false;
            }

            service = _services[type][key]();
            return true;
        }

        public void InjectDependencies(object target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            var type = target.GetType();

            InjectConstructor(target, type);

            InjectFields(target, type);

            InjectProperties(target, type);
        }

        public T CreateInstance<T>() where T : class
        {
            return (T)CreateInstance(typeof(T));
        }

        public object CreateInstance(Type type)
        {
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            ConstructorInfo constructor = null;

            foreach (var ctor in constructors)
            {
                if (ctor.GetCustomAttribute<InjectAttribute>() != null)
                {
                    constructor = ctor;
                    break;
                }
            }

            if (constructor == null)
            {
                constructor = constructors
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();
            }

            if (constructor == null)
            {
                return Activator.CreateInstance(type);
            }

            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var injectAttr = param.GetCustomAttribute<InjectAttribute>();
                var key = injectAttr?.Key;

                if (!TryResolve(param.ParameterType, out var dependency, key))
                {
                    throw new InvalidOperationException(
                        $"Cannot resolve parameter '{param.Name}' of type '{param.ParameterType.Name}' in constructor of '{type.Name}'");
                }

                args[i] = dependency;
            }

            var instance = constructor.Invoke(args);
            InjectDependencies(instance);
            return instance;
        }

        private void InjectConstructor(object target, Type type)
        {
        }

        private void InjectFields(object target, Type type)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var injectAttr = field.GetCustomAttribute<InjectAttribute>();
                if (injectAttr == null)
                    continue;

                if (field.GetValue(target) != null)
                    continue;

                var fieldType = field.FieldType;
                var key = injectAttr.Key;

                if (TryResolve(fieldType, out var dependency, key))
                {
                    field.SetValue(target, dependency);
                }
                else
                {
                    Debug.LogWarning($"Cannot resolve field '{field.Name}' of type '{fieldType.Name}' in '{type.Name}'");
                }
            }
        }

        private void InjectProperties(object target, Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var injectAttr = property.GetCustomAttribute<InjectAttribute>();
                if (injectAttr == null)
                    continue;

                if (!property.CanWrite)
                    continue;

                if (property.GetValue(target) != null)
                    continue;

                var propertyType = property.PropertyType;
                var key = injectAttr.Key;

                if (TryResolve(propertyType, out var dependency, key))
                {
                    property.SetValue(target, dependency);
                }
                else
                {
                    Debug.LogWarning($"Cannot resolve property '{property.Name}' of type '{propertyType.Name}' in '{type.Name}'");
                }
            }
        }

        private void Register(Type interfaceType, Type implementationType, Lifetime lifetime, string key)
        {
            key = key ?? DEFAULT_KEY;
            EnsureKeyDictionary(interfaceType, key);

            if (lifetime == Lifetime.Singleton)
            {
                var singletonKey = GetSingletonKey(interfaceType, key);
                if (_singletons.ContainsKey(singletonKey))
                {
                    _services[interfaceType][key] = () => _singletons[singletonKey];
                }
                else
                {
                    _services[interfaceType][key] = () =>
                    {
                        if (!_singletons.ContainsKey(singletonKey))
                        {
                            var instance = CreateInstance(implementationType);
                            _singletons[singletonKey] = instance;
                            OnInstanceRegistered?.Invoke(instance);
                            return instance;
                        }
                        return _singletons[singletonKey];
                    };
                }
            }
            else
            {
                _services[interfaceType][key] = () => CreateInstance(implementationType);
            }

            _lifetimes[interfaceType][key] = lifetime;
        }

        private void EnsureKeyDictionary(Type type, string key)
        {
            key = key ?? DEFAULT_KEY;
            
            if (!_services.ContainsKey(type))
            {
                _services[type] = new Dictionary<string, Func<object>>();
            }

            if (!_services[type].ContainsKey(key))
            {
                _services[type][key] = null;
            }

            if (!_lifetimes.ContainsKey(type))
            {
                _lifetimes[type] = new Dictionary<string, Lifetime>();
            }

            if (!_lifetimes[type].ContainsKey(key))
            {
                _lifetimes[type][key] = Lifetime.Transient;
            }
        }

        private Type GetSingletonKey(Type type, string key)
        {
            key = key ?? DEFAULT_KEY;
            return type;
        }
    }
}

public static class DIContainerExtensions
{
    public static T RegisterSingleton<T>(this DIContainer container) where T : class
    {
        container.Register<T, T>(DIContainer.Lifetime.Singleton);

        return container.Resolve<T>();
    }
    
    public static void CreateAllSingletons<T1, T2>(this DIContainer container)
    {
        container.Resolve<T1>();
        container.Resolve<T2>();
    }
}