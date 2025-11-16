using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    public static class ConfigEnumGenerator
    {
        private const string EnumsFolderPath = "Assets/WebUtility/Scripts/Data/ConfigEnums";
        
        public static void GenerateEnumForType(Type configType)
        {
            if (configType == null || !typeof(AbstractData).IsAssignableFrom(configType))
            {
                Debug.LogError($"Invalid config type: {configType?.Name}");
                return;
            }
            
            var configs = GetAllConfigsOfType(configType);
            var configNames = configs.Select(c => c.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            
            if (configNames.Count == 0)
            {
                Debug.LogWarning($"No configs found for type {configType.Name}. Enum will be empty.");
            }
            
            string enumName = configType.Name + "Type";
            string enumContent = GenerateEnumContent(enumName, configNames);
            
            SaveEnum(enumName, enumContent);
        }
        
        public static void GenerateAllEnums()
        {
            var types = GetAllAbstractDataTypes();
            foreach (var type in types)
            {
                GenerateEnumForType(type);
            }
            
            AssetDatabase.Refresh();
            Debug.Log("All config enums generated successfully.");
        }
        
        public static void UpdateEnumForType(Type configType, string configName, bool isRemoved = false)
        {
            GenerateEnumForType(configType);
        }
        
        private static string GenerateEnumContent(string enumName, List<string> configNames)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// Auto-generated enum. Do not edit manually.");
            sb.AppendLine("// This enum is generated based on config names.");
            sb.AppendLine();
            sb.AppendLine("namespace WebUtility.Data");
            sb.AppendLine("{");
            sb.AppendLine($"    public enum {enumName}");
            sb.AppendLine("    {");
            
            if (configNames.Count == 0)
            {
                sb.AppendLine("        None = 0");
            }
            else
            {
                for (int i = 0; i < configNames.Count; i++)
                {
                    string enumValue = SanitizeEnumName(configNames[i]);
                    sb.Append($"        {enumValue} = {i + 1}");
                    if (i < configNames.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private static string SanitizeEnumName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "None";
            
            string sanitized = name;
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^a-zA-Z0-9_]", "");
            
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "Config_" + sanitized;
            }
            
            if (string.IsNullOrEmpty(sanitized))
                return "None";
            
            return sanitized;
        }
        
        private static void SaveEnum(string enumName, string content)
        {
            if (!Directory.Exists(EnumsFolderPath))
            {
                Directory.CreateDirectory(EnumsFolderPath);
            }
            
            string filePath = Path.Combine(EnumsFolderPath, $"{enumName}.cs");
            File.WriteAllText(filePath, content);
            
            AssetDatabase.Refresh();
        }
        
        private static List<DataConfigWrapper> GetAllConfigsOfType(Type type)
        {
            var configs = new List<DataConfigWrapper>();
            const string ConfigsFolderPath = "Assets/WebUtility/Configs";
            
            if (!Directory.Exists(ConfigsFolderPath))
                return configs;
            
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
                        if (config != null && config.TypeName == type.Name)
                        {
                            configs.Add(config);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            
            return configs;
        }
        
        private static List<Type> GetAllAbstractDataTypes()
        {
            var types = new List<Type>();
            
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;
                    
                    if (typeof(AbstractData).IsAssignableFrom(type))
                    {
                        if (type.GetCustomAttribute<SerializableAttribute>() != null || type.IsSerializable)
                        {
                            types.Add(type);
                        }
                    }
                }
            }
            
            return types;
        }
    }
}

