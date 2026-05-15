using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

#if UNITY_EDITOR && !JIBBERS_MAPTOOLS_INTERNAL
    [UnityEditor.CustomEditor(typeof(MapObject))]
    public class MapObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var paramsProp = serializedObject.FindProperty("parameters");
            if (paramsProp != null)
            {
                UnityEditor.EditorGUILayout.LabelField("Parameters", UnityEditor.EditorStyles.boldLabel);
                if (paramsProp.arraySize == 0)
                {
                    UnityEditor.EditorGUILayout.LabelField("(none)", UnityEditor.EditorStyles.miniLabel);
                }
                else
                {
                    for (int i = 0; i < paramsProp.arraySize; i++)
                        UnityEditor.EditorGUILayout.PropertyField(paramsProp.GetArrayElementAtIndex(i), GUIContent.none);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

    public class MapObject : MonoBehaviour
    {

        public List<CustomParameter> parameters;


#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public string id;

#if !JIBBERS_MAPTOOLS_INTERNAL
        [HideInInspector]
#endif
        public bool forceUniformScale;

    }

    public class MapObjectData : ISerializable
    {

        public string id;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        public Dictionary<string,CustomParameter> parameters = new();

        public MapObjectData() {}
        public MapObjectData(MapObject mapObject)
        {
            id = mapObject.id;
            position = mapObject.transform.position;
            rotation = mapObject.transform.rotation.eulerAngles;
            scale = mapObject.transform.localScale;

            parameters = new();
            foreach(var parameter in mapObject.parameters)
                parameters[parameter.name] = parameter.Clone();
        }

        public void Serialize(ISerializer serializer)
        {
            id = serializer.SerializeString("id", id);

            position = serializer.SerializeVector3("position", position);
            rotation = serializer.SerializeVector3("rotation", rotation);
            scale = serializer.SerializeVector3("scale", scale);

            if (serializer.IsReader || !(parameters == null || parameters.Count == 0))
                serializer.SerializeSerializableDict("parameters", ref parameters, s => new CustomParameter { name = s }, enterBlocks:false);
        }

    }

}
