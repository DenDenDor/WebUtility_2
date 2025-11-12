using UnityEngine;
using UnityEditor;
using System.IO;

namespace WebUtility
{
    public static class SingletonControllerTemplate
    {
        private const string Template =
            @"using UnityEngine;
using System;

public class {0} : MonoBehaviour
{{
    private static {0} _instance;

    public static {0} Instance
    {{
        get
        {{
            if (_instance == null)
            {{
                _instance = FindObjectOfType<{0}>();

                if (_instance == null)
                {{
                    throw new NotImplementedException(""{0} not found!"");
                }}
            }}

            return _instance;
        }}
    }}

    private void Awake()
    {{
        if (_instance != null && _instance != this)
        {{
            Destroy(gameObject);
            return;
        }}
        
        _instance = this;
    }}
}}";

        [MenuItem("Assets/Create/Singleton Controller", priority = 0)]
        public static void CreateSingletonController()
        {
            string className = "NewSingletonController";
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (string.IsNullOrEmpty(path))
                path = "Assets";
            else if (!Directory.Exists(path))
                path = Path.GetDirectoryName(path);

            className = EditorInputDialog.Show("Create Singleton Controller", "Enter class name:", "XController");
            if (string.IsNullOrEmpty(className))
                return;

            string filePath = Path.Combine(path, className + ".cs");

            string finalCode = string.Format(Template, className);

            File.WriteAllText(filePath, finalCode);
            AssetDatabase.Refresh();
        }
    }

    public class EditorInputDialog : EditorWindow
    {
        private string inputText = "";
        private System.Action<string> onOk;

        public static string Show(string title, string label, string defaultText = "")
        {
            EditorInputDialog window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.inputText = defaultText;
            window.ShowModal();
            return window.inputText;
        }

        private void OnGUI()
        {
            GUILayout.Label("Enter class name:");
            inputText = EditorGUILayout.TextField(inputText);

            if (GUILayout.Button("OK"))
            {
                Close();
            }
        }
    }
}