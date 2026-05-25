using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jibbers.MapTools
{

    public class MeshData : ISerializable
    {

        public int vertexCount;

        public byte[] vertexData;
        public byte[] normalData;
        public byte[] uvData;

        public byte[][] submeshTriangleData;

        public int triangleCount
        {
            get
            {
                if (submeshTriangleData == null) return 0;
                int total = 0;
                for (int i = 0; i < submeshTriangleData.Length; i++)
                    if (submeshTriangleData[i] != null)
                        total += submeshTriangleData[i].Length / sizeof(int);
                return total;
            }
        }

        public MeshData() {}

        public MeshData(Mesh mesh)
        {
            vertexCount = mesh.vertexCount;

            vertexData = PackVector3Array(mesh.vertices);

            var normals = mesh.normals;
            normalData = (normals != null && normals.Length > 0) ? PackVector3Array(normals) : null;

            var uvs = mesh.uv;
            uvData = (uvs != null && uvs.Length > 0) ? PackVector2Array(uvs) : null;

            int sm = Mathf.Max(1, mesh.subMeshCount);
            submeshTriangleData = new byte[sm][];
            for (int i = 0; i < sm; i++)
            {
                var tris = mesh.GetTriangles(i);
                var data = new byte[tris.Length * sizeof(int)];
                Buffer.BlockCopy(tris, 0, data, 0, data.Length);
                submeshTriangleData[i] = data;
            }
        }

        public void Serialize(ISerializer serializer)
        {
            vertexCount = serializer.SerializeInt("vertex-count", vertexCount);
            vertexData  = serializer.SerializeBytes("vertices", vertexData);
            normalData  = serializer.SerializeBytes("normals", normalData);
            uvData      = serializer.SerializeBytes("uvs", uvData);

            int sm = serializer.SerializeInt("submesh-count", submeshTriangleData != null ? submeshTriangleData.Length : 0);
            if (serializer.IsReader)
                submeshTriangleData = new byte[sm][];
            for (int i = 0; i < sm; i++)
                submeshTriangleData[i] = serializer.SerializeBytes("triangles-" + i,
                    submeshTriangleData != null ? submeshTriangleData[i] : null);
        }

        public Mesh GetMesh()
        {
            var mesh = new Mesh();
            mesh.name = "CustomMesh";
            if (vertexCount > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices = UnpackVector3Array(vertexData, vertexCount);

            int sm = submeshTriangleData != null ? submeshTriangleData.Length : 0;
            mesh.subMeshCount = Mathf.Max(1, sm);
            for (int i = 0; i < sm; i++)
            {
                var data = submeshTriangleData[i];
                if (data == null) continue;
                var tris = new int[data.Length / sizeof(int)];
                Buffer.BlockCopy(data, 0, tris, 0, data.Length);
                mesh.SetTriangles(tris, i);
            }

            if (normalData != null && normalData.Length > 0)
                mesh.normals = UnpackVector3Array(normalData, vertexCount);
            else
                mesh.RecalculateNormals();

            if (uvData != null && uvData.Length > 0)
                mesh.uv = UnpackVector2Array(uvData, vertexCount);

            mesh.RecalculateBounds();
            return mesh;
        }

        static byte[] PackVector3Array(Vector3[] arr)
        {
            var flat = new float[arr.Length * 3];
            for (int i = 0; i < arr.Length; i++)
            {
                flat[i * 3]     = arr[i].x;
                flat[i * 3 + 1] = arr[i].y;
                flat[i * 3 + 2] = arr[i].z;
            }
            var bytes = new byte[flat.Length * sizeof(float)];
            Buffer.BlockCopy(flat, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static Vector3[] UnpackVector3Array(byte[] data, int count)
        {
            var flat = new float[count * 3];
            Buffer.BlockCopy(data, 0, flat, 0, data.Length);
            var arr = new Vector3[count];
            for (int i = 0; i < count; i++)
                arr[i] = new Vector3(flat[i * 3], flat[i * 3 + 1], flat[i * 3 + 2]);
            return arr;
        }

        static byte[] PackVector2Array(Vector2[] arr)
        {
            var flat = new float[arr.Length * 2];
            for (int i = 0; i < arr.Length; i++)
            {
                flat[i * 2]     = arr[i].x;
                flat[i * 2 + 1] = arr[i].y;
            }
            var bytes = new byte[flat.Length * sizeof(float)];
            Buffer.BlockCopy(flat, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static Vector2[] UnpackVector2Array(byte[] data, int count)
        {
            var flat = new float[count * 2];
            Buffer.BlockCopy(data, 0, flat, 0, data.Length);
            var arr = new Vector2[count];
            for (int i = 0; i < count; i++)
                arr[i] = new Vector2(flat[i * 2], flat[i * 2 + 1]);
            return arr;
        }

    }

}
