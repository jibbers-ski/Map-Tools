using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jibbers.MapTools
{

    public class MeshData : ISerializable
    {

        public int vertexCount;

        public byte[] vertexData;
        public byte[] normalData;
        public byte[] tangentData;
        public byte[] colorData;
        public byte[] uvData;
        public byte[] uv2Data;
        public byte[] uv3Data;
        public byte[] uv4Data;

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

            var normals  = mesh.normals;
            normalData   = (normals != null && normals.Length > 0)  ? PackVector3Array(normals)  : null;

            var tangents = mesh.tangents;
            tangentData  = (tangents != null && tangents.Length > 0) ? PackVector4Array(tangents) : null;

            var colors   = mesh.colors32;
            colorData    = (colors != null && colors.Length > 0)    ? PackColor32Array(colors)   : null;

            var uvs      = mesh.uv;
            uvData       = (uvs != null && uvs.Length > 0)          ? PackVector2Array(uvs)      : null;

            var uv2s     = mesh.uv2;
            uv2Data      = (uv2s != null && uv2s.Length > 0)        ? PackVector2Array(uv2s)     : null;

            var uv3s     = mesh.uv3;
            uv3Data      = (uv3s != null && uv3s.Length > 0)        ? PackVector2Array(uv3s)     : null;

            var uv4s     = mesh.uv4;
            uv4Data      = (uv4s != null && uv4s.Length > 0)        ? PackVector2Array(uv4s)     : null;

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
            tangentData = serializer.SerializeBytes("tangents", tangentData);
            colorData   = serializer.SerializeBytes("colors", colorData);
            uvData      = serializer.SerializeBytes("uvs", uvData);
            uv2Data     = serializer.SerializeBytes("uv2s", uv2Data);
            uv3Data     = serializer.SerializeBytes("uv3s", uv3Data);
            uv4Data     = serializer.SerializeBytes("uv4s", uv4Data);

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

            if (tangentData != null && tangentData.Length > 0)
                mesh.tangents = UnpackVector4Array(tangentData, vertexCount);
            else
                mesh.RecalculateTangents();

            if (colorData != null && colorData.Length > 0)
                mesh.colors32 = UnpackColor32Array(colorData, vertexCount);

            if (uvData != null && uvData.Length > 0)
                mesh.uv = UnpackVector2Array(uvData, vertexCount);

            if (uv2Data != null && uv2Data.Length > 0)
                mesh.uv2 = UnpackVector2Array(uv2Data, vertexCount);

            if (uv3Data != null && uv3Data.Length > 0)
                mesh.uv3 = UnpackVector2Array(uv3Data, vertexCount);

            if (uv4Data != null && uv4Data.Length > 0)
                mesh.uv4 = UnpackVector2Array(uv4Data, vertexCount);

            mesh.RecalculateBounds();
            return mesh;
        }

        static byte[] PackVector3Array(Vector3[] arr)
        {
            var bytes = new byte[arr.Length * 12];
            MemoryMarshal.AsBytes(arr.AsSpan()).CopyTo(bytes);
            return bytes;
        }

        static Vector3[] UnpackVector3Array(byte[] data, int count)
        {
            var arr = new Vector3[count];
            data.AsSpan(0, count * 12).CopyTo(MemoryMarshal.AsBytes(arr.AsSpan()));
            return arr;
        }

        static byte[] PackVector2Array(Vector2[] arr)
        {
            var bytes = new byte[arr.Length * 8];
            MemoryMarshal.AsBytes(arr.AsSpan()).CopyTo(bytes);
            return bytes;
        }

        static Vector2[] UnpackVector2Array(byte[] data, int count)
        {
            var arr = new Vector2[count];
            data.AsSpan(0, count * 8).CopyTo(MemoryMarshal.AsBytes(arr.AsSpan()));
            return arr;
        }

        static byte[] PackVector4Array(Vector4[] arr)
        {
            var bytes = new byte[arr.Length * 16];
            MemoryMarshal.AsBytes(arr.AsSpan()).CopyTo(bytes);
            return bytes;
        }

        static Vector4[] UnpackVector4Array(byte[] data, int count)
        {
            var arr = new Vector4[count];
            data.AsSpan(0, count * 16).CopyTo(MemoryMarshal.AsBytes(arr.AsSpan()));
            return arr;
        }

        static byte[] PackColor32Array(Color32[] arr)
        {
            var bytes = new byte[arr.Length * 4];
            MemoryMarshal.AsBytes(arr.AsSpan()).CopyTo(bytes);
            return bytes;
        }

        static Color32[] UnpackColor32Array(byte[] data, int count)
        {
            var arr = new Color32[count];
            data.AsSpan(0, count * 4).CopyTo(MemoryMarshal.AsBytes(arr.AsSpan()));
            return arr;
        }

    }

}
