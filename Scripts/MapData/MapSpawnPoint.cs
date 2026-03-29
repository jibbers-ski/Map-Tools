using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class MapSpawnPoint : MonoBehaviour
    {

        public Vector3 velocity;

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = Color.red;
            Gizmos.DrawCube(new Vector3(0.11f, -0.9f, 0.1f), new Vector3(0.15f, 0.1f, 1.8f));
            Gizmos.DrawCube(new Vector3(-0.11f, -0.9f, 0.1f), new Vector3(0.15f, 0.1f, 1.8f));

            Gizmos.color = Color.black;
            Gizmos.DrawCube(new Vector3(0, 0.75f, 0.25f), new Vector3(0.3f, 0.2f, 0.1f));

            Gizmos.color = Color.green;
            Gizmos.DrawCube(Vector3.zero, new Vector3(0.5f, 2, 0.5f));
            Gizmos.DrawCube(new Vector3(0, 0.6f, 0), new Vector3(1.7f, 0.15f, 0.15f));

            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, velocity.normalized*2);
        }

    }

    public class SpawnPointData : ISerializable
    {

        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 velocity;

        public SpawnPointData() {}
        public SpawnPointData(MapSpawnPoint spawnPoint)
        {
            name = spawnPoint.name;
            position = spawnPoint.transform.position;
            rotation = Quaternion.LookRotation(spawnPoint.transform.forward).eulerAngles;
            velocity = spawnPoint.velocity;
        }

        public void Serialize(ISerializer serializer)
        {
            name = serializer.SerializeString("name", name);
            position = serializer.SerializeVector3("position", position);
            rotation = serializer.SerializeVector3("rotation", rotation);
            velocity = serializer.SerializeVector3("velocity", velocity);
        }
    }

}
