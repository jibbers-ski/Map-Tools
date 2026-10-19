using System.Collections.Generic;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class LiftStation : MonoBehaviour
    {

        public Transform inPoint;
        public Transform outPoint;
        public Transform wheelCenter;
        public Transform cablePath;

        void OnDrawGizmosSelected()
        {
            var route = new List<Vector3>();
            if(LiftGeometry.TryGetRoute(cablePath, route))
            {
                Gizmos.color = MoreColors.Mint;
                for(int i = 1; i < route.Count; i++)
                    Gizmos.DrawLine(route[i - 1], route[i]);
                Gizmos.color = MoreColors.Azure;
                Gizmos.DrawSphere(route[0], 0.08f);
                Gizmos.color = MoreColors.JibbersOrange;
                Gizmos.DrawSphere(route[^1], 0.08f);
                return;
            }

            if(inPoint)
            {
                Gizmos.color = MoreColors.Azure;
                Gizmos.DrawSphere(inPoint.position, 0.08f);
            }
            if(outPoint)
            {
                Gizmos.color = MoreColors.JibbersOrange;
                Gizmos.DrawSphere(outPoint.position, 0.08f);
            }
            if(wheelCenter)
            {
                Gizmos.color = MoreColors.Mint;
                Gizmos.DrawRay(wheelCenter.position, wheelCenter.up * 0.5f);
                Gizmos.DrawRay(wheelCenter.position, wheelCenter.up * -0.5f);
            }
        }

    }

}
