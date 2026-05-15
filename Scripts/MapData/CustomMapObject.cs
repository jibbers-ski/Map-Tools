using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public enum IntendedUpMethod
    {
        LocalBody,
        LocalBodyRightProjected,
        LocalBodyForwardProjected,
        PerBodyYUpZForward,
        PerBodyZUpYForward,
    }

    public class CustomMapObject : MonoBehaviour
    {
        public SurfaceType surfaceType;

        [Header("Jib Settings")]
        public bool canStabilize = true;
        public bool canRotate = true;
        public bool canMagnetize = true;
        public IntendedUpMethod intendedUpMethod;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(CustomMapObject))]
    public class CustomMapObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var obj = (CustomMapObject) target;

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("surfaceType"));

            if (obj.surfaceType == SurfaceType.Jib)
            {
                UnityEditor.EditorGUILayout.Space(4);
                UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("canStabilize"));
                UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("canRotate"));
                UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("canMagnetize"));
                UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("intendedUpMethod"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif

    public class CustomMapObjectPartData : ISerializable
    {

        public string meshRef;
        public string baseTexRef;
        public string metallicTexRef;
        public string roughnessTexRef;
        public string normalTexRef;

        public Vector3 localPosition;
        public Vector3 localRotation;
        public Vector3 localScale;

        public CustomMapObjectPartData() {}

        public void Serialize(ISerializer serializer)
        {
            meshRef         = serializer.SerializeString("mesh", meshRef ?? "");
            baseTexRef      = serializer.SerializeString("base-tex", baseTexRef ?? "");
            metallicTexRef  = serializer.SerializeString("metallic-tex", metallicTexRef ?? "");
            roughnessTexRef = serializer.SerializeString("roughness-tex", roughnessTexRef ?? "");
            normalTexRef    = serializer.SerializeString("normal-tex", normalTexRef ?? "");

            localPosition = serializer.SerializeVector3("local-position", localPosition);
            localRotation = serializer.SerializeVector3("local-rotation", localRotation);
            localScale    = serializer.SerializeVector3("local-scale", localScale);
        }

    }

    public class CustomMapObjectData : ISerializable
    {

        public SurfaceType surfaceType;

        public bool canStabilize = true;
        public bool canRotate = true;
        public bool canMagnetize = true;
        public IntendedUpMethod intendedUpMethod;

        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        public CustomMapObjectPartData[] parts;
        public ColliderData[] colliders;

        public CustomMapObjectData() {}

        public void Serialize(ISerializer serializer)
        {
            surfaceType = (SurfaceType) serializer.SerializeInt("surface-type", (int) surfaceType);

            if (surfaceType == SurfaceType.Jib)
            {
                canStabilize     = serializer.SerializeBool("can-stabilize", canStabilize);
                canRotate        = serializer.SerializeBool("can-rotate", canRotate);
                canMagnetize     = serializer.SerializeBool("can-magnetize", canMagnetize);
                intendedUpMethod = (IntendedUpMethod) serializer.SerializeInt("intended-up-method", (int) intendedUpMethod);
            }

            position = serializer.SerializeVector3("position", position);
            rotation = serializer.SerializeVector3("rotation", rotation);
            scale    = serializer.SerializeVector3("scale", scale);

            serializer.SerializeSerializableArray("parts", ref parts, () => new CustomMapObjectPartData());
            serializer.SerializeSerializableArray("colliders", ref colliders, () => new ColliderData());
        }

    }

}
