using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class LiftMast : MonoBehaviour
    {

        public Transform upPath;
        public Transform downPath;

        void OnDrawGizmosSelected()
        {
            DrawPath(upPath, MoreColors.JibbersOrange);
            DrawPath(downPath, MoreColors.Azure);
        }

        static void DrawPath(Transform root, Color color)
        {
            var route = new List<Vector3>();
            LiftGeometry.TryGetRoute(root, route);
            Gizmos.color = color;
            for(int i = 0; i < route.Count; i++)
            {
                Gizmos.DrawSphere(route[i], 0.05f);
                if(i > 0)
                    Gizmos.DrawLine(route[i - 1], route[i]);
            }
        }

    }

}
