using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace WebUtility
{
    public class SceneSelector : MonoBehaviour
    {
        public bool includeInBuild = false;
    }

    [CustomEditor(typeof(SceneSelector))]
    public class SceneSelectorEditor : Editor
    {
    }
}