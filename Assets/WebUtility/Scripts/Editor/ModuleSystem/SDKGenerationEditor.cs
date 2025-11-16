using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using WebUtility.Editor.Data;

public class SDKGenerationEditor : EditorWindow
{
    private string newSDKName = "";
    private string selectedDefaultSDK = "";
    private string[] availableSDKs;
    private Vector2 scrollPosition;

    [MenuItem("Tools/SDK Generation Editor")]
    public static void ShowWindow()
    {
        GetWindow<SDKGenerationEditor>("SDK Generator");
    }

    private void OnEnable()
    {
        RefreshAvailableSDKs();
        LoadDefaultSDK();
    }

    private void RefreshAvailableSDKs()
    {
        System.Type typeSDKEnum = typeof(TypeSDK);
        availableSDKs = System.Enum.GetNames(typeSDKEnum);
    }

    private void LoadDefaultSDK()
    {
        string configPath = Path.Combine(Application.dataPath, "Resources", "SDKConfig.txt");
        if (File.Exists(configPath))
        {
            selectedDefaultSDK = File.ReadAllText(configPath).Trim();
        }
        else
        {
            selectedDefaultSDK = TypeSDK.PlayerPrefs.ToString();
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField("SDK Generation", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Create New SDK Adapter", EditorStyles.label);
        
        newSDKName = EditorGUILayout.TextField("SDK Name", newSDKName);
        
        GUILayout.Space(5);
        if (GUILayout.Button("Generate SDK Adapter"))
        {
            if (string.IsNullOrEmpty(newSDKName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter SDK name", "OK");
            }
            else
            {
                GenerateSDKAdapter(newSDKName);
                newSDKName = "";
                RefreshAvailableSDKs();
            }
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Default SDK Configuration", EditorStyles.label);
        
        if (availableSDKs != null && availableSDKs.Length > 0)
        {
            int currentIndex = System.Array.IndexOf(availableSDKs, selectedDefaultSDK);
            if (currentIndex < 0) currentIndex = 0;
            
            int newIndex = EditorGUILayout.Popup("Default SDK", currentIndex, availableSDKs);
            
            if (newIndex != currentIndex && newIndex >= 0)
            {
                selectedDefaultSDK = availableSDKs[newIndex];
                SaveDefaultSDK(selectedDefaultSDK);
            }
        }
        else
        {
            EditorGUILayout.LabelField("No SDK adapters found", EditorStyles.helpBox);
        }
        
        EditorGUILayout.EndScrollView();
    }

  private void GenerateSDKAdapter(string sdkName)
{
    try
    {
        string classContent = GenerateClassContent(sdkName);
        string classPath = Path.Combine(Application.dataPath, "WebUtility", "Scripts", "SDKAdapter", "View", $"{sdkName}SDKAdapter.cs");
        WriteFile(classPath, classContent);
        
        UpdateTypeSDKEnum(sdkName);
        
        AssetDatabase.Refresh();
        
        WaitForCompilationAndCreateConfig(sdkName);
        
        RefreshAvailableSDKs();
    }
    catch (System.Exception e)
    {
        EditorUtility.DisplayDialog("Error", $"Failed to generate SDK adapter: {e.Message}", "OK");
    }
}

private void WaitForCompilationAndCreateConfig(string sdkName)
{
    int attempts = 0;
    const int maxAttempts = 100;
    
    EditorApplication.CallbackFunction checkCompilation = null;
    checkCompilation = () =>
    {
        attempts++;
        
        if (EditorApplication.isCompiling)
        {
            if (attempts < maxAttempts)
            {
                EditorApplication.delayCall += checkCompilation;
            }
            else
            {
                Debug.LogError($"Compilation timeout for {sdkName}SDKAdapter");
                EditorUtility.DisplayDialog("Warning", $"SDK adapter '{sdkName}' was created but config creation timed out. Please create it manually.", "OK");
            }
            return;
        }
        
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += () =>
            {
                CreateConfigWithRetry(sdkName, 20);
            };
        };
    };
    
    EditorApplication.delayCall += checkCompilation;
}

private void CreateConfigWithRetry(string sdkName, int retriesLeft)
{
    if (retriesLeft <= 0)
    {
        Debug.LogError($"Failed to create config for {sdkName}SDKAdapter after multiple attempts");
        EditorUtility.DisplayDialog("Warning", $"SDK adapter '{sdkName}' was created but config might need to be created manually", "OK");
        return;
    }

    if (EditorApplication.isCompiling)
    {
        Debug.LogWarning($"Unity is still compiling, retrying... ({retriesLeft} attempts left)");
        EditorApplication.delayCall += () =>
        {
            CreateConfigWithRetry(sdkName, retriesLeft - 1);
        };
        return;
    }

    string className = $"{sdkName}SDKAdapter";
    System.Type adapterType = FindTypeInAllAssemblies(className);
    
    if (adapterType == null)
    {
        Debug.Log($"Type {className} not found in assemblies, trying MonoScript...");
        adapterType = FindTypeViaMonoScript(sdkName);
    }

    if (adapterType != null)
    {
        if (typeof(AbstractData).IsAssignableFrom(adapterType))
        {
            CreateConfig(sdkName, adapterType);
            EditorUtility.DisplayDialog("Success", $"SDK adapter '{sdkName}' generated successfully!", "OK");
        }
        else
        {
            Debug.LogError($"Type {className} found but is not an AbstractData subclass. Base type: {adapterType.BaseType?.Name}");
            EditorUtility.DisplayDialog("Error", $"Type {className} is not an AbstractData subclass", "OK");
        }
    }
    else
    {
        Debug.LogWarning($"Type {className} not found, retrying... ({retriesLeft} attempts left)");
        EditorApplication.delayCall += () =>
        {
            CreateConfigWithRetry(sdkName, retriesLeft - 1);
        };
    }
}

private System.Type FindTypeInAllAssemblies(string typeName)
{
    System.Type type = System.Type.GetType(typeName + ", Assembly-CSharp");
    if (type != null) return type;
    
    type = System.Type.GetType(typeName);
    if (type != null) return type;
    
    type = System.Type.GetType(typeName + ", Assembly-CSharp-firstpass");
    if (type != null) return type;
    
    foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
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
            
            System.Type[] types = assembly.GetTypes();
            foreach (System.Type t in types)
            {
                if (t.Name == typeName)
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

private System.Type FindTypeViaMonoScript(string sdkName)
{
    string scriptPath = $"Assets/WebUtility/Scripts/SDKAdapter/View/{sdkName}SDKAdapter.cs";
    Debug.Log($"Trying to load MonoScript from: {scriptPath}");
    
    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
    
    if (script == null)
    {
        Debug.LogWarning($"MonoScript not found at path: {scriptPath}");
        string altPath = scriptPath.Replace("Assets/", "").Replace("\\", "/");
        script = AssetDatabase.LoadAssetAtPath<MonoScript>(altPath);
    }
    
    if (script != null)
    {
        System.Type type = script.GetClass();
        if (type != null)
        {
            Debug.Log($"Found type {type.Name} via MonoScript");
            return type;
        }
        else
        {
            Debug.LogWarning($"MonoScript found but GetClass() returned null. Script name: {script.name}");
        }
    }
    else
    {
        Debug.LogWarning($"MonoScript not found at any path for {sdkName}SDKAdapter");
    }
    
    return null;
}


private void CreateConfig(string sdkName, System.Type adapterType)
{
    const string ConfigsFolderPath = "Assets/WebUtility/Configs";
    
    if (!Directory.Exists(ConfigsFolderPath))
    {
        Directory.CreateDirectory(ConfigsFolderPath);
    }

    object instance = System.Activator.CreateInstance(adapterType);
    string json = JsonUtility.ToJson(instance);
    
    var wrapper = new DataConfigWrapper(adapterType.Name, json, sdkName);
    
    string configFileName = $"{adapterType.Name}_{sdkName}.json";
    string configPath = Path.Combine(ConfigsFolderPath, configFileName);
    
    if (File.Exists(configPath))
    {
        File.Delete(configPath);
    }
    
    string jsonContent = JsonUtility.ToJson(wrapper, true);
    File.WriteAllText(configPath, jsonContent);
    
    AssetDatabase.Refresh();
    
    UpdateConfigsIndex();
    ConfigEnumGenerator.UpdateEnumForType(adapterType, sdkName);
    ConfigAddressableSetup.EnsureConfigsAddressable();
    
    Debug.Log($"Successfully created config: {configPath}");
}

private void UpdateConfigsIndex()
{
    const string ConfigsFolderPath = "Assets/WebUtility/Configs";
    const string ConfigsIndexPath = "Assets/WebUtility/Configs/index.json";
    
    if (!Directory.Exists(ConfigsFolderPath))
        return;
    
    var configs = new List<DataConfigWrapper>();
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
            catch
            {
            }
        }
    }
    catch (System.Exception e)
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
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Failed to save index: {e.Message}");
    }
}

[System.Serializable]
private class ConfigsIndex
{
    public List<string> configs;

    public ConfigsIndex()
    {
        configs = new List<string>();
    }
}

    private string GenerateClassContent(string sdkName)
    {
        StringBuilder sb = new StringBuilder();
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"public class {sdkName}SDKAdapter : AbstractSDKAdapter");
        sb.AppendLine("{");
        
        sb.AppendLine("    private readonly string _saveKey = \"SaveKey\";");
        sb.AppendLine();
        
        sb.AppendLine("    public override bool IsPaymentAvailable => true;");
        sb.AppendLine("    public override bool IsMobile => Application.isMobilePlatform;");
        sb.AppendLine("    public override bool IsSpecialFlag => true;");
        sb.AppendLine("    public override string Language => Application.systemLanguage.ToString();");
        sb.AppendLine("    public override bool IsAuthorized => false;");
        sb.AppendLine();
        
        sb.AppendLine("    public override void Save<T>(T data)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrEmpty(_saveKey))");
        sb.AppendLine("        {");
        sb.AppendLine("            Debug.LogError(\"Ключ не может быть пустым.\");");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        string json = JsonUtility.ToJson(data);");
        sb.AppendLine("        PlayerPrefs.SetString(_saveKey, json);");
        sb.AppendLine("        PlayerPrefs.Save();");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override bool TryLoad<T>(out T data)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrEmpty(_saveKey))");
        sb.AppendLine("        {");
        sb.AppendLine("            Debug.LogError(\"Ключ не может быть пустым.\");");
        sb.AppendLine("            data = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        string json = PlayerPrefs.GetString(_saveKey, null);");
        sb.AppendLine("        if (string.IsNullOrEmpty(json))");
        sb.AppendLine("        {");
        sb.AppendLine("            Debug.LogWarning($\"Данные с ключом '{_saveKey}' не найдены.\");");
        sb.AppendLine("            data = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        data = JsonUtility.FromJson<T>(json);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void SendMetrica(string metrica)");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] Send metrica: {metrica}\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void SendMetrica(string metrica, IDictionary<string, string> eventParams)");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] Send metrica: {metrica} with params\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void UpdateLeaderboard()");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] Update leaderboard\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void ShowFullscreenAd()");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] Show fullscreen ad\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override bool GetName(out string nickname)");
        sb.AppendLine("    {");
        sb.AppendLine("        nickname = \"Player\";");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override bool GetLanguage(string language)");
        sb.AppendLine("    {");
        sb.AppendLine("        return language == Language;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void Watch(Action onWatch, RewardType rewardType)");
        sb.AppendLine("    {");
        sb.AppendLine("        onWatch?.Invoke();");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void OnStart()");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] OnStart\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void OnEnd()");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] OnEnd\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void MakeReview()");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] Make review\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override string GetPrice(string key)");
        sb.AppendLine("    {");
        sb.AppendLine("        return $\"10 {key}Coins\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override IEnumerator GetUserSprite(Action<Sprite> action)");
        sb.AppendLine("    {");
        sb.AppendLine("        Texture2D texture = new Texture2D(64, 64);");
        sb.AppendLine("        Color[] colors = new Color[64 * 64];");
        sb.AppendLine("        for (int i = 0; i < colors.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            colors[i] = Color.blue;");
        sb.AppendLine("        }");
        sb.AppendLine("        texture.SetPixels(colors);");
        sb.AppendLine("        texture.Apply();");
        sb.AppendLine();
        sb.AppendLine("        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));");
        sb.AppendLine("        action(sprite);");
        sb.AppendLine("        yield return null;");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void BuyPayment(Action onBuy, string buyKey)");
        sb.AppendLine("    {");
        sb.AppendLine("        onBuy?.Invoke();");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        sb.AppendLine("    public override void InvokeGRA()");
        sb.AppendLine("    {");
        sb.AppendLine("        Debug.Log($\"[{nameof(" + sdkName + "SDKAdapter)}] Invoke GRA\");");
        sb.AppendLine("    }");
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }


    private void WriteFile(string path, string content)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    private void SaveDefaultSDK(string defaultSDK)
    {
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (!Directory.Exists(resourcesPath))
        {
            Directory.CreateDirectory(resourcesPath);
        }
        
        string configPath = Path.Combine(resourcesPath, "SDKConfig.txt");
        File.WriteAllText(configPath, defaultSDK, Encoding.UTF8);
        
        AssetDatabase.Refresh();
        
        Debug.Log($"Default SDK saved: {defaultSDK}");
    }

    private void UpdateTypeSDKEnum(string newSDK)
    {
        try
        {
            string enumPath = Path.Combine(Application.dataPath, "WebUtility", "Scripts", "SDKAdapter", "Model", "TypeSDK.cs");
            
            if (!File.Exists(enumPath))
            {
                Debug.LogError($"TypeSDK.cs not found at path: {enumPath}");
                return;
            }

            string[] lines = File.ReadAllLines(enumPath);
            List<string> newLines = new List<string>();
            
            bool foundEnum = false;
            bool addedNewValue = false;
            
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                newLines.Add(lines[i]);
                
                if (line.Contains("public enum TypeSDK"))
                {
                    foundEnum = true;
                }
                
                if (foundEnum && !addedNewValue && line == "}")
                {
                    newLines.RemoveAt(newLines.Count - 1);
                    
                    int lastValue = 0;
                    for (int j = newLines.Count - 1; j >= 0; j--)
                    {
                        string enumLine = newLines[j].Trim();
                        if (enumLine.Contains("=") && enumLine.Contains(","))
                        {
                            string valuePart = enumLine.Split('=')[1].Split(',')[0].Trim();
                            if (int.TryParse(valuePart, out lastValue))
                            {
                                break;
                            }
                        }
                    }
                    
                    newLines.Add($"    {newSDK} = {lastValue + 1},");
                    newLines.Add("}");
                    addedNewValue = true;
                }
            }
            
            if (foundEnum && addedNewValue)
            {
                File.WriteAllLines(enumPath, newLines, Encoding.UTF8);
                Debug.Log($"Successfully added {newSDK} to TypeSDK enum");
            }
            else
            {
                Debug.LogError("Failed to update TypeSDK enum - structure not recognized");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating TypeSDK enum: {e.Message}");
        }
    }
}