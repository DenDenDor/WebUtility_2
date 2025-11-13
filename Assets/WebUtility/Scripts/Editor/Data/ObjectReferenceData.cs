using System;
using System.Collections.Generic;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    [Serializable]
    public class ObjectReferenceData
    {
        [SerializeField] public string fieldPath; // Путь к полю (например, "weaponSprite" или "nestedData.sprite")
        [SerializeField] public string objectGuid; // GUID объекта в Unity
        [SerializeField] public string assetPath; // Путь к ассету
        [SerializeField] public string objectType; // Тип объекта (для загрузки)
        
        public ObjectReferenceData() { }
        
        public ObjectReferenceData(string fieldPath, string objectGuid, string assetPath, string objectType)
        {
            this.fieldPath = fieldPath;
            this.objectGuid = objectGuid;
            this.assetPath = assetPath;
            this.objectType = objectType;
        }
    }

    [Serializable]
    public class ConfigObjectReferences
    {
        public List<ObjectReferenceData> references = new List<ObjectReferenceData>();
    }
}

