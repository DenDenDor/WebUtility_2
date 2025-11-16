using System;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    [Serializable]
    public class DataConfigWrapper
    {
        [SerializeField] private string typeName;
        [SerializeField] private string jsonData;
        [SerializeField] private string name;
        [SerializeField] private string objectReferencesJson;

        public string TypeName => typeName;
        public string JsonData => jsonData;
        public string Name => name;
        public string ObjectReferencesJson => objectReferencesJson;

        public DataConfigWrapper()
        {
        }

        public DataConfigWrapper(string typeName, string jsonData, string name)
        {
            this.typeName = typeName;
            this.jsonData = jsonData;
            this.name = name;
            this.objectReferencesJson = "";
        }

        public void UpdateJsonData(string newJsonData)
        {
            jsonData = newJsonData;
        }

        public void UpdateName(string newName)
        {
            name = newName;
        }

        public void UpdateObjectReferences(string referencesJson)
        {
            objectReferencesJson = referencesJson;
        }
    }
}

