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

    public enum CustomObjectRenderMode { Opaque = 0, AlphaClip = 1, Transparent = 2 }

    public class CustomMapObjectMaterialData : ISerializable
    {

        public string baseTexRef;
        public string metallicTexRef;
        public string roughnessTexRef;
        public string normalTexRef;

        public CustomObjectRenderMode renderMode;
        public float                  alphaCutoff = 0.5f;

        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public int     cullMode = 2;
        public Color   baseColor = Color.white;

        public CustomMapObjectMaterialData() {}

        public void Serialize(ISerializer serializer)
        {
            baseTexRef      = serializer.SerializeString("base-tex", baseTexRef ?? "");
            metallicTexRef  = serializer.SerializeString("metallic-tex", metallicTexRef ?? "");
            roughnessTexRef = serializer.SerializeString("roughness-tex", roughnessTexRef ?? "");
            normalTexRef    = serializer.SerializeString("normal-tex", normalTexRef ?? "");
            renderMode      = (CustomObjectRenderMode) serializer.SerializeInt("render-mode", (int) renderMode);
            alphaCutoff     = serializer.SerializeFloat("alpha-cutoff", alphaCutoff);
            tiling          = serializer.SerializeVector2("tiling", tiling);
            offset          = serializer.SerializeVector2("offset", offset);
            cullMode        = serializer.SerializeInt("cull-mode", cullMode);
            baseColor.r     = serializer.SerializeFloat("base-color-r", baseColor.r);
            baseColor.g     = serializer.SerializeFloat("base-color-g", baseColor.g);
            baseColor.b     = serializer.SerializeFloat("base-color-b", baseColor.b);
            baseColor.a     = serializer.SerializeFloat("base-color-a", baseColor.a);
        }

    }

    public class CustomMapObjectPartData : ISerializable
    {

        public string meshRef;

        public Vector3 localPosition;
        public Vector3 localRotation;
        public Vector3 localScale;

        public CustomMapObjectMaterialData[] materials;

        public CustomMapObjectPartData() {}

        public void Serialize(ISerializer serializer)
        {
            meshRef       = serializer.SerializeString("mesh", meshRef ?? "");
            localPosition = serializer.SerializeVector3("local-position", localPosition);
            localRotation = serializer.SerializeVector3("local-rotation", localRotation);
            localScale    = serializer.SerializeVector3("local-scale", localScale);

            serializer.SerializeSerializableArray("materials", ref materials, () => new CustomMapObjectMaterialData());
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
