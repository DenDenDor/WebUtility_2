using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using Object = UnityEngine.Object;

namespace WebUtility.Editor.Data
{
    public class DataConfigEditorWindow : EditorWindow
    {
        private const string ConfigsFolderPath = "Assets/WebUtility/Configs";
        private const string ConfigsIndexPath = "Assets/WebUtility/Configs/index.json";
        
        private Vector2 _scrollPosition;
        private Vector2 _configListScroll;
        private DataConfigWrapper _selectedConfig;
        private Object _selectedConfigInstance;
        private string _newConfigName = "";
        private int _selectedTypeIndex = 0;
        private string[] _availableTypes;
        private Type[] _availableTypeObjects;
        private bool _needsRefresh = true;

        [MenuItem("Tools/Data Config Editor", priority = -100)]
        public static void ShowWindow()
        {
            GetWindow<DataConfigEditorWindow>("Data Config Editor");
        }

        private void OnEnable()
        {
            RefreshAvailableTypes();
            LoadConfigs();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            
            DrawConfigList();
            
            DrawConfigEditor();
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            
            EditorGUILayout.LabelField("Configs", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Refresh"))
            {
                RefreshAvailableTypes();
                LoadConfigs();
                Repaint();
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Create New Config", EditorStyles.boldLabel);
            _newConfigName = EditorGUILayout.TextField("Name:", _newConfigName);
            
            if (_availableTypes != null && _availableTypes.Length > 0)
            {
                _selectedTypeIndex = EditorGUILayout.Popup("Type:", _selectedTypeIndex, _availableTypes);
                
                if (GUILayout.Button("Create Config"))
                {
                    CreateNewConfig();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No AbstractData types found. Create a class that inherits from AbstractData with [Serializable] attribute.", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Existing Configs", EditorStyles.boldLabel);
            
            _configListScroll = EditorGUILayout.BeginScrollView(_configListScroll);
            
            var configs = GetAllConfigs();
            
            if (configs.Count == 0)
            {
                EditorGUILayout.HelpBox("No configs found. Create a new config.", MessageType.Info);
            }
            
            foreach (var config in configs)
            {
                EditorGUILayout.BeginHorizontal();
                
                bool isSelected = _selectedConfig != null && _selectedConfig.TypeName == config.TypeName && _selectedConfig.Name == config.Name;
                if (GUILayout.Button(config.Name, isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft))
                {
                    SelectConfig(config);
                }
                
                if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
                {
                    if (EditorUtility.DisplayDialog("Delete Config", $"Delete config '{config.Name}'?", "Yes", "No"))
                    {
                        DeleteConfig(config);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.EndVertical();
        }

        private void DrawConfigEditor()
        {
            EditorGUILayout.BeginVertical();
            
            if (_selectedConfig == null)
            {
                EditorGUILayout.HelpBox("Select a config to edit", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            EditorGUILayout.LabelField("Edit Config", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Name:", _selectedConfig.Name);
            if (EditorGUI.EndChangeCheck() && newName != _selectedConfig.Name)
            {
                if (ConfigNameExists(_selectedConfig.TypeName, newName))
                {
                    EditorUtility.DisplayDialog("Error", $"Config with name '{newName}' already exists for type {_selectedConfig.TypeName}. Please choose a different name.", "OK");
                }
                else
                {
                    string oldName = _selectedConfig.Name;
                    _selectedConfig.UpdateName(newName);
                    SaveConfig(_selectedConfig);
                    
                    Type configType = _availableTypeObjects?.FirstOrDefault(t => t.Name == _selectedConfig.TypeName);
                    if (configType != null)
                    {
                        ConfigEnumGenerator.UpdateEnumForType(configType, newName);
                    }
                    
                    RenameConfigFile(oldName, newName, _selectedConfig.TypeName);
                }
            }
            
            EditorGUILayout.LabelField("Type:", _selectedConfig.TypeName);
            
            EditorGUILayout.Space();
            
            if (_selectedConfigInstance != null)
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                
                EditorGUI.BeginChangeCheck();
                bool hasChanges = false;
                
                if (_selectedConfigInstance is ConfigDataHolder holder)
                {
                    object data = holder.GetData();
                    Type dataType = holder.DataType;
                    
                    if (data != null && dataType != null)
                    {
                        ConfigFieldEditor.DrawFields(data, dataType);
                        
                        hasChanges = EditorGUI.EndChangeCheck();
                        
                        if (hasChanges)
                        {
                            holder.SetData(data, dataType);
                            SaveConfig(_selectedConfig);
                        }
                    }
                    else
                    {
                        EditorGUI.EndChangeCheck();
                        EditorGUILayout.HelpBox("Failed to load config data", MessageType.Error);
                    }
                }
                else
                {
                    EditorGUI.EndChangeCheck();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space();
                if (GUILayout.Button("Save", GUILayout.Height(30)))
                {
                    SaveConfig(_selectedConfig);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Failed to load config instance", MessageType.Error);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void RefreshAvailableTypes()
        {
            var types = new List<Type>();
            var typeNames = new List<string>();
            
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;
                    
                    if (typeof(AbstractData).IsAssignableFrom(type))
                    {
                        if (type.GetCustomAttribute<SerializableAttribute>() != null || 
                            type.IsSerializable)
                        {
                            types.Add(type);
                            typeNames.Add(type.Name);
                        }
                    }
                }
            }
            
            _availableTypeObjects = types.ToArray();
            _availableTypes = typeNames.ToArray();
        }

        private void CreateNewConfig()
        {
            if (string.IsNullOrEmpty(_newConfigName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a name for the config", "OK");
                return;
            }
            
            if (_availableTypeObjects == null || _selectedTypeIndex >= _availableTypeObjects.Length)
            {
                EditorUtility.DisplayDialog("Error", "Invalid type selected", "OK");
                return;
            }
            
            Type selectedType = _availableTypeObjects[_selectedTypeIndex];
            
            if (ConfigNameExists(selectedType.Name, _newConfigName))
            {
                EditorUtility.DisplayDialog("Error", $"Config with name '{_newConfigName}' already exists for type {selectedType.Name}. Please choose a different name.", "OK");
                return;
            }
            
            object instance = Activator.CreateInstance(selectedType);
            string json = JsonUtility.ToJson(instance);
            
            var wrapper = new DataConfigWrapper(selectedType.Name, json, _newConfigName);
            
            SaveConfig(wrapper);
            AddConfigToIndex(wrapper);
            
            ConfigEnumGenerator.UpdateEnumForType(selectedType, _newConfigName);
            
            SelectConfig(wrapper);
            
            string createdName = _newConfigName;
            _newConfigName = "";
            
            Repaint();
            
            Debug.Log($"Created new config: {createdName} ({selectedType.Name})");
        }
        
        private bool ConfigNameExists(string typeName, string configName)
        {
            var configs = GetAllConfigs();
            return configs.Any(c => c.TypeName == typeName && c.Name == configName);
        }

        private void SelectConfig(DataConfigWrapper config)
        {
            _selectedConfig = config;
            
            try
            {
                Type configType = _availableTypeObjects.FirstOrDefault(t => t.Name == config.TypeName);
                if (configType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        configType = assembly.GetType(config.TypeName);
                        if (configType != null) break;
                        
                        var fullName = assembly.GetTypes().FirstOrDefault(t => t.Name == config.TypeName);
                        if (fullName != null)
                        {
                            configType = fullName;
                            break;
                        }
                    }
                }
                
                if (configType == null)
                {
                    Debug.LogError($"Type {config.TypeName} not found");
                    _selectedConfigInstance = null;
                    return;
                }
                
                object instance = JsonUtility.FromJson(config.JsonData, configType);
                
                if (!string.IsNullOrEmpty(config.ObjectReferencesJson))
                {
                    try
                    {
                        var references = JsonUtility.FromJson<ConfigObjectReferences>(config.ObjectReferencesJson);
                        if (references != null && references.references != null)
                        {
                            RestoreUnityObjectReferences(instance, configType, references);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to restore Unity object references: {e.Message}");
                    }
                }
                
                var so = ScriptableObject.CreateInstance<ConfigDataHolder>();
                so.SetData(instance, configType);
                _selectedConfigInstance = so;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load config: {e.Message}");
                _selectedConfigInstance = null;
            }
        }

        private void DeleteConfig(DataConfigWrapper config)
        {
            string configFileName = SanitizeFileName($"{config.TypeName}_{config.Name}.json");
            string configPath = Path.Combine(ConfigsFolderPath, configFileName);
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
                AssetDatabase.DeleteAsset(configPath);
            }
            
            RemoveConfigFromIndex(config);
            
            Type configType = _availableTypeObjects?.FirstOrDefault(t => t.Name == config.TypeName);
            if (configType != null)
            {
                ConfigEnumGenerator.UpdateEnumForType(configType, config.Name, isRemoved: true);
            }
            
            if (_selectedConfig == config)
            {
                _selectedConfig = null;
                _selectedConfigInstance = null;
            }
            
            AssetDatabase.Refresh();
        }

        private void SaveConfig(DataConfigWrapper config)
        {
            if (!Directory.Exists(ConfigsFolderPath))
            {
                Directory.CreateDirectory(ConfigsFolderPath);
            }
            
            if (_selectedConfigInstance != null)
            {
                if (_selectedConfigInstance is ConfigDataHolder holder)
                {
                    object data = holder.GetData();
                    
                    if (data != null)
                    {
                        var objectReferences = new ConfigObjectReferences();
                        CollectUnityObjectReferences(data, holder.DataType, "", objectReferences);
                        
                        object dataCopy = CreateSerializableCopy(data, holder.DataType);
                        
                        string json = JsonUtility.ToJson(dataCopy);
                        config.UpdateJsonData(json);
                        
                        if (objectReferences.references.Count > 0)
                        {
                            string referencesJson = JsonUtility.ToJson(objectReferences);
                            config.UpdateObjectReferences(referencesJson);
                        }
                        else
                        {
                            config.UpdateObjectReferences("");
                        }
                    }
                }
            }
            
            string configFileName = SanitizeFileName($"{config.TypeName}_{config.Name}.json");
            string configPath = Path.Combine(ConfigsFolderPath, configFileName);
            string jsonContent = JsonUtility.ToJson(config, true);
            
            try
            {
                File.WriteAllText(configPath, jsonContent);
                Debug.Log($"Config file saved to: {configPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save config file: {e.Message}");
                return;
            }
            
            AddConfigToIndex(config);
            
            AssetDatabase.Refresh();
            
            AddConfigToAddressables(configPath, config.Name, config.TypeName);
            
            Debug.Log($"Config saved: {config.Name} ({config.TypeName}) at {configPath}");
        }

        private object CreateSerializableCopy(object source, Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                return source;
            }
            
            if (type.IsValueType)
            {
                return source;
            }
            
            if (type.GetConstructor(Type.EmptyTypes) == null && !type.IsValueType)
            {
                Debug.LogWarning($"Type {type.Name} doesn't have a default constructor. Using original object.");
                return source;
            }
            
            object copy = Activator.CreateInstance(type);
            
            FieldInfo[] allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fields = new List<FieldInfo>();
            
            foreach (var field in allFields)
            {
                if (field.IsPublic)
                {
                    if (field.GetCustomAttribute<System.NonSerializedAttribute>() == null)
                    {
                        fields.Add(field);
                    }
                }
                else if (field.GetCustomAttribute<SerializeField>() != null)
                {
                    fields.Add(field);
                }
            }
            
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<System.NonSerializedAttribute>() != null)
                    continue;
                
                Type fieldType = field.FieldType;
                object fieldValue = field.GetValue(source);
                
                if (typeof(MonoBehaviour).IsAssignableFrom(fieldType) || 
                    typeof(Component).IsAssignableFrom(fieldType))
                {
                    field.SetValue(copy, null);
                }
                else if (typeof(GameObject).IsAssignableFrom(fieldType))
                {
                    field.SetValue(copy, null);
                }
                else if (fieldType.IsClass && fieldType.GetCustomAttribute<SerializableAttribute>() != null)
                {
                    if (fieldValue != null)
                    {
                        object nestedCopy = CreateSerializableCopy(fieldValue, fieldType);
                        field.SetValue(copy, nestedCopy);
                    }
                }
                else if (fieldType.IsArray)
                {
                    if (fieldValue != null)
                    {
                        Array sourceArray = fieldValue as Array;
                        Array newArray = Array.CreateInstance(fieldType.GetElementType(), sourceArray.Length);
                        for (int i = 0; i < sourceArray.Length; i++)
                        {
                            object element = sourceArray.GetValue(i);
                            if (element != null && !element.GetType().IsPrimitive && element.GetType() != typeof(string))
                            {
                                element = CreateSerializableCopy(element, element.GetType());
                            }
                            newArray.SetValue(element, i);
                        }
                        field.SetValue(copy, newArray);
                    }
                }
                else
                {
                    field.SetValue(copy, fieldValue);
                }
            }
            
            return copy;
        }


        private List<DataConfigWrapper> GetAllConfigs()
        {
            var configs = new List<DataConfigWrapper>();
            
            if (!Directory.Exists(ConfigsFolderPath))
            {
                Debug.Log($"Configs folder does not exist: {ConfigsFolderPath}");
                return configs;
            }
            
            if (!File.Exists(ConfigsIndexPath))
            {
                Debug.Log($"Index file does not exist: {ConfigsIndexPath}. Searching for config files...");
                
                try
                {
                    string[] files = Directory.GetFiles(ConfigsFolderPath, "*.json");
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(file) == "index.json")
                            continue;
                        
                        try
                        {
                            string configJson = File.ReadAllText(file);
                            var config = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                            if (config != null)
                            {
                                configs.Add(config);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Failed to load config from {file}: {e.Message}");
                        }
                    }
                    
                    if (configs.Count > 0)
                    {
                        EditorApplication.delayCall += () => SaveConfigsIndex();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to search for config files: {e.Message}");
                }
                
                return configs;
            }
            
            try
            {
                string json = File.ReadAllText(ConfigsIndexPath);
                var index = JsonUtility.FromJson<ConfigsIndex>(json);
                
                if (index != null && index.configs != null)
                {
                    Debug.Log($"Loading {index.configs.Count} configs from index");
                    
                    foreach (var configGuid in index.configs)
                    {
                        string configPath = Path.Combine(ConfigsFolderPath, $"{configGuid}.json");
                        if (File.Exists(configPath))
                        {
                            try
                            {
                                string configJson = File.ReadAllText(configPath);
                                var config = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                                if (config != null)
                                {
                                    configs.Add(config);
                                }
                                else
                                {
                                    Debug.LogWarning($"Failed to deserialize config from {configPath}");
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Failed to read config file {configPath}: {e.Message}");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Config file not found: {configPath}");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Index file is empty or invalid");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load configs: {e.Message}\nStackTrace: {e.StackTrace}");
            }
            
            Debug.Log($"Loaded {configs.Count} configs");
            return configs;
        }

        private void CollectUnityObjectReferences(object data, Type type, string fieldPath, ConfigObjectReferences references)
        {
            if (data == null || type == null)
                return;
            
            FieldInfo[] allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var field in allFields)
            {
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                    continue;
                
                if (field.GetCustomAttribute<System.NonSerializedAttribute>() != null)
                    continue;
                
                Type fieldType = field.FieldType;
                object fieldValue = field.GetValue(data);
                
                if (fieldValue == null)
                    continue;
                
                string currentPath = string.IsNullOrEmpty(fieldPath) ? field.Name : $"{fieldPath}.{field.Name}";
                
                if (fieldValue is UnityEngine.Object unityObj)
                {
                    string assetPath = AssetDatabase.GetAssetPath(unityObj);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        if (assetPath.Contains("unity_builtin_extra") || assetPath.Contains("Library/unity default resources"))
                        {
                            Debug.LogWarning($"Cannot save reference to built-in Unity resource: {assetPath}. Use a regular asset instead.");
                            continue;
                        }
                        
                        string guid = AssetDatabase.AssetPathToGUID(assetPath);
                        if (!string.IsNullOrEmpty(guid))
                        {
                            string address = GetAddressableAddress(assetPath);
                            
                            if (string.IsNullOrEmpty(address))
                            {
                                AddUnityObjectToAddressables(assetPath, guid);
                                address = GetAddressableAddress(assetPath);
                            }
                            
                            references.references.Add(new ObjectReferenceData(
                                currentPath,
                                guid,
                                assetPath,
                                fieldType.AssemblyQualifiedName,
                                address
                            ));
                        }
                    }
                    else if (unityObj is MonoBehaviour mb)
                    {
                        string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(mb);
                        if (!string.IsNullOrEmpty(prefabPath))
                        {
                            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                            if (!string.IsNullOrEmpty(guid))
                            {
                                string address = GetAddressableAddress(prefabPath);
                                
                                if (string.IsNullOrEmpty(address))
                                {
                                    AddUnityObjectToAddressables(prefabPath, guid);
                                    address = GetAddressableAddress(prefabPath);
                                }
                                
                                references.references.Add(new ObjectReferenceData(
                                    currentPath,
                                    guid,
                                    prefabPath,
                                    fieldType.AssemblyQualifiedName,
                                    address
                                ));
                            }
                        }
                    }
                }
                else if (fieldType.IsClass && fieldType.GetCustomAttribute<SerializableAttribute>() != null)
                {
                    CollectUnityObjectReferences(fieldValue, fieldType, currentPath, references);
                }
                else if (fieldType.IsArray)
                {
                    Array array = fieldValue as Array;
                    if (array != null)
                    {
                        Type elementType = fieldType.GetElementType();
                        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                        {
                            for (int i = 0; i < array.Length; i++)
                            {
                                object element = array.GetValue(i);
                                if (element is UnityEngine.Object obj)
                                {
                                    string assetPath = AssetDatabase.GetAssetPath(obj);
                                    if (!string.IsNullOrEmpty(assetPath))
                                    {
                                        if (assetPath.Contains("unity_builtin_extra") || assetPath.Contains("Library/unity default resources"))
                                        {
                                            continue;
                                        }
                                        
                                        string guid = AssetDatabase.AssetPathToGUID(assetPath);
                                        if (!string.IsNullOrEmpty(guid))
                                        {
                                            string address = GetAddressableAddress(assetPath);
                                            
                                            if (string.IsNullOrEmpty(address))
                                            {
                                                AddUnityObjectToAddressables(assetPath, guid);
                                                address = GetAddressableAddress(assetPath);
                                            }
                                            
                                            references.references.Add(new ObjectReferenceData(
                                                $"{currentPath}[{i}]",
                                                guid,
                                                assetPath,
                                                elementType.AssemblyQualifiedName,
                                                address
                                            ));
                                        }
                                    }
                                }
                            }
                        }
                        else if (elementType.IsClass && elementType.GetCustomAttribute<SerializableAttribute>() != null)
                        {
                            for (int i = 0; i < array.Length; i++)
                            {
                                object element = array.GetValue(i);
                                if (element != null)
                                {
                                    CollectUnityObjectReferences(element, elementType, $"{currentPath}[{i}]", references);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RestoreUnityObjectReferences(object data, Type type, ConfigObjectReferences references)
        {
            if (data == null || type == null || references == null || references.references == null)
                return;
            
            foreach (var refData in references.references)
            {
                try
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(refData.objectGuid);
                    if (string.IsNullOrEmpty(assetPath) && !string.IsNullOrEmpty(refData.assetPath))
                    {
                        assetPath = refData.assetPath;
                    }
                    
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        Type objectType = Type.GetType(refData.objectType);
                        if (objectType != null)
                        {
                            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(assetPath, objectType);
                            if (obj != null)
                            {
                                SetFieldValueByPath(data, type, refData.fieldPath, obj);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to restore reference {refData.fieldPath}: {e.Message}");
                }
            }
        }

        private string GetAddressableAddress(string assetPath)
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                    return null;
                
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                    return null;
                
                var entry = settings.FindAssetEntry(guid);
                return entry?.address;
            }
            catch
            {
                return null;
            }
        }

        private void AddUnityObjectToAddressables(string assetPath, string guid)
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    Debug.LogWarning("AddressableAssetSettings not found. Cannot add Unity object to Addressables.");
                    return;
                }

                var existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null)
                    return;

                const string UnityObjectsGroupName = "UnityObjects";
                var group = settings.FindGroup(UnityObjectsGroupName);
                if (group == null)
                {
                    group = settings.CreateGroup(UnityObjectsGroupName, false, false, false, null, typeof(BundledAssetGroupSchema));
                    if (group != null)
                    {
                        group.AddSchema<ContentUpdateGroupSchema>();
                    }
                }

                if (group == null)
                    return;

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                if (entry != null)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                    entry.address = fileName;
                    
                    entry.SetLabel("UnityObject", true, true);
                    
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to add Unity object to Addressables: {e.Message}");
            }
        }

        private void SetFieldValueByPath(object obj, Type type, string fieldPath, object value)
        {
            if (string.IsNullOrEmpty(fieldPath))
                return;
            
            string[] parts = fieldPath.Split('.');
            object currentObj = obj;
            Type currentType = type;
            
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string part = parts[i];
                
                if (part.Contains("["))
                {
                    int bracketIndex = part.IndexOf('[');
                    string fieldName = part.Substring(0, bracketIndex);
                    string indexStr = part.Substring(bracketIndex + 1, part.IndexOf(']') - bracketIndex - 1);
                    int index = int.Parse(indexStr);
                    
                    FieldInfo field = currentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        Array array = field.GetValue(currentObj) as Array;
                        if (array != null && index < array.Length)
                        {
                            currentObj = array.GetValue(index);
                            currentType = field.FieldType.GetElementType();
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    FieldInfo field = currentType.GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        currentObj = field.GetValue(currentObj);
                        currentType = field.FieldType;
                    }
                    else
                    {
                        return;
                    }
                }
                
                if (currentObj == null)
                    return;
            }
            
            string finalPart = parts[parts.Length - 1];
            
            if (finalPart.Contains("["))
            {
                int bracketIndex = finalPart.IndexOf('[');
                string fieldName = finalPart.Substring(0, bracketIndex);
                string indexStr = finalPart.Substring(bracketIndex + 1, finalPart.IndexOf(']') - bracketIndex - 1);
                int index = int.Parse(indexStr);
                
                FieldInfo field = currentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    Array array = field.GetValue(currentObj) as Array;
                    if (array != null && index < array.Length)
                    {
                        array.SetValue(value, index);
                    }
                }
            }
            else
            {
                FieldInfo field = currentType.GetField(finalPart, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(currentObj, value);
                }
            }
        }

        private void LoadConfigs()
        {
            _needsRefresh = false;
        }

        private string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = fileName;
            foreach (char c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }
        
        private void RenameConfigFile(string oldName, string newName, string typeName)
        {
            string oldFileName = SanitizeFileName($"{typeName}_{oldName}.json");
            string newFileName = SanitizeFileName($"{typeName}_{newName}.json");
            string oldPath = Path.Combine(ConfigsFolderPath, oldFileName);
            string newPath = Path.Combine(ConfigsFolderPath, newFileName);
            
            if (File.Exists(oldPath))
            {
                File.Move(oldPath, newPath);
                AssetDatabase.MoveAsset(oldPath, newPath);
                AssetDatabase.Refresh();
            }
        }
        
        private void AddConfigToAddressables(string configPath, string configName, string typeName)
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null)
                {
                    Debug.LogWarning("AddressableAssetSettings not found. Create Addressables settings first.");
                    return;
                }

                string assetGuid = AssetDatabase.AssetPathToGUID(configPath);
                if (string.IsNullOrEmpty(assetGuid))
                {
                    Debug.LogWarning($"Failed to get GUID for config: {configPath}");
                    return;
                }

                const string AddressableGroupName = "Configs";
                const string AddressLabel = "Config";
                const string AddressPrefix = "Configs/";

                var group = settings.FindGroup(AddressableGroupName);
                if (group == null)
                {
                    group = settings.CreateGroup(AddressableGroupName, false, false, false, null, typeof(BundledAssetGroupSchema));
                    if (group != null)
                    {
                        group.AddSchema<ContentUpdateGroupSchema>();
                    }
                }

                var entry = settings.FindAssetEntry(assetGuid);
                if (entry == null)
                {
                    entry = settings.CreateOrMoveEntry(assetGuid, group, readOnly: false, postEvent: false);
                }
                else if (entry.parentGroup != group)
                {
                    settings.MoveEntry(entry, group);
                }

                string address = $"{AddressPrefix}{typeName}_{configName}";
                if (entry.address != address)
                {
                    entry.address = address;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssets();
                }

                entry.SetLabel(AddressLabel, true, true);

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to add config to Addressables: {e.Message}");
            }
        }

        private void AddConfigToIndex(DataConfigWrapper config)
        {
            ConfigsIndex index;
            
            if (File.Exists(ConfigsIndexPath))
            {
                string json = File.ReadAllText(ConfigsIndexPath);
                index = JsonUtility.FromJson<ConfigsIndex>(json);
            }
            else
            {
                index = new ConfigsIndex();
            }
            
            if (index == null)
                index = new ConfigsIndex();
            
            if (index.configs == null)
                index.configs = new List<string>();
            
            string configKey = $"{config.TypeName}_{config.Name}";
            if (!index.configs.Contains(configKey))
            {
                index.configs.Add(configKey);
            }
            
            SaveConfigsIndex();
        }

        private void RemoveConfigFromIndex(DataConfigWrapper config)
        {
            if (!File.Exists(ConfigsIndexPath))
                return;
            
            string json = File.ReadAllText(ConfigsIndexPath);
            var index = JsonUtility.FromJson<ConfigsIndex>(json);
            
            if (index != null && index.configs != null)
            {
                string configKey = $"{config.TypeName}_{config.Name}";
                index.configs.Remove(configKey);
                SaveConfigsIndex();
            }
        }

        private void SaveConfigsIndex()
        {
            if (!Directory.Exists(ConfigsFolderPath))
            {
                Directory.CreateDirectory(ConfigsFolderPath);
                Debug.Log($"Created configs folder: {ConfigsFolderPath}");
            }
            
            var configs = new List<DataConfigWrapper>();
            try
            {
                if (Directory.Exists(ConfigsFolderPath))
                {
                    string[] files = Directory.GetFiles(ConfigsFolderPath, "*.json");
                    foreach (var file in files)
                    {
                        if (Path.GetFileName(file) == "index.json")
                            continue;
                        
                        try
                        {
                            string configJson = File.ReadAllText(file);
                            var config = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                            if (config != null)
                            {
                                configs.Add(config);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to scan config files: {e.Message}");
            }
            
            var index = new ConfigsIndex
            {
                configs = configs.Select(c => $"{c.TypeName}_{c.Name}").Distinct().ToList()
            };
            
            string json = JsonUtility.ToJson(index, true);
            
            try
            {
                File.WriteAllText(ConfigsIndexPath, json);
                Debug.Log($"Index saved with {index.configs.Count} configs");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save index: {e.Message}");
            }
            
            AssetDatabase.Refresh();
        }

        [Serializable]
        private class ConfigsIndex
        {
            public List<string> configs;

            public ConfigsIndex()
            {
                configs = new List<string>();
            }
        }
    }

    public class ConfigDataHolder : ScriptableObject
    {
        [SerializeField] private string dataJson;
        [SerializeField] private string dataType;
        [SerializeField] private string dataAssemblyQualifiedName;
        
        [SerializeField] private UnityEngine.Object data;
        
        private object _cachedData;
        private Type _cachedType;

        public Type DataType => _cachedType;

        public void SetData(object dataObj, Type type)
        {
            _cachedData = dataObj;
            _cachedType = type;
            dataType = type.Name;
            dataAssemblyQualifiedName = type.AssemblyQualifiedName;
            
            if (dataObj is UnityEngine.Object unityObj)
            {
                data = unityObj;
            }
            else
            {
                dataJson = JsonUtility.ToJson(dataObj);
            }
        }

        public object GetData()
        {
            if (_cachedData != null)
                return _cachedData;
            
            if (data != null)
            {
                _cachedData = data;
                return data;
            }
            
            if (string.IsNullOrEmpty(dataAssemblyQualifiedName) || string.IsNullOrEmpty(dataJson))
                return null;
            
            Type type = Type.GetType(dataAssemblyQualifiedName);
            if (type != null)
            {
                _cachedData = JsonUtility.FromJson(dataJson, type);
                _cachedType = type;
            }
            
            return _cachedData;
        }
    }
}

