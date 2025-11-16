using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WebUtility.Editor.Data
{
    public static class ConfigFieldEditor
    {
        public static void DrawFields(object target, Type type, SerializedObject serializedObject = null)
        {
            if (target == null || type == null)
                return;
            
            FieldInfo[] allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fields = new List<FieldInfo>();
            
            foreach (var field in allFields)
            {
                if (field.IsPublic)
                {
                    if (field.GetCustomAttribute<System.NonSerializedAttribute>() == null)
                    {
                        fields.Add(field);
                    }
                }
                else if (field.GetCustomAttribute<SerializeField>() != null)
                {
                    fields.Add(field);
                }
            }
            
            foreach (var field in fields)
            {
                if (field.GetCustomAttribute<System.NonSerializedAttribute>() != null)
                    continue;
                
                Type fieldType = field.FieldType;
                object fieldValue = field.GetValue(target);
                
                if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
                {
                    UnityEngine.Object objValue = fieldValue as UnityEngine.Object;
                    UnityEngine.Object newValue = EditorGUILayout.ObjectField(
                        field.Name, 
                        objValue, 
                        fieldType, 
                        true);
                    
                    if (newValue != objValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(string))
                {
                    string strValue = fieldValue as string ?? "";
                    string newValue = EditorGUILayout.TextField(field.Name, strValue);
                    if (newValue != strValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(int))
                {
                    int intValue = fieldValue != null ? (int)fieldValue : 0;
                    int newValue = EditorGUILayout.IntField(field.Name, intValue);
                    if (newValue != intValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(float))
                {
                    float floatValue = fieldValue != null ? (float)fieldValue : 0f;
                    float newValue = EditorGUILayout.FloatField(field.Name, floatValue);
                    if (newValue != floatValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(bool))
                {
                    bool boolValue = fieldValue != null && (bool)fieldValue;
                    bool newValue = EditorGUILayout.Toggle(field.Name, boolValue);
                    if (newValue != boolValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(Vector2))
                {
                    Vector2 vecValue = fieldValue != null ? (Vector2)fieldValue : Vector2.zero;
                    Vector2 newValue = EditorGUILayout.Vector2Field(field.Name, vecValue);
                    if (newValue != vecValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(Vector3))
                {
                    Vector3 vecValue = fieldValue != null ? (Vector3)fieldValue : Vector3.zero;
                    Vector3 newValue = EditorGUILayout.Vector3Field(field.Name, vecValue);
                    if (newValue != vecValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(Vector4))
                {
                    Vector4 vecValue = fieldValue != null ? (Vector4)fieldValue : Vector4.zero;
                    Vector4 newValue = EditorGUILayout.Vector4Field(field.Name, vecValue);
                    if (newValue != vecValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType == typeof(Color))
                {
                    Color colorValue = fieldValue != null ? (Color)fieldValue : Color.white;
                    Color newValue = EditorGUILayout.ColorField(field.Name, colorValue);
                    if (newValue != colorValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType.IsEnum)
                {
                    Enum enumValue = fieldValue as Enum ?? (Enum)Enum.GetValues(fieldType).GetValue(0);
                    Enum newValue = EditorGUILayout.EnumPopup(field.Name, enumValue);
                    if (newValue != enumValue)
                    {
                        field.SetValue(target, newValue);
                    }
                }
                else if (fieldType.IsArray)
                {
                    EditorGUILayout.LabelField(field.Name, "Array editing not fully supported");
                }
                else if (fieldType.IsClass && fieldType.GetCustomAttribute<SerializableAttribute>() != null)
                {
                    EditorGUILayout.LabelField(field.Name, fieldType.Name);
                    EditorGUI.indentLevel++;
                    if (fieldValue != null)
                    {
                        DrawFields(fieldValue, fieldType);
                    }
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField(field.Name, fieldValue?.ToString() ?? "null");
                }
            }
        }
    }
}

