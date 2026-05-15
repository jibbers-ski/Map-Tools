using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CustomParameterType { Null, Int, Float, Bool, String, Selection }

[Serializable]
public class CustomParameter : ISerializable
{
    public string name;
    public CustomParameterType type;

    public int intValue;
    public float floatValue;
    public bool boolValue;
    public string stringValue;
    public string optionsString;

    public void Serialize(ISerializer serializer)
    {
        if(serializer.IsReader && type == CustomParameterType.Null)
        {
            type = serializer.IsType<string>(name) ? CustomParameterType.String :
                   serializer.IsType<bool>(name) ? CustomParameterType.Bool :
                   serializer.IsType<long>(name) ? CustomParameterType.Int : CustomParameterType.Float;
        }

        switch(type)
        {
            case CustomParameterType.Int:
                intValue = serializer.SerializeInt(name, intValue);
            break;
            case CustomParameterType.Float:
                floatValue = serializer.SerializeFloat(name, floatValue);
            break;
            case CustomParameterType.Bool:
                boolValue = serializer.SerializeBool(name, boolValue);
            break;
            case CustomParameterType.String:
            case CustomParameterType.Selection:
                stringValue = serializer.SerializeString(name, stringValue ?? "");
            break;
        }
    }

    public override string ToString()
    {
        switch(type)
        {
            case CustomParameterType.Int:
                return intValue.ToString();
            case CustomParameterType.Float:
                return floatValue.ToString();
            case CustomParameterType.Bool:
                return boolValue.ToString();
            default:
                return stringValue;
        }
    }

    public CustomParameter Clone() => (CustomParameter) MemberwiseClone();
}

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(CustomParameter))]
public class CustomParameterDrawer : UnityEditor.PropertyDrawer
{
    static readonly HashSet<string> editingOptionsPaths = new HashSet<string>();

    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        UnityEditor.EditorGUI.BeginProperty(position, label, property);

        float lineH = UnityEditor.EditorGUIUtility.singleLineHeight;
        float spacing = 2f;

        var nameProp = property.FindPropertyRelative("name");
        var type = (CustomParameterType) property.FindPropertyRelative("type").enumValueIndex;

        Rect valueRect;

#if JIBBERS_MAPTOOLS_INTERNAL
        // Full setup UI: name + type + value
        float nameW = position.width * 0.3f;
        float typeW = 70f;
        float valueW = position.width - nameW - typeW - spacing * 2;

        var nameRect = new Rect(position.x,                          position.y, nameW, lineH);
        var typeRect = new Rect(position.x + nameW + spacing,        position.y, typeW, lineH);
        valueRect    = new Rect(position.x + nameW + typeW + spacing * 2, position.y, valueW, lineH);

        UnityEditor.EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
        UnityEditor.EditorGUI.PropertyField(typeRect, property.FindPropertyRelative("type"), GUIContent.none);
#else
        // Value-only: name shown as a read-only label
        float nameW = position.width * 0.3f;
        float valueW = position.width - nameW - spacing;

        var nameRect = new Rect(position.x,                   position.y, nameW, lineH);
        valueRect    = new Rect(position.x + nameW + spacing, position.y, valueW, lineH);

        UnityEditor.EditorGUI.LabelField(nameRect, nameProp.stringValue);
#endif

        switch (type)
        {
            case CustomParameterType.Int:
                UnityEditor.EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("intValue"), GUIContent.none);
                break;
            case CustomParameterType.Float:
                UnityEditor.EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("floatValue"), GUIContent.none);
                break;
            case CustomParameterType.Bool:
                UnityEditor.EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("boolValue"), GUIContent.none);
                break;
            case CustomParameterType.String:
                UnityEditor.EditorGUI.PropertyField(valueRect, property.FindPropertyRelative("stringValue"), GUIContent.none);
                break;
            case CustomParameterType.Selection:
            {
                var optionsProp = property.FindPropertyRelative("optionsString");
                var stringProp  = property.FindPropertyRelative("stringValue");

#if JIBBERS_MAPTOOLS_INTERNAL
                const float btnW = 22f;
                float mainW = valueRect.width - btnW - spacing;
                var mainRect = new Rect(valueRect.x,                  valueRect.y, mainW, lineH);
                var btnRect  = new Rect(valueRect.x + mainW + spacing, valueRect.y, btnW,  lineH);

                bool editing = editingOptionsPaths.Contains(property.propertyPath);

                if (editing)
                    optionsProp.stringValue = UnityEditor.EditorGUI.TextField(mainRect, optionsProp.stringValue);
                else
                    DrawSelectionDropdown(mainRect, optionsProp.stringValue, stringProp);

                if (GUI.Button(btnRect, editing ? "OK" : "...", UnityEditor.EditorStyles.miniButton))
                {
                    if (editing) editingOptionsPaths.Remove(property.propertyPath);
                    else         editingOptionsPaths.Add(property.propertyPath);
                }
#else
                DrawSelectionDropdown(valueRect, optionsProp.stringValue, stringProp);
#endif
                break;
            }
        }

        UnityEditor.EditorGUI.EndProperty();
    }

    static void DrawSelectionDropdown(Rect rect, string optionsString, UnityEditor.SerializedProperty stringProp)
    {
        var options = string.IsNullOrEmpty(optionsString)
            ? Array.Empty<string>()
            : optionsString.Split(',').Select(s => s.Trim()).ToArray();

        if (options.Length > 0)
        {
            int currentIdx = Array.IndexOf(options, stringProp.stringValue);
            if (currentIdx < 0) currentIdx = 0;
            int newIdx = UnityEditor.EditorGUI.Popup(rect, currentIdx, options);
            if (newIdx >= 0 && newIdx < options.Length)
                stringProp.stringValue = options[newIdx];
        }
        else
        {
            UnityEditor.EditorGUI.LabelField(rect, "(no options)", UnityEditor.EditorStyles.miniLabel);
        }
    }
}
#endif
