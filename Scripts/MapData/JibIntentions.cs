using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Jibbers.MapTools
{
    public static class JibIntentions
    {
        public struct Body
        {
            public Vector3 center;
            public Quaternion rotation;
            public float size;
        }

        public static readonly IntendedUpMethod[] MenuOrder =
        {
            IntendedUpMethod.LocalBody,
            IntendedUpMethod.LocalBodyRightProjected,
            IntendedUpMethod.LocalBodyForwardProjected,
            IntendedUpMethod.PerBodyRightProjected,
            IntendedUpMethod.PerBodyForwardProjected,
            IntendedUpMethod.PerBodyXUpYForward,
            IntendedUpMethod.PerBodyXUpZForward,
            IntendedUpMethod.PerBodyYUpXForward,
            IntendedUpMethod.PerBodyYUpZForward,
            IntendedUpMethod.PerBodyZUpXForward,
            IntendedUpMethod.PerBodyZUpYForward,
        };

        public static string Label(IntendedUpMethod method) => method switch
        {
            IntendedUpMethod.LocalBody => "Local Body",
            IntendedUpMethod.LocalBodyRightProjected => "Local Body Right Projected",
            IntendedUpMethod.LocalBodyForwardProjected => "Local Body Forward Projected",
            IntendedUpMethod.PerBodyRightProjected => "Per Body Right Projected",
            IntendedUpMethod.PerBodyForwardProjected => "Per Body Forward Projected",
            IntendedUpMethod.PerBodyXUpYForward => "Per Body X Up, Y Forward",
            IntendedUpMethod.PerBodyXUpZForward => "Per Body X Up, Z Forward",
            IntendedUpMethod.PerBodyYUpXForward => "Per Body Y Up, X Forward",
            IntendedUpMethod.PerBodyYUpZForward => "Per Body Y Up, Z Forward",
            IntendedUpMethod.PerBodyZUpXForward => "Per Body Z Up, X Forward",
            IntendedUpMethod.PerBodyZUpYForward => "Per Body Z Up, Y Forward",
            _ => method.ToString(),
        };

        public const string Legend ="Select the object to see the intended frame in the Scene view: green = intended up (what the skier is balanced towards), blue = grind direction (sign does not matter), magenta = the across axis with the plane the up vector may tilt in.";

        public static bool IsPerBody(IntendedUpMethod method) => method is not (IntendedUpMethod.LocalBody or IntendedUpMethod.LocalBodyRightProjected or IntendedUpMethod.LocalBodyForwardProjected);

        public static bool IsProjected(IntendedUpMethod method) => method is IntendedUpMethod.LocalBodyRightProjected or IntendedUpMethod.LocalBodyForwardProjected or IntendedUpMethod.PerBodyRightProjected or IntendedUpMethod.PerBodyForwardProjected;

        public static Quaternion CapsuleRotation(int direction) => direction switch
        {
            0 => Quaternion.Euler(0, 0, 90),
            2 => Quaternion.Euler(90, 0, 0),
            _ => Quaternion.identity,
        };

        public static string Describe(IntendedUpMethod method) => method switch
        {
            IntendedUpMethod.LocalBody => "One fixed frame from this object for all its colliders: intended up = the object's local Y, grind direction = its local Z. Straight flat rails, boxes, ledges.",
            IntendedUpMethod.LocalBodyRightProjected => "Up follows the surface: the contact normal is flattened so it can only tilt around this object's local X, which must point ACROSS the rail. Grind direction = X × up, i.e. along the rail. Boxes modelled with Z along their length that curve up or down (rainbows, kinks)",
            IntendedUpMethod.LocalBodyForwardProjected => "Up follows the surface: the contact normal is flattened so it can only tilt around this object's local Z, which must point ACROSS the rail. Grind direction = Z × up, i.e. along the rail. Same idea as Right Projected for rails modelled with X along their length.",
            IntendedUpMethod.PerBodyYUpZForward => "Every collider piece has its own fixed frame: up = the piece's Y, grind direction = the piece's Z. For segmented or curved rails built from several rotated pieces.",
            IntendedUpMethod.PerBodyZUpYForward => "Every collider piece has its own fixed frame: up = the piece's Z, grind direction = the piece's Y. For segmented or curved rails built from several rotated pieces.",
            IntendedUpMethod.PerBodyYUpXForward => "Every collider piece has its own fixed frame: up = the piece's Y, grind direction = the piece's X. For segmented or curved rails built from several rotated pieces.",
            IntendedUpMethod.PerBodyZUpXForward => "Every collider piece has its own fixed frame: up = the piece's Z, grind direction = the piece's X. For segmented or curved rails built from several rotated pieces.",
            IntendedUpMethod.PerBodyXUpYForward => "Every collider piece has its own fixed frame: up = the piece's X, grind direction = the piece's Y. For segmented or curved rails built from several rotated pieces.",
            IntendedUpMethod.PerBodyXUpZForward => "Every collider piece has its own fixed frame: up = the piece's X, grind direction = the piece's Z. For segmented or curved rails built from several rotated pieces.",
            IntendedUpMethod.PerBodyRightProjected => "Per collider piece, up follows the surface: the contact normal is flattened so it can only tilt around the piece's X, which must point across the rail. Grind direction = X × up.",
            IntendedUpMethod.PerBodyForwardProjected => "Per collider piece, up follows the surface: the contact normal is flattened so it can only tilt around the piece's Z, which must point across the rail. Grind direction = Z × up.",
            _ => "",
        };

        public static void Resolve(IntendedUpMethod method, Quaternion feature, Quaternion body, Vector3 contactNormal, out Vector3 up, out Vector3 direction)
        {
            var frame = IsPerBody(method) ? body : feature;
            Vector3 x = frame * Vector3.right, y = frame * Vector3.up, z = frame * Vector3.forward;
            switch(method)
            {
                case IntendedUpMethod.PerBodyZUpYForward: up = z; direction = y; break;
                case IntendedUpMethod.PerBodyYUpXForward: up = y; direction = x; break;
                case IntendedUpMethod.PerBodyZUpXForward: up = z; direction = x; break;
                case IntendedUpMethod.PerBodyXUpYForward: up = x; direction = y; break;
                case IntendedUpMethod.PerBodyXUpZForward: up = x; direction = z; break;
                case IntendedUpMethod.LocalBodyRightProjected:
                case IntendedUpMethod.PerBodyRightProjected: Project(x, contactNormal, out up, out direction); break;
                case IntendedUpMethod.LocalBodyForwardProjected:
                case IntendedUpMethod.PerBodyForwardProjected: Project(z, contactNormal, out up, out direction); break;
                default: up = y; direction = z; break;
            }
        }

        public static Body BodyOf(Collider collider) => new()
        {
            center = collider.bounds.center,
            rotation = collider.transform.rotation * (collider is CapsuleCollider capsule ? CapsuleRotation(capsule.direction) : Quaternion.identity),
            size = collider.bounds.extents.magnitude * 0.6f,
        };

        static void Project(Vector3 across, Vector3 contactNormal, out Vector3 up, out Vector3 direction)
        {
            up = (contactNormal - Vector3.Dot(contactNormal, across) * across).normalized;
            direction = Vector3.Cross(across, up).normalized;
        }

#if UNITY_EDITOR
        public static void MethodField(SerializedProperty property)
        {
            var labels = new GUIContent[MenuOrder.Length];
            for(int i = 0; i < labels.Length; ++i)
                labels[i] = new GUIContent(Label(MenuOrder[i]));

            var rect = EditorGUILayout.GetControlRect();
            var label = EditorGUI.BeginProperty(rect, new GUIContent("Intended Up Method", "How the game derives the up direction and grind direction it balances the skier towards while on this jib."), property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int index = EditorGUI.Popup(rect, label, System.Array.IndexOf(MenuOrder, (IntendedUpMethod)property.intValue), labels);
            if(EditorGUI.EndChangeCheck() && index >= 0)
                property.intValue = (int)MenuOrder[index];
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();

            if(!property.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox(Describe((IntendedUpMethod)property.intValue), MessageType.Info);
            EditorGUILayout.HelpBox(Legend, MessageType.None);
        }

        public static void DrawGizmos(IntendedUpMethod method, Transform feature, IList<Body> bodies)
        {
            if(IsPerBody(method))
            {
                foreach(var body in bodies)
                    DrawFrame(method, feature.rotation, body.rotation, body.center, body.size);
            }
            else
            {
                var center = feature.position;
                float size = 0.5f;
                if(bodies.Count > 0)
                {
                    center = Vector3.zero;
                    foreach(var body in bodies)
                    {
                        center += body.center / bodies.Count;
                        size = Mathf.Max(size, body.size);
                    }
                }
                DrawFrame(method, feature.rotation, feature.rotation, center, size);
            }
            Handles.Label(feature.position + Vector3.up * 0.25f, "Intended up: " + method);
        }

        static void DrawFrame(IntendedUpMethod method, Quaternion feature, Quaternion body, Vector3 center, float size)
        {
            size = Mathf.Clamp(size, 0.25f, 2f);
            if(IsProjected(method))
            {
                var frame = IsPerBody(method) ? body : feature;
                var across = frame * (method is IntendedUpMethod.LocalBodyRightProjected or IntendedUpMethod.PerBodyRightProjected ? Vector3.right : Vector3.forward);
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(center - across * size, center + across * size);
                Handles.color = new Color(1, 0, 1, 0.35f);
                Handles.DrawWireDisc(center, across, size);
                for(int i = -1; i <= 1; ++i)
                {
                    Resolve(method, feature, body, Quaternion.AngleAxis(i * 35f, across) * (frame * Vector3.up), out var up, out var direction);
                    Arrow(center, up * size * (i == 0 ? 1f : 0.7f), i == 0 ? Color.green : new Color(0, 1, 0, 0.4f));
                    if(i == 0)
                        Arrow(center, direction * size, Color.blue);
                }
            }
            else
            {
                Resolve(method, feature, body, Vector3.up, out var up, out var direction);
                Arrow(center, up * size, Color.green);
                Arrow(center, direction * size, Color.blue);
            }
        }

        static void Arrow(Vector3 origin, Vector3 vector, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(origin, origin + vector);
            Handles.color = color;
            Handles.ConeHandleCap(0, origin + vector, Quaternion.LookRotation(vector), vector.magnitude * 0.2f, EventType.Repaint);
        }
#endif
    }
}
