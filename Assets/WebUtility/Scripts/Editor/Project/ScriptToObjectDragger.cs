using UnityEngine;
using UnityEditor;
using System.IO;

namespace WebUtility
{
    [InitializeOnLoad]
    public class ScriptToObjectDragger
    {
        static ScriptToObjectDragger()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
            SceneView.duringSceneGui += HandleSceneViewDrag;
        }

        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            GameObject targetObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            ProcessDragEvent(selectionRect, targetObject, false);
        }

        private static void HandleSceneViewDrag(SceneView sceneView)
        {
            if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
            {
                ProcessDragEvent(new Rect(0, 0, Screen.width, Screen.height), null, true);
            }
        }

        private static void ProcessDragEvent(Rect dropArea, GameObject targetObject = null, bool isSceneView = false)
        {
            if (Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform)
            {
                if (dropArea.Contains(Event.current.mousePosition))
                {
                    bool hasScripts = false;
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj is MonoScript)
                        {
                            hasScripts = true;
                            break;
                        }
                    }

                    if (hasScripts)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                        if (Event.current.type == EventType.DragPerform)
                        {
                            foreach (var obj in DragAndDrop.objectReferences)
                            {
                                if (obj is MonoScript script)
                                {
                                    CreateObjectFromScript(script, targetObject, isSceneView);
                                }
                            }

                            DragAndDrop.AcceptDrag();
                            Event.current.Use();
                        }
                    }
                }
            }
        }

        private static void CreateObjectFromScript(MonoScript script, GameObject targetObject = null,
            bool positionInSceneView = false)
        {
            string scriptName = Path.GetFileNameWithoutExtension(script.name);
            GameObject newObject = new GameObject(scriptName);

            if (targetObject != null)
            {
                newObject.transform.SetParent(targetObject.transform, false);
                newObject.transform.localPosition = Vector3.zero;
                newObject.transform.localRotation = Quaternion.identity;
                newObject.transform.localScale = Vector3.one;
            }
            else if (positionInSceneView)
            {
                Vector3 dropPosition = GetWorldDropPosition();
                newObject.transform.position = dropPosition;
            }

            System.Type scriptType = script.GetClass();
            if (scriptType != null && scriptType.IsSubclassOf(typeof(MonoBehaviour)))
            {
                newObject.AddComponent(scriptType);
            }

            Selection.activeGameObject = newObject;
            Undo.RegisterCreatedObjectUndo(newObject, "Create " + scriptName);
        }

        private static Vector3 GetWorldDropPosition()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return Vector3.zero;

            Vector2 mousePosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.point;
            }

            return ray.GetPoint(10);
        }
    }
}