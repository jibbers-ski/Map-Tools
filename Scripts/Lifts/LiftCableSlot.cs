using UnityEngine;

namespace Jibbers.MapTools
{

    public class LiftCableSlot : MonoBehaviour
    {

        public float min;
        public float max = 0.5f;

        void OnDrawGizmosSelected()
        {
            var origin = transform.position;
            var axis = transform.up;
            Gizmos.color = MoreColors.Mint;
            Gizmos.DrawLine(origin + axis * min, origin + axis * max);
            Gizmos.DrawCube(origin + axis * min, Vector3.one * 0.04f);
            Gizmos.DrawCube(origin + axis * max, Vector3.one * 0.04f);
        }

    }

}
