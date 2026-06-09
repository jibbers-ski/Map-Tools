using System;
using UnityEngine;

namespace Jibbers.MapTools
{

    public class TextureData : ISerializable
    {

        public TextureFormat format;
        public int width;
        public int height;
        public string encoding;
        public bool isLinear = true;
        public bool mipmaps = false;

        public byte[] data;

        public TextureData() {}
        public TextureData(Texture2D texture)
        {
            width = texture.width;
            height = texture.height;
            format = TextureFormat.RGBA32;
            isLinear = DetectIsLinear(texture);
            mipmaps = DetectMipmaps(texture);

            if (!texture.isReadable)
            {
                Debug.LogError($"[TextureData] Texture '{texture.name}' is not readable. Enable Read/Write in import settings.");
                encoding = "raw";
                data = new byte[width * height * 4];
                return;
            }

            encoding = "png";
            data = texture.EncodeToPNG();
            if (data != null) return;

            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readable.SetPixels(texture.GetPixels());
            readable.Apply();
            data = readable.EncodeToPNG();
        }

        public void Serialize(ISerializer serializer)
        {
            format = (TextureFormat) serializer.SerializeInt("format", (int) format);
            width = serializer.SerializeInt("width", width);
            height = serializer.SerializeInt("height", height);
            encoding = serializer.SerializeString("encoding", encoding ?? "");
            isLinear = serializer.SerializeBool("linear", isLinear);
            mipmaps = serializer.SerializeBool("mipmaps", mipmaps);
            data = serializer.SerializeBytes("data", data);
        }

        public Texture2D GetTexture()
        {
            if (encoding == "png")
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipmaps, isLinear);
                texture.LoadImage(data, false);
                return texture;
            }

            var raw = new Texture2D(width, height, format, false, isLinear);
            raw.LoadRawTextureData(data);
            raw.Apply(false, false);
            return raw;
        }

        static bool DetectIsLinear(Texture2D texture)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                if (importer != null)
                    return !importer.sRGBTexture;
            }
#endif
            return true;
        }

        static bool DetectMipmaps(Texture2D texture)
        {
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
                if (importer != null)
                    return importer.mipmapEnabled;
            }
#endif
            return false;
        }

    }

}
