using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    [InitializeOnLoad]
    public static class ConfigAddressableSetup
    {
        private const string SourceConfigsPath = "Assets/WebUtility/Configs";
        private const string AddressableGroupName = "Configs";
        private const string AddressLabel = "Config";
        private const string AddressPrefix = "Configs/";

        static ConfigAddressableSetup()
        {
            EditorApplication.delayCall += EnsureConfigsAddressable;
        }

        [MenuItem("Tools/Addressables/Update Config Entries")]
        public static void EnsureConfigsAddressable()
        {
            if (!Directory.Exists(SourceConfigsPath))
            {
                Debug.LogWarning($"Config directory not found: {SourceConfigsPath}");
                return;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("AddressableAssetSettings not found. Create Addressables settings before syncing configs.");
                return;
            }

            var group = settings.FindGroup(AddressableGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(AddressableGroupName, false, false, false, null, typeof(BundledAssetGroupSchema));
                if (group != null)
                {
                    group.AddSchema<ContentUpdateGroupSchema>();
                }
            }

            string[] files = Directory.GetFiles(SourceConfigsPath, "*.json", SearchOption.TopDirectoryOnly);
            int updatedCount = 0;

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                if (fileName == "index.json")
                    continue;

                string assetGuid = AssetDatabase.AssetPathToGUID(file);
                if (string.IsNullOrEmpty(assetGuid))
                    continue;

                try
                {
                    string configJson = File.ReadAllText(file);
                    var wrapper = JsonUtility.FromJson<DataConfigWrapper>(configJson);
                    if (wrapper == null || string.IsNullOrEmpty(wrapper.TypeName) || string.IsNullOrEmpty(wrapper.Name))
                        continue;

                    var entry = settings.FindAssetEntry(assetGuid);
                    if (entry == null)
                    {
                        entry = settings.CreateOrMoveEntry(assetGuid, group, readOnly: false, postEvent: false);
                    }
                    else if (entry.parentGroup != group)
                    {
                        settings.MoveEntry(entry, group);
                    }

                    string address = $"{AddressPrefix}{wrapper.TypeName}_{wrapper.Name}";
                    if (entry.address != address)
                    {
                        entry.address = address;
                        updatedCount++;
                    }

                    entry.SetLabel(AddressLabel, true, true);
                }
                catch
                {
                }
            }

            if (updatedCount > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log($"Addressable config entries updated: {updatedCount}");
            }
        }
    }
}
