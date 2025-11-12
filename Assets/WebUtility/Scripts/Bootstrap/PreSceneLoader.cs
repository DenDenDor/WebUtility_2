using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WebUtility
{
    public class PreSceneLoader : MonoBehaviour
    {
        private static List<string> _scenes = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnGameStart()
        {
            Debug.Log("Игра запускается! Этот метод работает и в редакторе, и в билде");

            _scenes = ScenesParser.ParseScenesList();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (_scenes.Contains(arg0.name))
            {
                string entryPoint = ScenesParser.GenerateEntryPointClassName(arg0.name);

                GameObject entryPointObject = new GameObject(entryPoint);
                entryPointObject.AddComponent(Type.GetType(entryPoint));
            }
        }
    }

    public static class ScenesParser
    {
        public static List<string> ParseScenesList()
        {
            var markedScenes = new List<string>();

            TextAsset textAsset = Resources.Load<TextAsset>("MarkedScenesList");

            if (textAsset == null)
            {
                return markedScenes;
            }

            using (StringReader reader = new StringReader(textAsset.text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                        continue;

                    string cleanedPath = line.Trim();
                    if (!string.IsNullOrEmpty(cleanedPath))
                    {
                        cleanedPath = cleanedPath.Split('/').Last().Split('.').First();
                        markedScenes.Add(cleanedPath);
                    }
                }
            }

            return markedScenes;
        }

        public static string GenerateEntryPointClassName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Scene";
            string cleaned = Regex.Replace(sceneName, "[^A-Za-z0-9_ -]", " ");
            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            string title = textInfo.ToTitleCase(cleaned.Replace('_', ' ').Trim().ToLowerInvariant());
            string noSpaces = Regex.Replace(title, @"[\t \n\r-]+", "");
            if (string.IsNullOrEmpty(noSpaces)) noSpaces = "Scene";
            if (!char.IsLetter(noSpaces[0])) noSpaces = "S" + noSpaces;
            return noSpaces + "EntryPoint";
        }
    }
}