using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Jibbers.MapTools
{

    public class MovingPart : MonoBehaviour
    {
        public enum Motion { Spin, Swing, Bob }
        public enum Axis { X, Y, Z }
        public enum CaptureShape { Sphere, Cylinder, Box }
        public enum CaptureMode { Islands, Vertices }
        public enum PivotMode { CapturedCenter, TransformOrigin, CapturedTop, CapturedBottom }

        [Tooltip("Turns this marker into an exclusion: it removes geometry from the Moving Part above it instead of defining a part. Place it as a child of that part.")]
        public bool exclude;
        [Tooltip("Spin: turns continuously around the axis (wheels, rotors). Swing: swings back and forth around the axis (signs, lamps). Bob: slides back and forth along the axis (floats, pistons).")]
        public Motion motion;
        [Tooltip("Local axis this part moves around or along (for an exclusion: the axis of its cylinder region). Unity cylinders spin around Y.")]
        public Axis axis = Axis.Y;
        [Tooltip("Shape of the capture region around this object. Sizes are world meters, oriented by this object's rotation - scale has no effect.")]
        public CaptureShape shape;
        [Tooltip("Radius of the sphere or cylinder region, in meters.")]
        public float captureRadius = 0.25f;
        [Tooltip("Length of the cylinder region along the axis, in meters.")]
        public float captureHeight = 0.2f;
        [Tooltip("Size of the box region along this object's local axes, in meters.")]
        public Vector3 captureSize = new(0.5f, 0.2f, 0.5f);
        [Tooltip("Islands: whole loose parts of the mesh whose center lies inside the region (a wheel modelled as its own shell). Vertices: every vertex inside the region, for parts welded to static geometry.")]
        public CaptureMode capture;
        [Tooltip("How far the part turns: one full turn per 2π·radius meters of drive, as if its rim rolled along the cable. 0 = measured from the captured geometry.")]
        public float rollingRadius;
        [Tooltip("Spin the other way, e.g. sheaves on the return strand of a lift.")]
        public bool reverse;
        [Tooltip("How far the part swings to each side, in degrees.")]
        [Range(0f, 180f)] public float swingAngle = 15f;
        [Tooltip("How far the part moves to each side, in meters.")]
        [Range(0f, 2.5f)] public float bobDistance = 0.2f;
        [Tooltip("Length of one full back-and-forth cycle. Seconds at Auto Speed 1, meters of drive when a script drives the material.")]
        public float period = 4f;
        [Tooltip("Every part starts its cycle at a different point, derived from its position, so rows of parts move out of step.")]
        public bool randomPhase = true;
        [Tooltip("Where in the cycle this part starts, 0 to 1.")]
        [Range(0f, 1f)] public float phase;
        [Tooltip("Where the motion center sits: the center of the captured geometry, this object's position, or the top / bottom edge of the captured geometry (a hanging sign swings from its top, a hinged flap from its bottom).")]
        public PivotMode pivot;

        [HideInInspector] public bool baked;
        [HideInInspector] public Vector3 bakedLocalPivot;
        [HideInInspector] public float bakedRadius;

        public Vector3 LocalAxis => axis switch { Axis.X => Vector3.right, Axis.Z => Vector3.forward, _ => Vector3.up };
        public Vector3 WorldAxis => transform.rotation * LocalAxis;
        public bool HasMeshes => !exclude && GetComponentInChildren<MeshFilter>();

        public bool Contains(Vector3 worldPoint)
        {
            var p = Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);
            switch (shape)
            {
                case CaptureShape.Box:
                    return Mathf.Abs(p.x) <= captureSize.x * 0.5f && Mathf.Abs(p.y) <= captureSize.y * 0.5f && Mathf.Abs(p.z) <= captureSize.z * 0.5f;
                case CaptureShape.Cylinder:
                    int a = (int) axis;
                    float along = p[a];
                    p[a] = 0f;
                    return Mathf.Abs(along) <= captureHeight * 0.5f && p.sqrMagnitude <= captureRadius * captureRadius;
                default:
                    return p.sqrMagnitude <= captureRadius * captureRadius;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Handles.color = exclude ? MoreColors.Crimson : reverse ? MoreColors.Azure : MoreColors.JibbersOrange;
            if (baked && !exclude)
            {
                var center = transform.TransformPoint(bakedLocalPivot);
                var dir = WorldAxis;
                Handles.DrawLine(center - dir * (bakedRadius + 0.2f), center + dir * (bakedRadius + 0.2f), 2f);
                Handles.DrawWireDisc(center, dir, bakedRadius, 2f);
                return;
            }

            using (new Handles.DrawingScope(Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one)))
            {
                var a = LocalAxis;
                if (HasMeshes)
                {
                    Handles.DrawLine(-a, a, 2f);
                    return;
                }
                switch (shape)
                {
                    case CaptureShape.Box:
                        Handles.DrawLine(-a * captureSize.magnitude, a * captureSize.magnitude, 2f);
                        Handles.DrawWireCube(Vector3.zero, captureSize);
                        break;
                    case CaptureShape.Cylinder:
                        var b = axis == Axis.X ? Vector3.up : Vector3.right;
                        var c = Vector3.Cross(a, b);
                        var h = a * (captureHeight * 0.5f);
                        Handles.DrawLine(-h - a * 0.2f, h + a * 0.2f, 2f);
                        Handles.DrawWireDisc(h, a, captureRadius, 2f);
                        Handles.DrawWireDisc(-h, a, captureRadius, 2f);
                        foreach (var side in new[] { b, -b, c, -c })
                            Handles.DrawLine(side * captureRadius - h, side * captureRadius + h);
                        break;
                    default:
                        Handles.DrawLine(-a * (captureRadius + 0.2f), a * (captureRadius + 0.2f), 2f);
                        Handles.DrawWireDisc(Vector3.zero, Vector3.right, captureRadius, 2f);
                        Handles.DrawWireDisc(Vector3.zero, Vector3.up, captureRadius, 2f);
                        Handles.DrawWireDisc(Vector3.zero, Vector3.forward, captureRadius, 2f);
                        break;
                }
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MovingPart)), CanEditMultipleObjects]
    public class MovingPartEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var part = (MovingPart) target;

            Draw("exclude");
            if (part.exclude)
                EditorGUILayout.HelpBox("Removes geometry from the Moving Part above this object. Islands: every loose part this region touches (an axle, a fork). Vertices: every vertex inside.", MessageType.None);
            else
                Draw("motion");
            Draw("axis");
            if (part.HasMeshes)
                EditorGUILayout.HelpBox("This object has meshes of its own - they are the part. No capture region needed; child markers with Exclude ticked can still remove geometry from it.", MessageType.None);
            else
            {
                Draw("shape");
                switch (part.shape)
                {
                    case MovingPart.CaptureShape.Box:
                        Draw("captureSize");
                        break;
                    case MovingPart.CaptureShape.Cylinder:
                        Draw("captureRadius");
                        Draw("captureHeight");
                        break;
                    default:
                        Draw("captureRadius");
                        break;
                }
                Draw("capture");
            }
            if (!part.exclude)
            {
                switch (part.motion)
                {
                    case MovingPart.Motion.Swing:
                        Draw("swingAngle");
                        Draw("period");
                        Draw("randomPhase");
                        if (!part.randomPhase) Draw("phase");
                        break;
                    case MovingPart.Motion.Bob:
                        Draw("bobDistance");
                        Draw("period");
                        Draw("randomPhase");
                        if (!part.randomPhase) Draw("phase");
                        break;
                    default:
                        Draw("rollingRadius");
                        Draw("reverse");
                        break;
                }
                Draw("pivot");
            }

            serializedObject.ApplyModifiedProperties();
        }

        void Draw(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
    }
#endif

}
