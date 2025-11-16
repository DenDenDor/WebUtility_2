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
        [SerializeField] public string assetPath; // Путь к ассету (для справки)
        [SerializeField] public string objectType; // Тип объекта (для загрузки)
        [SerializeField] public string address; // Addressables-адрес
        
        public ObjectReferenceData() { }
        
        public ObjectReferenceData(string fieldPath, string objectGuid, string assetPath, string objectType, string address)
        {
            this.fieldPath = fieldPath;
            this.objectGuid = objectGuid;
            this.assetPath = assetPath;
            this.objectType = objectType;
            this.address = address;
        }
    }

    [Serializable]
    public class ConfigObjectReferences
    {
        public List<ObjectReferenceData> references = new List<ObjectReferenceData>();
    }
}

