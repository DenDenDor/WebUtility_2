using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using WebUtility;

public class SDKAdapterPresenter : IPresenter
{
    [Inject] private DIContainer _container;

    private TypeSDK _currentSDKType;

    public void Init()
    {
        LoadSDKSelection();
        InitializeAdapter();
    }
    
    private void LoadSDKSelection()
    {
        TextAsset configFile = Resources.Load<TextAsset>("SDKConfig");
        if (configFile != null)
        {
            string content = configFile.text;
            if (System.Enum.TryParse(content, out TypeSDK loadedSDK))
            {
                _currentSDKType = loadedSDK;
                Debug.Log($"Loaded SDK type from config: {_currentSDKType}");
            }
        }
        else
        {
            Debug.LogWarning("SDK config file not found. Using default SDK type.");
        }
    }
    
    private void InitializeAdapter()
    {
        EnsureAllAdapterConfigsExist();
        
        var adapterName = _currentSDKType + "SDKAdapter";
        var adapters = DataConfigManager.GetAllDataOfType<AbstractSDKAdapter>();
        
        var selectedAdapter = adapters.FirstOrDefault(a => a.GetType().Name == adapterName);
        
        if (selectedAdapter == null)
        {
            var adapterType = FindAdapterType(adapterName);
            if (adapterType != null)
            {
                selectedAdapter = Activator.CreateInstance(adapterType) as AbstractSDKAdapter;
                Debug.Log($"Created adapter instance directly: {adapterName}");
            }
        }
        
        if (selectedAdapter != null)
        {
            _container.RegisterInstance<AbstractSDKAdapter>(selectedAdapter);
            selectedAdapter.Init();
            Debug.Log($"Initialized and registered {selectedAdapter.GetType().Name}");
        }
        else
        {
            Debug.LogError($"Adapter {adapterName} not found!");
        }
        
        _container.RegisterSingleton<SDKMediator>();
    }

    private void EnsureAllAdapterConfigsExist()
    {
        var sdkTypes = Enum.GetValues(typeof(TypeSDK)).Cast<TypeSDK>();
        var existingAdapters = DataConfigManager.GetAllDataOfType<AbstractSDKAdapter>();
        var existingAdapterNames = existingAdapters.Select(a => a.GetType().Name).ToHashSet();

        foreach (var sdkType in sdkTypes)
        {
            var adapterName = sdkType + "SDKAdapter";
            
            if (existingAdapterNames.Contains(adapterName))
                continue;

            var adapterType = FindAdapterType(adapterName);
            if (adapterType != null)
            {
                CreateAdapterConfig(adapterType, sdkType.ToString());
            }
            else
            {
                Debug.LogWarning($"Adapter type {adapterName} not found in assemblies");
            }
        }
    }

    private Type FindAdapterType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        if (type != null) return type;
        
        type = Type.GetType(typeName);
        if (type != null) return type;
        
        type = Type.GetType(typeName + ", Assembly-CSharp-firstpass");
        if (type != null) return type;
        
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
                
                if (!typeName.Contains("."))
                {
                    type = assembly.GetType($"UnityEngine.{typeName}");
                    if (type != null) return type;
                }
                
                Type[] types = assembly.GetTypes();
                foreach (Type t in types)
                {
                    if (t.Name == typeName && typeof(AbstractSDKAdapter).IsAssignableFrom(t))
                    {
                        return t;
                    }
                }
            }
            catch
            {
                continue;
            }
        }
        
        return null;
    }

    private void CreateAdapterConfig(Type adapterType, string configName)
    {
        const string ConfigsFolderPath = "Assets/WebUtility/Configs";
        
        try
        {
            string configsPath = Path.Combine(Application.dataPath, "WebUtility", "Configs");
            if (!Directory.Exists(configsPath))
            {
                Directory.CreateDirectory(configsPath);
            }

            object instance = Activator.CreateInstance(adapterType);
            string json = JsonUtility.ToJson(instance);
            
            var wrapper = new DataConfigManager.DataConfigWrapper
            {
                typeName = adapterType.Name,
                jsonData = json,
                name = configName,
                objectReferencesJson = ""
            };
            
            string configFileName = $"{adapterType.Name}_{configName}.json";
            string configPath = Path.Combine(configsPath, configFileName);
            
            if (File.Exists(configPath))
            {
                return;
            }
            
            string jsonContent = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(configPath, jsonContent);
            
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
            #endif
            
            Debug.Log($"Created missing config: {configFileName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to create config for {adapterType.Name}: {e.Message}");
        }
    }


    public void Exit()
    {
        
    }
}