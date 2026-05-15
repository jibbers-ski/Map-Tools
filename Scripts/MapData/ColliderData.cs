using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public enum ColliderShape { Box, Sphere, Capsule, Mesh }
    public enum SurfaceType { Generic, Snow, Jib }

    public class ColliderData : ISerializable
    {

        public ColliderShape shape;
        public SurfaceType surfaceType;

        public Vector3 localPosition;
        public Vector3 localRotation;
        public Vector3 localScale;

        public Vector3 center;
        public Vector3 size;
        public float radius;
        public float height;
        public int direction;
        public string meshRef;

        public ColliderData() {}

        public void Serialize(ISerializer serializer)
        {
            shape       = (ColliderShape) serializer.SerializeInt("shape", (int) shape);
            surfaceType = (SurfaceType) serializer.SerializeInt("surface-type", (int) surfaceType);

            localPosition = serializer.SerializeVector3("local-position", localPosition);
            localRotation = serializer.SerializeVector3("local-rotation", localRotation);
            localScale    = serializer.SerializeVector3("local-scale", localScale);

            center    = serializer.SerializeVector3("center", center);
            size      = serializer.SerializeVector3("size", size);
            radius    = serializer.SerializeFloat("radius", radius);
            height    = serializer.SerializeFloat("height", height);
            direction = serializer.SerializeInt("direction", direction);
            meshRef   = serializer.SerializeString("mesh-ref", meshRef ?? "");
        }

    }

}
