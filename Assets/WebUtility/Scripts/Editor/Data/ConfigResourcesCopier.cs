using System.IO;
using UnityEditor;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    /// <summary>
    /// Автоматически копирует конфиги из Assets/WebUtility/Configs в Resources/Configs
    /// для использования в runtime
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigResourcesCopier
    {
        private const string SourceConfigsPath = "Assets/WebUtility/Configs";
        private const string ResourcesConfigsPath = "Assets/Resources/Configs";
        
        static ConfigResourcesCopier()
        {
            // Копируем конфиги при загрузке Unity
            EditorApplication.delayCall += CopyConfigsToResources;
            
            // Также копируем при изменении файлов в папке конфигов
            FileSystemWatcher watcher = null;
            try
            {
                string sourcePath = Path.GetFullPath(SourceConfigsPath);
                if (Directory.Exists(sourcePath))
                {
                    watcher = new FileSystemWatcher(sourcePath, "*.json");
                    watcher.Changed += OnConfigChanged;
                    watcher.Created += OnConfigChanged;
                    watcher.EnableRaisingEvents = true;
                }
            }
            catch
            {
                // Игнорируем ошибки файловой системы
            }
        }
        
        private static void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            // Копируем с задержкой, чтобы файл успел сохраниться
            EditorApplication.delayCall += CopyConfigsToResources;
        }
        
        [MenuItem("Tools/Copy Configs to Resources")]
        public static void CopyConfigsToResources()
        {
            if (!Directory.Exists(SourceConfigsPath))
            {
                Debug.LogWarning($"Source configs folder does not exist: {SourceConfigsPath}");
                return;
            }
            
            // Создаём папку Resources/Configs если её нет
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            if (!AssetDatabase.IsValidFolder(ResourcesConfigsPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Configs");
            }
            
            // Копируем все JSON файлы (кроме index.json)
            string[] files = Directory.GetFiles(SourceConfigsPath, "*.json");
            int copiedCount = 0;
            
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                
                // Пропускаем index.json
                if (fileName == "index.json")
                    continue;
                
                string destPath = Path.Combine(ResourcesConfigsPath, fileName);
                
                try
                {
                    // Копируем файл
                    File.Copy(file, destPath, true);
                    copiedCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to copy config file {fileName}: {e.Message}");
                }
            }
            
            AssetDatabase.Refresh();
            
            if (copiedCount > 0)
            {
                Debug.Log($"Copied {copiedCount} config files to Resources/Configs");
            }
        }
    }
}


