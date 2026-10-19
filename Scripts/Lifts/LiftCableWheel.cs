using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class LiftCableWheel : MonoBehaviour
    {

        public float radius = 0.5f;
        public float arcDegrees = 180f;

        public void AppendArc(List<Vector3> route)
        {
            var center = transform.position;
            var axis = transform.up;
            var start = transform.forward * radius;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(arcDegrees) / LiftGeometry.ArcSampleDegrees), 1, 64);
            for(int s = 0; s <= steps; s++)
                route.Add(center + Quaternion.AngleAxis(arcDegrees * s / steps, axis) * start);
        }

        void OnDrawGizmosSelected()
        {
            var center = transform.position;
            var axis = transform.up;
            var start = transform.forward * radius;

            Gizmos.color = MoreColors.Mint;
            var prev = center + start;
            for(int s = 1; s <= 32; s++)
            {
                var p = center + Quaternion.AngleAxis(360f * s / 32f, axis) * start;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawRay(center, axis * 0.3f);

            Gizmos.color = MoreColors.JibbersOrange;
            prev = center + start;
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(arcDegrees) / LiftGeometry.ArcSampleDegrees));
            for(int s = 1; s <= steps; s++)
            {
                var p = center + Quaternion.AngleAxis(arcDegrees * s / steps, axis) * start;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawRay(center, start);
        }

    }

}
