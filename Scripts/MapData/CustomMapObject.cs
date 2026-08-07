using System;
using System.Collections.Generic;
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

        [Header("Culling")]
        [Tooltip("The game hides distant map objects for performance. Enable this to keep the object visible at any distance (big landmarks, decorative mountains, ...).")]
        public bool disableDistanceCulling;

        [Header("Visibility")]
        [Tooltip("Show this object only during a time-of-day window (24h clock). Collision stays active while hidden.")]
        public bool timedVisibility;
        public float visibleFromHour = 19f;
        public float visibleUntilHour = 6f;
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

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("disableDistanceCulling"));

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("timedVisibility"));
            if (obj.timedVisibility)
            {
                HourField(serializedObject.FindProperty("visibleFromHour"), "Show From");
                HourField(serializedObject.FindProperty("visibleUntilHour"), "Show Until");
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void HourField(UnityEditor.SerializedProperty prop, string label)
        {
            float v = UnityEditor.EditorGUILayout.Slider(new GUIContent($"{label} ({FormatHour(prop.floatValue)})"), prop.floatValue, 0f, 24f);
            prop.floatValue = Mathf.Round(v * 12f) / 12f;
        }

        static string FormatHour(float hours)
        {
            hours = Mathf.Repeat(hours, 24f);
            int h = Mathf.FloorToInt(hours);
            int m = Mathf.RoundToInt((hours - h) * 60f);
            return $"{h:00}:{m:00}";
        }
    }
#endif

    public enum CustomObjectRenderMode { Opaque = 0, AlphaClip = 1, Transparent = 2 }

    public class MaterialPropertyData : ISerializable
    {
        public int type;
        public float floatValue;
        public Vector4 vectorValue;
        public string textureRef;

        public MaterialPropertyData() {}

        public void Serialize(ISerializer serializer)
        {
            type = serializer.SerializeInt("type", type);
            switch (type)
            {
                case 0:
                    floatValue = serializer.SerializeFloat("v", floatValue);
                    break;
                case 1:
                case 2:
                    vectorValue.x = serializer.SerializeFloat("x", vectorValue.x);
                    vectorValue.y = serializer.SerializeFloat("y", vectorValue.y);
                    vectorValue.z = serializer.SerializeFloat("z", vectorValue.z);
                    vectorValue.w = serializer.SerializeFloat("w", vectorValue.w);
                    break;
                case 3:
                    textureRef = serializer.SerializeString("ref", textureRef ?? "");
                    break;
            }
        }
    }

    public class CustomMapObjectMaterialData : ISerializable
    {

        public string baseTexRef;
        public string metallicTexRef;
        public string roughnessTexRef;
        public string normalTexRef;
        public string emissionTexRef;

        public CustomObjectRenderMode renderMode;
        public float                  alphaCutoff = 0.5f;

        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public int     cullMode = 2;
        public Color   baseColor = Color.white;
        public Color   emissionColor = Color.black;

        public float metallic = 0f;
        public float smoothness = 0f;

        public bool lit = true;

        public Dictionary<string, MaterialPropertyData> extraProps;
        public string[] keywords;

        public CustomMapObjectMaterialData() {}

        public void Serialize(ISerializer serializer)
        {
            baseTexRef      = serializer.SerializeString("base-tex", baseTexRef ?? "");
            metallicTexRef  = serializer.SerializeString("metallic-tex", metallicTexRef ?? "");
            roughnessTexRef = serializer.SerializeString("roughness-tex", roughnessTexRef ?? "");
            normalTexRef    = serializer.SerializeString("normal-tex", normalTexRef ?? "");
            emissionTexRef  = serializer.SerializeString("emission-tex", emissionTexRef ?? "");
            renderMode      = (CustomObjectRenderMode) serializer.SerializeInt("render-mode", (int) renderMode);
            alphaCutoff     = serializer.SerializeFloat("alpha-cutoff", alphaCutoff);
            tiling          = serializer.SerializeVector2("tiling", tiling);
            offset          = serializer.SerializeVector2("offset", offset);
            cullMode        = serializer.SerializeInt("cull-mode", cullMode);
            baseColor.r     = serializer.SerializeFloat("base-color-r", baseColor.r);
            baseColor.g     = serializer.SerializeFloat("base-color-g", baseColor.g);
            baseColor.b     = serializer.SerializeFloat("base-color-b", baseColor.b);
            baseColor.a     = serializer.SerializeFloat("base-color-a", baseColor.a);
            emissionColor.r = serializer.SerializeFloat("emission-color-r", emissionColor.r);
            emissionColor.g = serializer.SerializeFloat("emission-color-g", emissionColor.g);
            emissionColor.b = serializer.SerializeFloat("emission-color-b", emissionColor.b);
            metallic        = serializer.SerializeFloat("metallic", metallic);
            smoothness      = serializer.SerializeFloat("smoothness", smoothness);
            lit             = serializer.SerializeBool("lit", lit);

            extraProps ??= new Dictionary<string, MaterialPropertyData>();
            serializer.SerializeSerializableDict("extra-props", ref extraProps, k => new MaterialPropertyData(), true);
            serializer.SerializeArray("keywords", ref keywords, (eId, val) => serializer.SerializeString(eId, val ?? ""));
        }

    }

    public class CustomMapObjectPartData : ISerializable
    {

        public string meshRef;

        public Vector3 localPosition;
        public Vector3 localRotation;
        public Vector3 localScale;

        public CustomMapObjectMaterialData[] materials;

        public int lodGroupIndex = -1;
        public int lodIndex = 0;

        public int shadowCastingMode = 1;

        public CustomMapObjectPartData() {}

        public void Serialize(ISerializer serializer)
        {
            meshRef       = serializer.SerializeString("mesh", meshRef ?? "");
            localPosition = serializer.SerializeVector3("local-position", localPosition);
            localRotation = serializer.SerializeVector3("local-rotation", localRotation);
            localScale    = serializer.SerializeVector3("local-scale", localScale);

            serializer.SerializeSerializableArray("materials", ref materials, () => new CustomMapObjectMaterialData());

            lodGroupIndex     = serializer.SerializeInt("lod-group-index", lodGroupIndex);
            lodIndex          = serializer.SerializeInt("lod-index", lodIndex);
            shadowCastingMode = serializer.SerializeInt("shadow-casting-mode", shadowCastingMode);
        }

    }

    public class LODGroupData : ISerializable
    {

        public Vector3 localPosition;
        public Vector3 localReferencePoint;
        public float size = 1f;
        public float[] transitions;
        public float[] fadeWidths;
        public int fadeMode = 0;
        public bool animateCrossFading = false;

        public LODGroupData() {}

        public void Serialize(ISerializer serializer)
        {
            localPosition       = serializer.SerializeVector3("local-position", localPosition);
            localReferencePoint = serializer.SerializeVector3("local-reference-point", localReferencePoint);
            size                = serializer.SerializeFloat("size", size);
            fadeMode            = serializer.SerializeInt("fade-mode", fadeMode);
            animateCrossFading  = serializer.SerializeBool("animate-crossfading", animateCrossFading);

            int count = serializer.SerializeInt("transition-count", transitions != null ? transitions.Length : 0);
            if (serializer.IsReader)
            {
                transitions = new float[count];
                fadeWidths  = new float[count];
            }
            for (int i = 0; i < count; i++)
            {
                transitions[i] = serializer.SerializeFloat("transition-" + i, transitions[i]);
                fadeWidths[i]  = serializer.SerializeFloat("fade-width-" + i, fadeWidths != null && i < fadeWidths.Length ? fadeWidths[i] : 0f);
            }
        }

    }

    public class LightData : ISerializable
    {

        public int type;
        public Vector3 localPosition;
        public Vector3 localRotation;
        public Color color = Color.white;
        public float intensity = 1f;
        public float range = 10f;
        public float spotAngle = 30f;
        public float innerSpotAngle = 21.8f;
        public int shadows;
        public float shadowStrength = 1f;

        public LightData() {}

        public void Serialize(ISerializer serializer)
        {
            type            = serializer.SerializeInt("type", type);
            localPosition   = serializer.SerializeVector3("local-position", localPosition);
            localRotation   = serializer.SerializeVector3("local-rotation", localRotation);
            color.r         = serializer.SerializeFloat("color-r", color.r);
            color.g         = serializer.SerializeFloat("color-g", color.g);
            color.b         = serializer.SerializeFloat("color-b", color.b);
            intensity       = serializer.SerializeFloat("intensity", intensity);
            range           = serializer.SerializeFloat("range", range);
            spotAngle       = serializer.SerializeFloat("spot-angle", spotAngle);
            innerSpotAngle  = serializer.SerializeFloat("inner-spot-angle", innerSpotAngle);
            shadows         = serializer.SerializeInt("shadows", shadows);
            shadowStrength  = serializer.SerializeFloat("shadow-strength", shadowStrength);
        }

    }

    public class CustomMapObjectData : ISerializable
    {

        public SurfaceType surfaceType;

        public bool canStabilize = true;
        public bool canRotate = true;
        public bool canMagnetize = true;
        public IntendedUpMethod intendedUpMethod;

        public bool disableDistanceCulling;

        public bool timedVisibility;
        public float visibleFromHour = 19f;
        public float visibleUntilHour = 6f;

        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;

        public CustomMapObjectPartData[] parts;
        public ColliderData[] colliders;
        public LODGroupData[] lodGroups;
        public LightData[] lights;

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

            disableDistanceCulling = serializer.SerializeBool("disable-distance-culling", disableDistanceCulling);

            timedVisibility = serializer.SerializeBool("timed-visibility", timedVisibility);
            if (timedVisibility)
            {
                visibleFromHour  = serializer.SerializeFloat("visible-from-hour", visibleFromHour);
                visibleUntilHour = serializer.SerializeFloat("visible-until-hour", visibleUntilHour);
            }

            position = serializer.SerializeVector3("position", position);
            rotation = serializer.SerializeVector3("rotation", rotation);
            scale    = serializer.SerializeVector3("scale", scale);

            serializer.SerializeSerializableArray("parts", ref parts, () => new CustomMapObjectPartData());
            serializer.SerializeSerializableArray("colliders", ref colliders, () => new ColliderData());
            serializer.SerializeSerializableArray("lod-groups", ref lodGroups, () => new LODGroupData());
            serializer.SerializeSerializableArray("lights", ref lights, () => new LightData());
        }

    }

}
