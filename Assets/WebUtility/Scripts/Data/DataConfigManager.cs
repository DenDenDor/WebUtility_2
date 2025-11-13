using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace WebUtility
{
    /// <summary>
    /// Runtime менеджер для работы с конфигами данных через Addressables.
    /// Конфиги должны быть отмечены как Addressable с адресом вида "Configs/&lt;TypeName&gt;_&lt;ConfigName&gt;"
    /// и меткой "Config" (см. ConfigAddressableSetup в редакторе).
    /// </summary>
    public static class DataConfigManager
    {
        private const string ConfigAddressPrefix = "Configs/";
        private const string ConfigLabel = "Config";

        private static bool _addressablesInitialized;

        /// <summary>
        /// Получить данные конфига по enum значению.
        /// </summary>
        public static T GetData<T>(Enum configType) where T : AbstractData
        {
            if (configType == null)
            {
                Debug.LogError("Config type enum cannot be null");
                return null;
            }

            Type type = typeof(T);
            string configName = configType.ToString();
            return (T)GetData(type, configName);
        }

        /// <summary>
        /// Получить данные конфига по типу и имени.
        /// </summary>
        private static object GetData(Type type, string configName)
        {
            if (type == null)
            {
                Debug.LogError("Type cannot be null");
                return null;
            }

            if (string.IsNullOrEmpty(configName))
            {
                Debug.LogError("Config name cannot be null or empty");
                return null;
            }

            if (!typeof(AbstractData).IsAssignableFrom(type))
            {
                Debug.LogError($"Type {type.Name} must inherit from AbstractData");
                return null;
            }

            EnsureAddressablesInitialized();

            string address = BuildAddress(type.Name, configName);
            if (!TryLocateTextAsset(address, out _))
            {
                Debug.LogWarning($"Config address not found: {address}. Ensure configs are marked as Addressable via Tools/Addressables/Update Config Entries.");
                return null;
            }

            TextAsset configAsset = LoadConfigAsset(address);
            if (configAsset == null)
                return null;

            try
            {
                var wrapper = JsonUtility.FromJson<DataConfigWrapper>(configAsset.text);
                if (wrapper == null)
                {
                    Debug.LogError($"Failed to deserialize config wrapper for {configName}");
                    return null;
                }

                if (wrapper.TypeName != type.Name)
                {
                    Debug.LogWarning($"Config type mismatch. Expected {type.Name}, but got {wrapper.TypeName}");
                }

                object data = JsonUtility.FromJson(wrapper.JsonData, type);
                if (data == null)
                {
                    Debug.LogError($"Failed to deserialize data of type {type.Name} from config {configName}");
                    return null;
                }

                if (!string.IsNullOrEmpty(wrapper.ObjectReferencesJson))
                {
                    RestoreUnityObjectReferences(data, type, wrapper.ObjectReferencesJson);
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load config {configName}: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Проверить существование конфига по enum значению.
        /// </summary>
        public static bool ConfigExists<T>(Enum configType) where T : AbstractData
        {
            if (configType == null)
                return false;

            Type type = typeof(T);
            string configName = configType.ToString();
            EnsureAddressablesInitialized();
            string address = BuildAddress(type.Name, configName);
            return TryLocateTextAsset(address, out _);
        }

        #region Addressables helpers

        private static void EnsureAddressablesInitialized()
        {
            if (_addressablesInitialized)
                return;

            try
            {
                var handle = Addressables.InitializeAsync();
                handle.WaitForCompletion();
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
                _addressablesInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize Addressables: {e.Message}");
            }
        }

        private static bool TryLocateTextAsset(string address, out IList<IResourceLocation> locations)
        {
            locations = null;
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(address, typeof(TextAsset), out locations))
                {
                    return true;
                }
            }

            return false;
        }

        private static TextAsset LoadConfigAsset(string address)
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>(address);

            try
            {
                TextAsset asset = handle.WaitForCompletion();
                if (asset == null)
                {
                    Debug.LogWarning($"Addressable config asset not found: {address}");
                }
                return asset;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load Addressable config asset {address}: {e.Message}");
                return null;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static IList<IResourceLocation> LoadAllConfigLocations()
        {
            var handle = Addressables.LoadResourceLocationsAsync(ConfigLabel, typeof(TextAsset));
            try
            {
                return handle.WaitForCompletion();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load config locations: {e.Message}");
                return null;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static string BuildAddress(string typeName, string configName)
        {
            return $"{ConfigAddressPrefix}{typeName}_{configName}";
        }

        #endregion

        #region Unity object references restoration

        private static void RestoreUnityObjectReferences(object data, Type type, string referencesJson)
        {
            if (data == null || type == null || string.IsNullOrEmpty(referencesJson))
                return;

            try
            {
                var references = JsonUtility.FromJson<ConfigObjectReferences>(referencesJson);
                if (references == null || references.references == null)
                    return;

                foreach (var refData in references.references)
                {
                    try
                    {
                        UnityEngine.Object obj = LoadUnityObject(refData);
                        if (obj != null)
                        {
                            SetFieldValueByPath(data, type, refData.fieldPath, obj);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to restore reference {refData.fieldPath}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse Unity object references: {e.Message}");
            }
        }

        private static UnityEngine.Object LoadUnityObject(ObjectReferenceData reference)
        {
            if (reference == null)
                return null;

            EnsureAddressablesInitialized();

            // Пытаемся загрузить по адресу Addressables (если объект помечен как Addressable)
            if (!string.IsNullOrEmpty(reference.address))
            {
                var obj = LoadUnityObjectByAddress(reference.address);
                if (obj != null)
                    return obj;
            }

            // Пробуем загрузить по сохранённому пути через Addressables
            if (!string.IsNullOrEmpty(reference.assetPath))
            {
                // Пропускаем встроенные ресурсы Unity (Resources/unity_builtin_extra)
                if (reference.assetPath.Contains("unity_builtin_extra"))
                {
                    Debug.LogWarning($"Cannot load built-in Unity resource: {reference.assetPath}. This resource is not available in runtime.");
                    return null;
                }
                
                var obj = LoadUnityObjectByAddress(reference.assetPath);
                if (obj != null)
                    return obj;
            }

            // Попытка загрузить по GUID через Addressables
            if (!string.IsNullOrEmpty(reference.objectGuid))
            {
                // Пропускаем специальные GUID встроенных ресурсов
                if (reference.objectGuid == "0000000000000000f000000000000000")
                {
                    Debug.LogWarning($"Cannot load built-in Unity resource by GUID. This resource is not available in runtime.");
                    return null;
                }
                
                var obj = LoadUnityObjectByAddress(reference.objectGuid);
                if (obj != null)
                    return obj;
            }

            // Если не удалось загрузить через Addressables, пробуем через Resources (fallback)
            if (!string.IsNullOrEmpty(reference.assetPath))
            {
                return LoadUnityObjectFromResources(reference.assetPath, reference.objectType);
            }

            return null;
        }

        private static UnityEngine.Object LoadUnityObjectByAddress(string address)
        {
            // Проверяем, существует ли адрес в Addressables
            IList<IResourceLocation> locations;
            if (!TryLocateUnityObject(address, out locations))
            {
                return null;
            }

            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(address);
            try
            {
                return handle.WaitForCompletion();
            }
            catch (Exception e)
            {
                // Игнорируем ошибки InvalidKeyException - это значит объект не в Addressables
                if (e.GetType().Name != "InvalidKeyException")
                {
                    Debug.LogWarning($"Failed to load Unity object by address {address}: {e.Message}");
                }
                return null;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static bool TryLocateUnityObject(string address, out IList<IResourceLocation> locations)
        {
            locations = null;
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(address, typeof(UnityEngine.Object), out locations))
                {
                    return true;
                }
            }

            return false;
        }

        private static UnityEngine.Object LoadUnityObjectFromResources(string assetPath, string objectTypeName)
        {
            try
            {
                // Пытаемся загрузить из Resources
                // Убираем "Assets/" и расширение файла
                string resourcesPath = assetPath;
                if (resourcesPath.StartsWith("Assets/"))
                {
                    resourcesPath = resourcesPath.Substring(7);
                }
                
                // Убираем расширение
                int lastDot = resourcesPath.LastIndexOf('.');
                if (lastDot > 0)
                {
                    resourcesPath = resourcesPath.Substring(0, lastDot);
                }
                
                // Проверяем, находится ли путь в Resources
                if (!resourcesPath.Contains("Resources/"))
                {
                    return null; // Не в Resources, не можем загрузить
                }
                
                // Извлекаем путь относительно Resources
                int resourcesIndex = resourcesPath.IndexOf("Resources/");
                if (resourcesIndex >= 0)
                {
                    resourcesPath = resourcesPath.Substring(resourcesIndex + 10); // "Resources/".Length = 10
                }
                
                Type objectType = Type.GetType(objectTypeName);
                if (objectType != null)
                {
                    return Resources.Load(resourcesPath, objectType);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load Unity object from Resources: {e.Message}");
            }
            
            return null;
        }

        private static void SetFieldValueByPath(object obj, Type type, string fieldPath, object value)
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

                    var field = currentType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                    var field = currentType.GetField(part, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

            string finalPart = parts[^1];

            if (finalPart.Contains("["))
            {
                int bracketIndex = finalPart.IndexOf('[');
                string fieldName = finalPart.Substring(0, bracketIndex);
                string indexStr = finalPart.Substring(bracketIndex + 1, finalPart.IndexOf(']') - bracketIndex - 1);
                int index = int.Parse(indexStr);

                var field = currentType.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
                var field = currentType.GetField(finalPart, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(currentObj, value);
                }
            }
        }

        #endregion

        #region Serialization helper classes

        [Serializable]
        public class DataConfigWrapper
        {
            [SerializeField] public string typeName;
            [SerializeField] public string jsonData;
            [SerializeField] public string name;
            [SerializeField] public string objectReferencesJson;

            public string TypeName => typeName;
            public string JsonData => jsonData;
            public string Name => name;
            public string ObjectReferencesJson => objectReferencesJson;
        }

        [Serializable]
        public class ObjectReferenceData
        {
            [SerializeField] public string fieldPath;
            [SerializeField] public string objectGuid;
            [SerializeField] public string assetPath;
            [SerializeField] public string objectType;
            [SerializeField] public string address;
        }

        [Serializable]
        public class ConfigObjectReferences
        {
            public List<ObjectReferenceData> references = new List<ObjectReferenceData>();
        }

        #endregion
    }
}

