#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Linq;

namespace WebUtility
{
    [CustomEditor(typeof(SceneAsset))]
    [CanEditMultipleObjects]
    public class SceneAssetEditor : UnityEditor.Editor
    {
        private const string LabelName = "SceneChecked";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var sceneTargets = new List<SceneAsset>();
            foreach (var t in targets)
            {
                if (t is SceneAsset sa) sceneTargets.Add(sa);
            }

            if (sceneTargets.Count == 0) return;

            bool firstValue = HasLabel(sceneTargets[0]);
            bool mixed = false;
            for (int i = 1; i < sceneTargets.Count; i++)
            {
                if (HasLabel(sceneTargets[i]) != firstValue)
                {
                    mixed = true;
                    break;
                }
            }

            EditorGUI.showMixedValue = mixed;
            bool prevEnabled = GUI.enabled;
            GUI.enabled = true;
            bool newValue = EditorGUILayout.ToggleLeft("Mark scene (Project)", firstValue);
            GUI.enabled = prevEnabled;
            EditorGUI.showMixedValue = false;

            if (newValue != firstValue || mixed)
            {
                Undo.RecordObjects(sceneTargets.ToArray(), "Toggle Scene Mark");
                
                foreach (var sa in sceneTargets)
                {
                    bool wasLabeled = HasLabel(sa);
                    
                    SetLabel(sa, newValue);
                    if (newValue && !wasLabeled)
                    {
                        TryCreateEntryPointForScene(sa);
                    }
                }

                UpdateMarkedScenesList();
            }
        }

        private static bool HasLabel(Object obj)
        {
            var labels = AssetDatabase.GetLabels(obj);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == LabelName) return true;
            }

            return false;
        }

        private static void SetLabel(Object obj, bool enabled)
        {
            var labels = new List<string>(AssetDatabase.GetLabels(obj));
            bool has = labels.Contains(LabelName);
            if (enabled && !has) labels.Add(LabelName);
            else if (!enabled && has) labels.Remove(LabelName);
            AssetDatabase.SetLabels(obj, labels.ToArray());
        }

        private static void TryCreateEntryPointForScene(SceneAsset sceneAsset)
        {
            string scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            if (string.IsNullOrEmpty(scenePath)) return;

            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(sceneGuid)) return;

            string targetDirRelative = "Assets/WebUtility/Scripts/EntryPoint";
            string targetDirAbsolute = Path.Combine(Application.dataPath, "WebUtility", "Scripts", "EntryPoint");
            if (!Directory.Exists(targetDirAbsolute)) Directory.CreateDirectory(targetDirAbsolute);

            if (EntryPointExistsForGuid(targetDirAbsolute, sceneGuid)) return;

            string className =  ScenesParser.GenerateEntryPointClassName(sceneAsset.name);
            string fileName = className + ".cs";
            string fileAbsolutePath = Path.Combine(targetDirAbsolute, fileName);
            string fileRelativePath = Path.Combine(targetDirRelative, fileName).Replace("\\", "/");

            if (File.Exists(fileAbsolutePath))
            {
                fileName = className + "_" + sceneGuid.Substring(0, 8) + ".cs";
                fileAbsolutePath = Path.Combine(targetDirAbsolute, fileName);
                fileRelativePath = Path.Combine(targetDirRelative, fileName).Replace("\\", "/");
            }

            var code = GetEntryPointTemplate(className, sceneGuid);
            File.WriteAllText(fileAbsolutePath, code);
            AssetDatabase.ImportAsset(fileRelativePath);
            AssetDatabase.Refresh();
        }

        private static bool EntryPointExistsForGuid(string directoryAbsolute, string sceneGuid)
        {
            if (!Directory.Exists(directoryAbsolute)) return false;
            var files = Directory.GetFiles(directoryAbsolute, "*.cs", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    var text = File.ReadAllText(files[i]);
                    if (text.Contains("// SceneGUID: " + sceneGuid)) return true;
                }
                catch
                {
                    
                }
            }

            return false;
        }

        private static string GetEntryPointTemplate(string className, string sceneGuid)
        {
            return "// Auto-generated EntryPoint for scene\n" +
                   "// SceneGUID: " + sceneGuid + "\n" +
                   "using UnityEngine;\n" +
                   "using System.Collections.Generic;\n" +
                   "using WebUtility;\n\n" +
                   "public class " + className + " : AbstractEntryPoint\n" +
                   "{\n" +
                   "   protected override List<IDIRouter> Routers => new List<IDIRouter>()\n" +
                   "   {\n" +
                   "       \n" +
                   "   };\n" +
                   "}\n";
        }
        
        private static void UpdateMarkedScenesList()
        {
            var allSceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            var markedScenes = new List<string>();

            foreach (var guid in allSceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (scene != null && HasLabel(scene))
                {
                    markedScenes.Add(path);
                }
            }

            string resourcesDir = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesDir))
                Directory.CreateDirectory(resourcesDir);

            string filePath = Path.Combine(resourcesDir, "MarkedScenesList.txt");
            string content = string.Join("\n", markedScenes.ToArray());
            File.WriteAllText(filePath, content);

            AssetDatabase.Refresh();
        }
        
        [InitializeOnLoadMethod]
        private static void InitializeMarkedScenesList()
        {
            EditorApplication.delayCall += () => 
            {
                UpdateMarkedScenesList();
            };
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class SceneAssetMarkerDrawer
    {
        private const string LabelName = "SceneChecked";
        private static readonly GUIContent Icon;

        static SceneAssetMarkerDrawer()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectGUI;
            Icon = EditorGUIUtility.IconContent("TestPassed");
            if (Icon == null || Icon.image == null)
            {
                Icon = EditorGUIUtility.IconContent("d_Favorite");
            }
        }

        private static void OnProjectGUI(string guid, Rect selectionRect)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity")) return;

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (scene == null) return;

            var labels = AssetDatabase.GetLabels(scene);
            bool marked = false;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == LabelName)
                {
                    marked = true;
                    break;
                }
            }

            if (!marked) return;

            var r = new Rect(selectionRect.xMax - 16f, selectionRect.y, 16f, 16f);
            GUI.Label(r, Icon);
        }
    }
#endif
}
#endif