using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{
    public static partial class Utility
    {

        public static readonly string Version = "0.4";
        public static string NewGuid => Guid.NewGuid().ToString();
        public static string DataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/Jibbers/";

        public static void DrawLineGizmo(List<Vector3> points, float sphereRadius = 0.05f, Vector3 offset = default)
        {
            var last = points[0] + offset;
            for(int i = 1; i < points.Count; ++i)
            {
                var current = points[i] + offset;
                Gizmos.DrawSphere(last, sphereRadius);
                Gizmos.DrawLine(last, current);
                last = current;
            }
            Gizmos.DrawSphere(last, sphereRadius);
        }

    }
}
