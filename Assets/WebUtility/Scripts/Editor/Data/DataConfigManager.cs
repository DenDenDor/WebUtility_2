using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    /// <summary>
    /// Менеджер для работы с конфигами данных
    /// Позволяет получать сохранённые конфиги по GUID
    /// 
    /// Пример использования:
    /// <code>
    /// // Получить конфиг по GUID
    /// var weaponConfig = DataConfigManager.GetData&lt;WeaponData&gt;("390a5133-85d0-4158-8ef8-bd4b7bc9b3ee");
    /// 
    /// // Получить все конфиги определённого типа
    /// var allWeapons = DataConfigManager.GetAllDataOfType&lt;WeaponData&gt;();
    /// 
    /// // Проверить существование конфига
    /// bool exists = DataConfigManager.ConfigExists("390a5133-85d0-4158-8ef8-bd4b7bc9b3ee");
    /// </code>
    /// </summary>
    public static class DataConfigManager
    {
        private const string ConfigsFolderPath = "Assets/WebUtility/Configs";
        
        /// <summary>
        /// Получить данные конфига по GUID или имени (TypeName_ConfigName)
        /// </summary>
        /// <typeparam name="T">Тип данных конфига (должен наследоваться от AbstractData)</typeparam>
        /// <param name="guid">GUID конфига или ключ в формате TypeName_ConfigName</param>
        /// <returns>Экземпляр конфига или null, если не найден</returns>
        public static T GetData<T>(string guid) where T : AbstractData
        {
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("GUID cannot be null or empty");
                return null;
            }
            
            // Поддерживаем обратную совместимость: имя файла может быть как GUID.json, так и TypeName_Name.json
            string configPath = Path.Combine(ConfigsFolderPath, $"{guid}.json");
            
            if (!File.Exists(configPath))
            {
                Debug.LogError($"Config file not found: {configPath}");
                return null;
            }
            
            try
            {
                // Читаем файл конфига
                string configJson = File.ReadAllText(configPath);
                var wrapper = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                
                if (wrapper == null)
                {
                    Debug.LogError($"Failed to deserialize config wrapper from {configPath}");
                    return null;
                }
                
                // Проверяем тип
                if (wrapper.TypeName != typeof(T).Name)
                {
                    Debug.LogWarning($"Config type mismatch. Expected {typeof(T).Name}, but got {wrapper.TypeName}");
                }
                
                // Десериализуем данные
                T data = JsonUtility.FromJson<T>(wrapper.JsonData);
                
                if (data == null)
                {
                    Debug.LogError($"Failed to deserialize data of type {typeof(T).Name} from config {guid}");
                    return null;
                }
                
                // Восстанавливаем ссылки на Unity объекты
                if (!string.IsNullOrEmpty(wrapper.ObjectReferencesJson))
                {
                    RestoreUnityObjectReferences(data, typeof(T), wrapper.ObjectReferencesJson);
                }
                
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load config {guid}: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
        }
        
        /// <summary>
        /// Получить данные конфига по GUID (без указания типа)
        /// </summary>
        /// <param name="guid">GUID конфига</param>
        /// <param name="type">Тип данных конфига</param>
        /// <returns>Экземпляр конфига или null, если не найден</returns>
        public static object GetData(string guid, Type type)
        {
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError("GUID cannot be null or empty");
                return null;
            }
            
            if (type == null)
            {
                Debug.LogError("Type cannot be null");
                return null;
            }
            
            if (!typeof(AbstractData).IsAssignableFrom(type))
            {
                Debug.LogError($"Type {type.Name} must inherit from AbstractData");
                return null;
            }
            
            // Поддерживаем обратную совместимость: если guid выглядит как ключ (TypeName_Name), используем его напрямую
            string configPath;
            if (guid.Contains("_"))
            {
                // Новый формат: TypeName_Name.json
                configPath = Path.Combine(ConfigsFolderPath, $"{guid}.json");
            }
            else
            {
                // Старый формат: GUID.json
                configPath = Path.Combine(ConfigsFolderPath, $"{guid}.json");
            }
            
            if (!File.Exists(configPath))
            {
                Debug.LogError($"Config file not found: {configPath}");
                return null;
            }
            
            try
            {
                // Читаем файл конфига
                string configJson = File.ReadAllText(configPath);
                var wrapper = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                
                if (wrapper == null)
                {
                    Debug.LogError($"Failed to deserialize config wrapper from {configPath}");
                    return null;
                }
                
                // Десериализуем данные
                object data = JsonUtility.FromJson(wrapper.JsonData, type);
                
                if (data == null)
                {
                    Debug.LogError($"Failed to deserialize data of type {type.Name} from config {guid}");
                    return null;
                }
                
                // Восстанавливаем ссылки на Unity объекты
                if (!string.IsNullOrEmpty(wrapper.ObjectReferencesJson))
                {
                    RestoreUnityObjectReferences(data, type, wrapper.ObjectReferencesJson);
                }
                
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load config {guid}: {e.Message}\nStackTrace: {e.StackTrace}");
                return null;
            }
        }
        
        /// <summary>
        /// Проверить существование конфига по GUID
        /// </summary>
        /// <param name="guid">GUID конфига</param>
        /// <returns>True, если конфиг существует</returns>
        public static bool ConfigExists(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return false;
            
            // Поддерживаем обратную совместимость
            string configPath = Path.Combine(ConfigsFolderPath, $"{guid}.json");
            return File.Exists(configPath);
        }
        
        /// <summary>
        /// Получить все GUID конфигов указанного типа
        /// </summary>
        /// <typeparam name="T">Тип конфига</typeparam>
        /// <returns>Массив GUID конфигов указанного типа</returns>
        public static string[] GetAllGuidsOfType<T>() where T : AbstractData
        {
            return GetAllGuidsOfType(typeof(T));
        }
        
        /// <summary>
        /// Получить все конфиги указанного типа
        /// </summary>
        /// <typeparam name="T">Тип конфига</typeparam>
        /// <returns>Массив конфигов указанного типа</returns>
        public static T[] GetAllDataOfType<T>() where T : AbstractData
        {
            string[] guids = GetAllGuidsOfType<T>();
            var results = new System.Collections.Generic.List<T>();
            
            foreach (var guid in guids)
            {
                T data = GetData<T>(guid);
                if (data != null)
                {
                    results.Add(data);
                }
            }
            
            return results.ToArray();
        }
        
        /// <summary>
        /// Получить все GUID конфигов указанного типа
        /// </summary>
        /// <param name="type">Тип конфига</param>
        /// <returns>Массив GUID конфигов указанного типа</returns>
        public static string[] GetAllGuidsOfType(Type type)
        {
            if (type == null)
                return new string[0];
            
            if (!Directory.Exists(ConfigsFolderPath))
                return new string[0];
            
            var guids = new System.Collections.Generic.List<string>();
            
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
                        var wrapper = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                        
                        if (wrapper != null && wrapper.TypeName == type.Name)
                        {
                            // Используем имя конфига вместо GUID
                            string configKey = $"{wrapper.TypeName}_{wrapper.Name}";
                            guids.Add(configKey);
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки при чтении отдельных файлов
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to scan config files: {e.Message}");
            }
            
            return guids.ToArray();
        }
        
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
                        // Загружаем объект по GUID
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
                                    // Устанавливаем значение поля
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
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse Unity object references: {e.Message}");
            }
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
                
                // Проверяем, является ли часть массивом
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
            
            // Устанавливаем финальное значение
            string finalPart = parts[parts.Length - 1];
            
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
    }
}

