using UnityEngine;
using UnityEditor;
using System.IO;

namespace WebUtility
{
    public class FolderStructureCreator : EditorWindow
    {
        private string moduleName = "ModuleName";
        private bool createRouterScript = true;
        private bool createWindowScript = true;
        private bool createPresenterScript = true;

        [MenuItem("Assets/Create/Module Structure", false, 20)]
        private static void CreateModuleStructure()
        {
            GetWindow<FolderStructureCreator>("Create Module Structure");
        }

        private void OnGUI()
        {
            GUILayout.Label("Create Module Folder Structure", EditorStyles.boldLabel);

            moduleName = EditorGUILayout.TextField("Module Name", moduleName);

            EditorGUILayout.Space();
            createRouterScript = EditorGUILayout.Toggle("Create Router Script", createRouterScript);
            createPresenterScript = EditorGUILayout.Toggle("Create Presenter Script", createPresenterScript);
            createWindowScript = EditorGUILayout.Toggle("Create Window Script", createWindowScript);

            EditorGUILayout.Space();
            if (GUILayout.Button("Create"))
            {
                CreateStructure();
            }
        }

        private void CreateStructure()
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                Debug.LogError("Module name cannot be empty!");
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(selectedPath))
            {
                selectedPath = "Assets";
            }
            else if (!Directory.Exists(selectedPath))
            {
                selectedPath = Path.GetDirectoryName(selectedPath);
            }

            string basePath = Path.Combine(selectedPath, moduleName);

            try
            {
                Directory.CreateDirectory(basePath);
                Directory.CreateDirectory(Path.Combine(basePath, "Router"));
                Directory.CreateDirectory(Path.Combine(basePath, "Model"));
                Directory.CreateDirectory(Path.Combine(basePath, "View"));

                if (createRouterScript)
                {
                    string routerPath = Path.Combine(basePath, "Router", moduleName + "Router.cs");
                    CreateRouterScript(moduleName, routerPath);
                }

                if (createWindowScript)
                {
                    string windowPath = Path.Combine(basePath, "View", moduleName + "Window.cs");
                    CreateWindowScript(moduleName, windowPath);
                }

                if (createRouterScript)
                {
                    string presenterPath = Path.Combine(basePath, "Model", moduleName + "Presenter.cs");
                    CreatePresenterScript(moduleName, presenterPath);
                }

                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();
                Object createdFolder = AssetDatabase.LoadAssetAtPath(basePath, typeof(Object));
                Selection.activeObject = createdFolder;

                Debug.Log($"Module structure created at {basePath}");
                Close();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating module: {e.Message}");
            }
        }

        private void CreateRouterScript(string moduleName, string filePath)
        {
            string scriptContent = $@"using UnityEngine;
using System.Collections.Generic;
using WebUtility;

public class {moduleName}Router : IDIRouter
{{
    [Inject] private DIContainer _container;

    public List<IPresenter> Init()
    {{
        {moduleName}Presenter {moduleName.ToLower()}Presenter = _container.RegisterSingleton<{moduleName}Presenter>();

        return new List<IPresenter>()
        {{
            {moduleName.ToLower()}Presenter
        }};
    }}
}}";
            File.WriteAllText(filePath, scriptContent);
        }

        private void CreatePresenterScript(string moduleName, string filePath)
        {
            string scriptContent = $@"using UnityEngine;
using WebUtility;

public class {moduleName}Presenter : IPresenter
{{
    [Inject] private DIContainer _container;
    [Inject] private {moduleName}Window _window;

    public void Init()
    {{
        
    }}

    public void Exit()
    {{
        
    }}
}}";
            File.WriteAllText(filePath, scriptContent);
        }

        private void CreateWindowScript(string moduleName, string filePath)
        {
            string scriptContent = $@"using UnityEngine;
using WebUtility;

public class {moduleName}Window : AbstractWindowUi
{{
    public override void Init()
    {{
        
    }}
}}";
            File.WriteAllText(filePath, scriptContent);
        }
    }
}